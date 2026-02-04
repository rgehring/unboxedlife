using Sandbox;
using Sandbox.Citizen;

namespace UnboxedLife;

public sealed class CombatAnimator : Component
{
	[Property] public CitizenAnimationHelper AnimHelper { get; set; }
	[Property] public Sandbox.PlayerController Controller { get; set; }

	// How long to hold the punch pose
	[Property] public float PunchPoseSeconds { get; set; } = 0.35f;

	private TimeSince _sincePunch;

	protected override void OnStart()
	{
		AnimHelper ??= Components.Get<CitizenAnimationHelper>( FindMode.InSelf | FindMode.InChildren );
		Controller ??= Components.Get<Sandbox.PlayerController>( FindMode.InSelf | FindMode.InAncestors );
	}

	protected override void OnUpdate()
	{
		if ( AnimHelper is null || Controller is null )
			return;

		// Drive locomotion every frame
		AnimHelper.WithVelocity( Controller.Velocity );
		AnimHelper.IsGrounded = Controller.IsOnGround;
		AnimHelper.IsSitting = false;

		var model = AnimHelper.Target as SkinnedModelRenderer;
		if ( model is null )
			return;

		// Hold punch pose briefly after triggering
		if ( _sincePunch < PunchPoseSeconds )
		{
			AnimHelper.HoldType = CitizenAnimationHelper.HoldTypes.Punch;
			model.Set( "holdtype", (int)CitizenAnimationHelper.HoldTypes.Punch );
		}
		else
		{
			AnimHelper.HoldType = CitizenAnimationHelper.HoldTypes.None;
			// reset pose to default
			model.Set( "holdtype_pose", 0f );
		}
	}

	/// <summary>Call to play punch animation immediately.</summary>
	public void TriggerPunch()
	{
		_sincePunch = 0f;

		if ( AnimHelper is null )
			return;

		var model = AnimHelper.Target as SkinnedModelRenderer;
		if ( model is null )
			return;

		// These parameters must exist in the citizen animgraph you’re using.
		model.Set( "b_attack", true );
		model.Set( "holdtype", (int)CitizenAnimationHelper.HoldTypes.Punch );
	}

	/// <summary>Call when this pawn is damaged to flinch.</summary>
	public void TriggerHitReact()
	{
		var model = AnimHelper?.Target as SkinnedModelRenderer;
		if ( model is null )
			return;

		model.Set( "hit", true );
	}
}
