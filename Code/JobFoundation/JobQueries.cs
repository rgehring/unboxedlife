using Sandbox;

namespace UnboxedLife;

public static class JobQueries
{
	public static JobComponent GetJob( GameObject pawnOrState )
		=> pawnOrState?.Components.Get<JobComponent>( FindMode.InSelf | FindMode.InAncestors );

	public static bool HasJob( GameObject pawnOrState, JobId job )
		=> GetJob( pawnOrState )?.CurrentJob == job;
}
