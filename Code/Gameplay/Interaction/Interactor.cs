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

		_hovered = tr.Hit
			? tr.GameObject?.Components.Get<Interactable>( FindMode.InSelf | FindMode.InAncestors )
			: null;

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
			return;

		//<summary> If you later decide you want some interactables to be server-only, non-networked (possible), then you can remove the target.Network is null guard or make it conditional. For now, given your PvP/security goals, requiring networked targets is reasonable.</summary>
		if ( target is null || !target.IsValid )
			return;

		if ( target.Network is null )
			return;

		var pawn = Scene.GetAllObjects( true )
			.FirstOrDefault( go =>
				go.Network?.Owner == Rpc.Caller &&
				go.Components.Get<Sandbox.PlayerController>() is not null );

		if ( pawn is null )
			return;

		var interactable = target.Components.Get<Interactable>( FindMode.InSelf | FindMode.InAncestors );
		if ( interactable is null )
			return;

		interactable.InteractHost( pawn );
	}


	[Rpc.Host]
	private void RequestAccessInfoRpc( GameObject target )
	{
		if ( !Networking.IsHost ) return;

		var owner = GameObject.Network?.Owner;
		if ( owner is null || Rpc.Caller != owner ) return;

		if ( target is null || !target.IsValid ) return;

		var interactable = target.Components.Get<Interactable>( FindMode.InSelf | FindMode.InAncestors );
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

}
