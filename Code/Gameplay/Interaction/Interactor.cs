using System;
using System.Linq;

namespace UnboxedLife;

public sealed class Interactor : Component
{
	[Property] public float TraceDistance { get; set; } = 200f;
	[Property] public float EyeHeight { get; set; } = 64f;
	[Property] public bool DebugTrace { get; set; } = true;
	[Property] public float DebugDuration { get; set; } = 0.05f;
	
	public bool IsHovering => _hovered is not null;
	public Interactable Hovered => _hovered;
	private Interactable _hovered;
	private GameObject _lastHoveredGo;
	private bool _hasAccessCached = true;
	private string _accessReasonCached = null;
	public Door HoverDoor { get; private set; }
	public PropertyZone HoverZone { get; private set; }

	public string HoverPrompt
	{
		get
		{
			if ( _hovered is null ) return "Use";

			if ( _hovered.RequirePropertyAccess && !_hasAccessCached )
				return _accessReasonCached ?? "No access";

			var pawn = PawnResolver.GetLocalPawn( Scene );
			return _hovered.GetPrompt( pawn );
		}
	}

	public string HoverReason
	{
		get
		{
			if ( _hovered is null ) return null;
			if ( !_hovered.RequirePropertyAccess ) return null;

			return _hasAccessCached ? null : (_accessReasonCached ?? "No access");
		}
	}

	protected override void OnUpdate()
	{
		// Find the local pawn (same pattern as DebugTool.cs)
		var pawn = PawnResolver.GetLocalPawn( Scene );
		if ( pawn is null )
			return;

		var pc = pawn.Components.Get<Sandbox.PlayerController>();

		var tr = TraceUtil.TraceFromEyes(
			pawn,
			pc,
			EyeHeight,
			TraceDistance
		);

		_hovered = null;
		HoverDoor = null;
		HoverZone = null;

		if ( tr.Hit && tr.GameObject is not null )
		{
			// 1) Always populate door/zone info from what you're looking at
			var door = tr.GameObject.Components.Get<Door>( FindMode.InSelf | FindMode.InAncestors );
			if ( door is not null )
			{
				HoverDoor = door;

				var pos = (door.DoorPivot?.IsValid() ?? false)
					? door.DoorPivot.WorldPosition
					: door.WorldPosition;

				HoverZone = PropertyZoneRegistry.FindZoneAt( pos );
			}

			// 2) Separately choose the best interactable for the action prompt / Use
			_hovered = ChooseInteractableForUI( tr.GameObject, pawn );
		}

		var hoveredGo = _hovered?.GameObject;

		if ( hoveredGo != _lastHoveredGo )
		{
			_lastHoveredGo = hoveredGo;
			_hasAccessCached = true;
			_accessReasonCached = null;

			if ( hoveredGo is not null )
				RequestAccessInfoRpc( hoveredGo );
		}

		if ( Input.Pressed( "use" ) )
		{
			Log.Info( "use pressed (Interactor)" );

			if ( _hovered is not null )
			{
				RequestUseRpc( _hovered.GameObject );
			}
			else
			{
				Log.Info( "no interactable hovered" );
			}
		}
	}

