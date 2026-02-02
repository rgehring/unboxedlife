namespace UnboxedLife;

public sealed class DebugTool : Component
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
			AddMoney();
			Log.Info( $"DebugTool: Left click pressed" );
			//DamageMeRpc( DamageAmount );
			//Log.Info( $"DebugDamage: requested {DamageAmount} damage" );
		}
	}

	[Button]
	public void AddMoney()
	{
		RequestAddMoneyRpc( 500 );
		Log.Info( $"DebugTool: requested to ADD 500 money" );

	}

	[Button]
	public void RemoveMoney()
	{
		RequestRemoveMoneyRpc( 500 );
		Log.Info( $"DebugTool: requested to REMOVE 500 money" );
	}

	[Rpc.Host]
	private void RequestAddMoneyRpc( int amount )
	{
		if ( !Networking.IsHost ) return;

		var owner = GameObject.Network?.Owner;
		if ( owner is null ) return;

		var state = Scene.GetAllObjects( true )
			.FirstOrDefault( go =>
				go.Network?.Owner == owner &&
				go.Components.Get<HealthComponent>() is not null );

		state?.Components.Get<BankAccount>()?.AddMoney( amount );
	}

	[Rpc.Host]
	private void RequestRemoveMoneyRpc( int amount )
	{
		if ( !Networking.IsHost ) return;

		var owner = GameObject.Network?.Owner;
		if ( owner is null ) return;

		var state = Scene.GetAllObjects( true )
			.FirstOrDefault( go =>
				go.Network?.Owner == owner &&
				go.Components.Get<HealthComponent>() is not null );

		state?.Components.Get<BankAccount>()?.RemoveMoney( amount );
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
