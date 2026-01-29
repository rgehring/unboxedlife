using Sandbox;
using System;

namespace UnboxedLife;

public sealed class Interactor : Component
{
	[Property] public float TraceDistance { get; set; } = 200f;

	// Basic “eyes” height for a capsule-ish player
	[Property] public float EyeHeight { get; set; } = 64f;

	private Interactable _hovered;
	private Guid? _hoveredId;
	private float _useHeldTime;


	protected override void OnUpdate()
	{
		//if ( Time.Now % 1f < Time.Delta )
		//	Log.Info( "Interactor tick" );

		//if ( Input.Pressed( "attack1" ) )
		//	Log.Info( "attack1 pressed (Interactor)" );

		//if ( Input.Pressed( "use" ) )
		//	Log.Info( "use pressed (Interactor)" );

		// 1) Find what we're looking at
		_hovered = TraceForInteractable();

		// Optional: cheap debug indicator (console-only)
		//if ( _hovered != null && Time.Now % 1f < Time.Delta )
		//	Log.Info( $"Looking at: {_hovered.GameObject.Name} ({_hovered.Prompt})" );

		// 2) Use input (E is usually "use" in s&box setups; if it doesn't fire, we’ll swap)
		if ( Input.Down( "use" ) && _hovered != null )
		{
			_useHeldTime += Time.Delta;

			// Only request once per second-ish; the node will enforce exact timing
			if ( _useHeldTime >= 0.1f )
			{
				_useHeldTime = 0f;
				TryInteract( _hovered );
			}
		}
		else
		{
			_useHeldTime = 0f;
		}

	}

	private Interactable TraceForInteractable()
	{
		// Use the active camera/view as the source of truth
		var camPos = Scene.Camera.WorldPosition;
		var camRot = Scene.Camera.WorldRotation;

		var start = camPos;
		var end = start + camRot.Forward * TraceDistance;

		var hit = Scene.Trace.Ray( start, end )
			.IgnoreGameObject( GameObject )
			.Run();

		if ( !hit.Hit )
		{
			_hoveredId = null;
			return null;
		}

		var interactable =
			hit.GameObject.Components.Get<Interactable>() ??
			hit.GameObject.Components.GetInAncestorsOrSelf<Interactable>();

		_hoveredId = interactable?.GameObject?.Id;
		return interactable;
	}




	private void TryInteract( Interactable target )
	{
		if ( Networking.IsHost )
		{
			target.InteractHost( GameObject );
			return;
		}

		if ( _hoveredId is null )
			return;

		RequestInteract( _hoveredId.Value );
	}

	[Rpc.Host]
	private void RequestInteract( Guid targetId )
	{
		// Find the target object in the scene by Id
		var targetGo = Scene.Directory.FindByGuid( targetId );
		if ( targetGo is null )
			return;

		var interactable = targetGo.Components.GetInAncestorsOrSelf<Interactable>();
		if ( interactable is null )
			return;

		interactable.InteractHost( GameObject );
	}

}
