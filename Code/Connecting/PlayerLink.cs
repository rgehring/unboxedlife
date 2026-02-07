namespace UnboxedLife;

public sealed class PlayerLink : Component
{
	// Host writes these links; everyone reads them.
	[Sync( SyncFlags.FromHost )]
	public GameObject Player { get; private set; }

	[Sync( SyncFlags.FromHost )]
	public GameObject State { get; private set; }

	public void SetPlayer( GameObject player )
	{
		if ( !Networking.IsHost ) return;
		Player = player;
	}

	public void SetState( GameObject state )
	{
		if ( !Networking.IsHost ) return;
		State = state;
	}
}
