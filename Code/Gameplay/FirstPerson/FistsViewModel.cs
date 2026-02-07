using Sandbox;

namespace UnboxedLife;

public sealed class FistsViewModel : Component
{
	[Property] public PlayerController Controller { get; set; }
	[Property] public SkinnedModelRenderer ViewModelRenderer { get; set; }

	// optional tweak
	[Property] public Vector3 LocalOffset { get; set; } = new( 12, 0, -12 );

	protected override void OnStart()
	{
		Controller ??= Components.Get<PlayerController>( FindMode.InSelf | FindMode.InAncestors );

		// Do NOT auto-find. Require it to be set in the inspector.
		if ( ViewModelRenderer is null )
		{
			Log.Warning( $"{nameof( FistsViewModel )}: ViewModelRenderer is not assigned." );
			return;
		}

		if ( GameObject.Network?.IsOwner != true )
		{
			if ( ViewModelRenderer.SceneObject is not null )
				ViewModelRenderer.SceneObject.RenderingEnabled = false;
		}
	}


	protected override void OnUpdate()
	{
		if ( Controller is null || ViewModelRenderer?.SceneObject is null )
			return;

		// Only the local player's controller should drive this.
		if ( Controller.IsProxy )
		{
			ViewModelRenderer.SceneObject.RenderingEnabled = false;
			return;
		}

		// TEMP DEBUG: always render (ignore third-person for now)
		ViewModelRenderer.SceneObject.RenderingEnabled = true;

		var pos = Controller.EyePosition;
		var rot = Rotation.From( Controller.EyeAngles );

		ViewModelRenderer.GameObject.WorldRotation = rot;
		ViewModelRenderer.GameObject.WorldPosition = pos + (rot * LocalOffset);
	}



	public void TriggerPunch( bool hit = false )
	{
		if ( GameObject.Network?.IsOwner != true )
			return;

		var sm = ViewModelRenderer?.SceneModel;
		if ( sm is null )
			return;

		// First-person weapons doc: b_attack throws a punch; b_attack_hit can vary hit/miss. :contentReference[oaicite:6]{index=6}
		// SceneModel.SetAnimParameter is the supported API. :contentReference[oaicite:7]{index=7}
		sm.SetAnimParameter( "b_attack", true );
		sm.SetAnimParameter( "b_attack_hit", hit );
	}
}
