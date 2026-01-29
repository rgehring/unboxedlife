using Sandbox;

namespace UnboxedLife;

public sealed class DebugDamage : Component
{
	[Property] public float DamageAmount { get; set; } = 25f;

	protected override void OnUpdate()
	{
		// Left mouse click (default action)
		if ( Input.Pressed( "attack1" ) )
		{
			Log.Info( "DebugDamage: attack1 pressed" );

			var health = Components.Get<HealthComponent>();
			health?.Damage( DamageAmount );

			Log.Info( $"DebugDamage: attempted damage {DamageAmount}" );
		}
	}
}
