namespace UnboxedLife;

public sealed class DebugDamage : Component
{
	[Property] public float DamageAmount { get; set; } = 25f;

	protected override void OnUpdate()
	{
		if ( Input.Pressed( "attack1" ) )
		{
			DamageMeRpc( DamageAmount );
			Log.Info( "DebugDamage: attack1 pressed" );
			Log.Info( $"DebugDamage: attempted damage {DamageAmount}" );

		}
	}

	[Rpc.Host]
	void DamageMeRpc( float amount )
	{
		// Find the server-owned state that points to THIS pawn
		var state = Scene.GetAllObjects( true )
			.FirstOrDefault( go => go.Components.Get<PlayerLink>()?.Player == GameObject );

		state?.Components.Get<HealthComponent>()?.Damage( amount );
	}
}
