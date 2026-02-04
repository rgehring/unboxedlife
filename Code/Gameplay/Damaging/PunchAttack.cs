using Sandbox;
using System.Linq;
namespace UnboxedLife;

public sealed class PunchAttack : Component
{
	[Property] public float Range { get; set; } = 80f;
	[Property] public float Radius { get; set; } = 12f;
	[Property] public float Damage { get; set; } = 10f;
	[Property] public float CooldownSeconds { get; set; } = 0.35f;

	// reuse what you already have in Interactor
	[Property] public float EyeHeight { get; set; } = 64f;

	private TimeSince _timeSinceLastPunch;

	protected override void OnUpdate()
	{
		// only the owning client should read input
		if ( GameObject.Network?.IsOwner != true )
			return;

		if ( _timeSinceLastPunch < CooldownSeconds )
			return;

		// default S&box binding is usually "attack1"
		if ( !Input.Pressed( "attack1" ) )
			return;
		Log.Info( $"TryPunchRpc from {Rpc.Caller}" );

		_timeSinceLastPunch = 0f;
		TryPunchRpc();
	}

	[Rpc.Host]
	private void TryPunchRpc()
	{
		if ( !Networking.IsHost )
			return;

		// Ensure caller owns this PlayerState object
		var owner = GameObject.Network?.Owner;
		if ( owner is null || owner != Rpc.Caller )
			return;

		// Use the host-maintained link (state -> current pawn)
		var attackerPawn = Components.Get<PlayerLink>()?.Player;
		if ( attackerPawn is null || !attackerPawn.IsValid )
			return;

		var pc = attackerPawn.Components.Get<Sandbox.PlayerController>();
		if ( pc is null )
			return;

		var tr = TraceUtil.TraceFromEyes(
			attackerPawn,
			pc,
			EyeHeight,
			Range,
			radius: Radius,
			withAnyTag: "player",
			ignorePawnHierarchy: true
		);

		if ( !tr.Hit || tr.GameObject is null )
			return;

		var victimPawnRoot = tr.GameObject.Root;

		// Only allow real pawns to be victims
		if ( victimPawnRoot.Components.Get<Sandbox.PlayerController>() is null )
			return;

		if ( victimPawnRoot == attackerPawn.Root )
			return;

		var victimState = victimPawnRoot.Components.Get<PlayerLink>()?.State;
		if ( victimState is null )
			return;

		victimState.Components.Get<HealthComponent>()?.Damage( Damage );

		Log.Info( $"PUNCH HIT: {attackerPawn.Name} -> {victimPawnRoot.Name} for {Damage}" );
	}

}
