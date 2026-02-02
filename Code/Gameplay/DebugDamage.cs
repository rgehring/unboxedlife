namespace UnboxedLife;

public sealed class DebugDamage : Component
{
	[Property] public float DamageAmount { get; set; } = 25f;

	protected override void OnUpdate()
	{
		// Only the simulating side should read input.
		// For client-owned pawns: the owning client is NOT a proxy; host/other clients ARE proxies.
		if ( GameObject.Network?.IsProxy ?? true )
			return;

		if ( Input.Pressed( "attack1" ) )
		{
			DamageMeRpc( DamageAmount );
			Log.Info( $"DebugDamage: requested {DamageAmount} damage" );
		}
	}

	[Rpc.Host]
	private void DamageMeRpc( float amount )
	{
		if ( !Networking.IsHost ) return;

		var owner = GameObject.Network?.Owner;
		if ( owner is null ) return;

		// Find the client-owned PlayerState for this pawn’s owner (same idea as UbxNetwork.GetPlayerStateFor)
		var state = Scene.GetAllObjects( true )
			.FirstOrDefault( go =>
				go.Network?.Owner == owner &&
				go.Components.Get<HealthComponent>() is not null );

		state?.Components.Get<HealthComponent>()?.Damage( amount );
	}
}
