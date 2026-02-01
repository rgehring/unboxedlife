namespace UnboxedLife;

public sealed class PlayerStateSpawner : Component, Component.INetworkListener
{
	[Property] public GameObject PlayerStatePrefab { get; set; }

	public void OnActive( Connection c )
	{
		if ( !Networking.IsHost ) return;

		// Find the player pawn owned by this connection
		var player = Scene.GetAllObjects( true )
			.FirstOrDefault( go => go.Network?.Owner == c );

		if ( player is null || !PlayerStatePrefab.IsValid() )
			return;

		// Spawn PlayerState for this connection
		var state = PlayerStatePrefab.Clone();

		state.NetworkMode = NetworkMode.Object;
		state.NetworkSpawn( c ); // IMPORTANT: client-owned state

		Log.Info(
			$"[HOST SPAWN] State={state.Id} Active={state.Network.Active} " +
			$"Mode={state.NetworkMode} Owner={state.Network.OwnerId}"
		);



		// Link both ways
		player.Components.Get<PlayerLink>()?.SetState( state );
		state.Components.Get<PlayerLink>()?.SetPlayer( player );
	}
}

