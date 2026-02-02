namespace UnboxedLife;

public sealed class HealthComponent : Component
{
	[Property] public float MaxHealth { get; set; } = 100f;

	// Host authoritative
	[Sync( SyncFlags.FromHost )] public float Health { get; private set; }
	[Sync( SyncFlags.FromHost )] public bool IsDead { get; private set; }

	[Property] public bool AutoRespawn { get; set; } = true;
	[Property] public float RespawnDelaySeconds { get; set; } = 5f;
	private bool _respawnQueued;

	protected override void OnStart()
	{
		if ( Networking.IsHost )
		{
			Health = MaxHealth;
			IsDead = false;
		}
	}


	public void ResetHealth()
	{
		if ( !Networking.IsHost ) return;

		IsDead = false;
		Health = MaxHealth;
		Components.Get<NeedsComponent>()?.ResetNeeds();
	}

	public void Damage( float amount )
	{
		if ( amount <= 0 ) return;

		// Optional: let clients request damage, host performs it.
		if ( !Networking.IsHost )
		{
			RequestDamage( amount );
			return;
		}

		HostDamage( amount );
	}

	private void HostDamage( float amount )
	{
		if ( IsDead ) return;

		Health = MathF.Max( 0, Health - amount );

		if ( Health <= 0f )
		{
			IsDead = true;

			SpawnRagdollAndRemovePawn(); // <-- add this

			PerformDeath();
		}
	}

	private void SpawnRagdollAndRemovePawn()
	{
		if ( !Networking.IsHost ) return;

		// HealthComponent is on PlayerState, so look up the pawn via PlayerLink
		var pawn = Components.Get<PlayerLink>()?.Player;
		if ( pawn is null || !pawn.IsValid() )
			return;

		var controller = pawn.Components.Get<PlayerController>();
		if ( controller is null )
		{
			// No supported fallback without knowing your pawn renderer setup
			Log.Warning( "[Death] Pawn has no PlayerController; cannot CreateRagdoll()." );
			return;
		}

		// Create ragdoll GO from the controller’s render body
		var ragdoll = controller.CreateRagdoll( $"{pawn.Name} (Ragdoll)" ); // official API :contentReference[oaicite:1]{index=1}
		if ( ragdoll is null || !ragdoll.IsValid() )
			return;

		// Make it networked so all clients see it
		ragdoll.NetworkMode = NetworkMode.Object;
		ragdoll.NetworkSpawn(); // spawns to everyone :contentReference[oaicite:2]{index=2}

		// Optional cleanup
		ragdoll.Components.GetOrCreate<RagdollCleanup>();

		// Remove pawn (your respawn later will create a new one)
		pawn.Destroy();
	}

	// Only runs on the host (per Rpc.Host)
	[Rpc.Host]
	private void RequestDamage( float amount )
	{
		if ( !Networking.IsHost ) return;
		HostDamage( amount );
	}

	private async void PerformDeath()
	{
		if ( !Networking.IsHost ) return;
		if ( !IsDead ) return;
		if ( !AutoRespawn ) return;

		if ( _respawnQueued ) return;
		_respawnQueued = true;

		await GameTask.DelaySeconds( RespawnDelaySeconds );

		var channel = GameObject.Network?.Owner;
		if ( channel is null )
		{
			_respawnQueued = false;
			return;
		}

		var net = Scene.GetAllComponents<UbxNetwork>().FirstOrDefault();
		net?.Respawn( channel );

		Health = MaxHealth;
		IsDead = false;
		Components.Get<NeedsComponent>()?.ResetNeeds();

		_respawnQueued = false;
	}

}