	[Rpc.Host]
	private void RequestUseRpc( GameObject target )
	{
		if ( !Networking.IsHost )
			return;

		var owner = GameObject.Network?.Owner;
		if ( owner is null || Rpc.Caller != owner )
		{
			Log.Info( $"[Interactor][HOST] RequestUse denied: caller mismatch. caller={Rpc.Caller?.DisplayName} owner={owner?.DisplayName}" );
			return;
		}
		//<summary> If you later decide you want some interactables to be server-only, non-networked (possible), then you can remove the target.Network is null guard or make it conditional. For now, given your PvP/security goals, requiring networked targets is reasonable.</summary>
		if ( target is null || !target.IsValid )
		{
			Log.Info( $"[Interactor][HOST] RequestUse denied: invalid target" );
			return;
		}
		if ( target.Network is null )
		{
			Log.Info( $"[Interactor][HOST] RequestUse denied: target not networked target={target.Name}" );
			return;
		}

		Log.Info( $"[Interactor][HOST] RequestUse received: caller={Rpc.Caller.DisplayName} target={target.Name}" );


		var pawn = Scene.GetAllObjects( true )
			.FirstOrDefault( go =>
				go.Network?.Owner == Rpc.Caller &&
				go.Components.Get<Sandbox.PlayerController>() is not null );

		if ( pawn is null )
		{
			Log.Info( $"[Interactor][HOST] RequestUse denied: no pawn for caller={Rpc.Caller.DisplayName}" );
			return;
		}
		var interactables = target.Components.GetAll<Interactable>( FindMode.InSelf | FindMode.InAncestors )
			.ToList();

		if ( !interactables.Any() )
		{
			Log.Info( $"[Interactor][HOST] RequestUse denied: no Interactable on target={target.Name}" );
			return;
		}

		Log.Info( $"[Interactor][HOST] Interactables on target={target.Name} count={interactables.Count} :: " +
			string.Join( ", ", interactables.Select( i => $"{i.GetType().Name}:{i.Action}" ) ) );

		Interactable chosen = null;

		foreach ( var i in interactables )
		{
			bool ok = false;
			try { ok = i.CanInteract( pawn ); }
			catch ( Exception e )
			{
				Log.Info( $"[Interactor][HOST] CanInteract exception on {i.GetType().Name}:{i.Action} :: {e.Message}" );
			}

			Log.Info( $"[Interactor][HOST] CanInteract {i.GetType().Name}:{i.Action} -> {ok}" );

			if ( ok )
			{
				chosen = i;
				break;
			}
		}

		if ( chosen is null )
		{
			Log.Info( $"[Interactor][HOST] RequestUse denied: no interactable allowed interaction on target={target.Name}" );
			return;
		}

		Log.Info( $"[Interactor][HOST] Chosen interactable: {chosen.GetType().Name}:{chosen.Action}" );
		chosen.InteractHost( pawn );
	}


	[Rpc.Host]
	private void RequestAccessInfoRpc( GameObject target )
	{
		if ( !Networking.IsHost ) return;

		var owner = GameObject.Network?.Owner;
		if ( owner is null || Rpc.Caller != owner ) return;

		if ( target is null || !target.IsValid ) return;

		var pawn = Scene.GetAllObjects( true )
			.FirstOrDefault( go =>
				go.Network?.Owner == owner &&
				go.Components.Get<Sandbox.PlayerController>() is not null );

		var interactable = (pawn is null) ? null : ChooseInteractableForUI( target, pawn );
		if ( interactable is null ) return;

		// Determine zone/access for UI only (do NOT rely on this for enforcement)
		var zone = PropertyZoneRegistry.FindZoneAt( target.WorldPosition );

		bool allowed = true;
		string reason = null;

		if ( interactable.RequirePropertyAccess )
		{
			if ( zone is null )
			{
				allowed = false;
				reason = "Not in a property";
			}
			else if ( !zone.HasAccess( owner.SteamId ) )
			{
				allowed = false;
				reason = "No access";
			}
		}

		ReceiveAccessInfoRpc( target, allowed, reason );
	}

	[Rpc.Owner]
	private void ReceiveAccessInfoRpc( GameObject target, bool allowed, string reason )
	{
		// cache only if still hovered
		if ( target == _lastHoveredGo )
		{
			_hasAccessCached = allowed;
			_accessReasonCached = reason;
		}
	}

	private static int GetPriority( Interactable i, GameObject interactor )
	{
		// Higher wins.
		if ( i is DoorInteractable di )
		{
			var equip = interactor?.Components.Get<EquipComponent>();

			if ( di.Action == PropertyAction.LockpickDoor && equip?.ActiveSlot == EquipComponent.Slot.Lockpick )
				return 100;

			if ( di.Action == PropertyAction.OpenDoor )
				return 10;
		}

		return 0;
	}

	private static Interactable ChooseInteractableForUI( GameObject target, GameObject interactor )
	{
		var list = target.Components.GetAll<Interactable>( FindMode.InSelf | FindMode.InAncestors ).ToList();
		if ( list.Count == 0 ) return null;

		return list
			.Where( x => x is not null && x.CanPreview( interactor ) )
			.OrderByDescending( x => GetPriority( x, interactor ) )
			.FirstOrDefault();
	}


}
