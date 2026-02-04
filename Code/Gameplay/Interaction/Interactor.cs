namespace UnboxedLife;

public sealed class Interactor : Component
{
	[Property] public float TraceDistance { get; set; } = 200f;
	[Property] public float EyeHeight { get; set; } = 64f;

	[Property] public bool DebugTrace { get; set; } = true;
	[Property] public float DebugDuration { get; set; } = 0.05f;

	public Interactable Hovered => _hovered;
	public string HoverPrompt => _hovered?.Prompt ?? "Use";


	private Interactable _hovered;
	public bool IsHovering => _hovered is not null;

	protected override void OnUpdate()
	{
		// Find the local pawn (same pattern as DebugTool.cs)
		var pawn = Scene.GetAllObjects( true )
			.FirstOrDefault( go =>
				go.Network?.IsOwner == true &&
				go.Components.Get<NetworkIdentification>() is not null );

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

		// Caller must own this Interactor's object (your PlayerState)
		var owner = GameObject.Network?.Owner;
		if ( owner is null || Rpc.Caller != owner )
			return;

		var pawn = Scene.GetAllObjects( true )
			.FirstOrDefault( go =>
				go.Network?.Owner == Rpc.Caller &&
				go.Components.Get<NetworkIdentification>() is not null );

		if ( pawn is null )
			return;

		if ( target is null || !target.IsValid )
			return;

		var interactable = target.Components.Get<Interactable>( FindMode.InSelf | FindMode.InAncestors );
		if ( interactable is null )
			return;

		interactable.InteractHost( pawn );
	}
}
