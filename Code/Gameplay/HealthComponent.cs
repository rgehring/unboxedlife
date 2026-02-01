namespace UnboxedLife;

public sealed class HealthComponent : Component
{
	[Property] public float MaxHealth { get; set; } = 100f;

	// Networked so all clients can read it, but only host should change it.
	[Sync( SyncFlags.FromHost )] public float Health { get; private set; }

	[Property] public bool AutoRespawn { get; set; } = true;
	[Property] public float RespawnDelaySeconds { get; set; } = 5f;

	protected override void OnStart()
	{
		if ( Networking.IsHost )
		{
			Health = MaxHealth;
		}
	}

	public void ResetHealth()
	{
		if ( !Networking.IsHost ) return;
		Health = MaxHealth;
	}

	public void Damage( float amount )
	{
		if ( !Networking.IsHost ) return;
		if ( amount <= 0 ) return;

		Health = MathF.Max( 0, Health - amount );

		if ( Health <= 0 )
			OnDied();
	}

	private async void OnDied()
	{
		if ( !Networking.IsHost ) return;

		var pawn = Components.Get<PlayerLink>()?.Player ?? GameObject; // state -> pawn
		pawn.Enabled = false;

		if ( !AutoRespawn ) return;

		await GameTask.DelaySeconds( RespawnDelaySeconds );

		Health = MaxHealth;
		Components.Get<NeedsComponent>()?.ResetNeeds();

		pawn.Enabled = true;
		pawn.WorldPosition += Vector3.Up * 64f;
	}

}
