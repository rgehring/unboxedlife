namespace UnboxedLife;

[Title( "Player Link" ),
Description( "(PLAYER STATE) Links a player GameObject to their PlayerState " +
			 "(or similar) for easy access across the codebase." )]
public sealed class PlayerLink : Component
{

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
