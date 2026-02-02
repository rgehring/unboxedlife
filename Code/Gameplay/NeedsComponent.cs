namespace UnboxedLife;

public sealed class NeedsComponent : Component
{
	[Property] public float MaxHunger { get; set; } = 100f;
	[Property] public float MaxThirst { get; set; } = 100f;

	// Drain per second
	[Property] public float HungerDrainPerSecond { get; set; } = 0.10f;
	[Property] public float ThirstDrainPerSecond { get; set; } = 0.20f;

	// Damage per second when empty
	[Property] public float StarveDamagePerSecond { get; set; } = 2.0f;

	[Sync( SyncFlags.FromHost )] public float Hunger { get; private set; }
	[Sync( SyncFlags.FromHost )] public float Thirst { get; private set; }


	[Property, ReadOnly] public float HungerReadout => Hunger;
	[Property, ReadOnly] public float ThirstReadout => Thirst;

	protected override void OnStart()
	{
		if ( Networking.IsHost )
		{
			Hunger = MaxHunger;
			Thirst = MaxThirst;
		}
	}

	protected override void OnUpdate()
	{
		if ( !Networking.IsHost )
			return;

		var dt = Time.Delta;

		Hunger = MathF.Max( 0, Hunger - HungerDrainPerSecond * dt );
		Thirst = MathF.Max( 0, Thirst - ThirstDrainPerSecond * dt );

		if ( Hunger <= 0 || Thirst <= 0 )
		{
			var health = Components.Get<HealthComponent>();
			health?.Damage( StarveDamagePerSecond * dt );
		}

#if DEBUG // for needs testing
//		if ( Time.Now % 1f < Time.Delta )
//			Log.Info( $"Needs: Hunger={Hunger:0.0} Thirst={Thirst:0.0}" );
#endif

	}

	public void ResetNeeds()
	{
		if ( !Networking.IsHost ) return;
		Hunger = MaxHunger;
		Thirst = MaxThirst;
	}
}
