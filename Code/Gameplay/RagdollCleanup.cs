namespace UnboxedLife;

public sealed class RagdollCleanup : Component
{
	[Property] public float LifetimeSeconds { get; set; } = 5f;

	protected override async void OnStart()
	{
		await GameTask.DelaySeconds( LifetimeSeconds );

		if ( GameObject.IsValid() )
			GameObject.Destroy();
	}
}
