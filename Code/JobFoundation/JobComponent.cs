using Sandbox;

namespace UnboxedLife;

public enum JobId
{
	Citizen = 0,
	Police = 1,
	Thief = 2
}

public sealed class JobComponent : Component
{
	[Sync( SyncFlags.FromHost )] public JobId CurrentJob { get; private set; } = JobId.Citizen;

	public void SetJobHost( JobId job )
	{
		if ( !Networking.IsHost ) return;
		CurrentJob = job;
	}
}
