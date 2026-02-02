namespace UnboxedLife;

public sealed class PlayerStateSpawner : Component, Component.INetworkListener
{
	[Property] public GameObject PlayerStatePrefab { get; set; }

	public async void OnActive( Connection c )
	{
		if ( !Networking.IsHost ) return;

		GameObject player = null;
		for ( int i = 0; i < 30 && player is null; i++ ) // ~0.5s at 60fps
		{
			player = Scene.GetAllObjects( true )
				.FirstOrDefault( go => go.Network?.Owner == c && go.Components.Get<NetworkIdentification>() is not null );

			if ( player is null ) await GameTask.Yield();
		}

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

