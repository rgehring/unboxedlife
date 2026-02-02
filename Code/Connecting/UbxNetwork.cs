namespace UnboxedLife;

/// <summary>
/// Creates a networked game lobby and assigns player prefabs to connected clients.
/// </summary>
[Title( "Ubx Custom Network Helper" )]
[Category( "Ubx Networking" )]
[Icon( "electrical_services" )]

public sealed class UbxNetwork : Component, Component.INetworkListener
{	
	/// <summary>
	/// A list of points to choose from randomly to spawn the player in. If not set, we'll spawn at the
	/// location of the NetworkHelper object.
	/// </summary>
	[Property] public List<GameObject> SpawnPoints { get; set; }

	[Property] public bool StartServer { get; set; } = true;
	[Property] public GameObject PlayerPrefab { get; set; }
	private readonly Dictionary<Connection, GameObject> _pawnByConn = new();


	protected override void OnStart()
	{
	}

	protected override async Task OnLoad()
	{
		if ( Scene.IsEditor )
			return;

		if ( StartServer && !Networking.IsActive )
		{
			LoadingScreen.Title = "Creating Lobby";
			await Task.DelayRealtimeSeconds( 0.1f );
			Networking.CreateLobby( new() );
		}
	}

	/// <summary>
	/// A client is fully connected to the server. This is called on the host.
	/// </summary>
	public void OnActive( Connection channel )
	{
		if ( !Networking.IsHost ) return;

		Log.Info( $"[UbxNetwork.cs]Player '{channel.DisplayName}' has joined the game" );

		var pawn = SpawnPlayerFor( channel );
		if ( pawn is null )
			return;

		var state = GetPlayerStateFor( channel );
		if ( state is not null )
		{
			LinkPawnAndState( pawn, state );
		}
		// else: PlayerStateSpawner will create it shortly and link from its side
	}


	private GameObject SpawnPlayerFor( Connection channel )
	{
		if ( !Networking.IsHost )
			return null;

		if ( !PlayerPrefab.IsValid() )
			return null;

		var startLocation = FindSpawnLocation().WithScale( 1 );

		var player = PlayerPrefab.Clone( startLocation, name: $"[UbxNetwork.cs]Player - {channel.DisplayName}" );
		player.NetworkMode = NetworkMode.Object;
		player.NetworkSpawn( channel );
		Log.Info( $"[Spawn] pawn id={player.Id} name={player.Name} owner={player.Network?.OwnerId}" );

		_pawnByConn[channel] = player;
		return player;
	}


	/// <summary>
	/// Host only - Respawn the player for the given connection
	/// </summary>
	public void Respawn( Connection channel )
	{
		if ( !Networking.IsHost ) return;

		var state = GetPlayerStateFor( channel );
		if ( state is null )
		{
			Log.Warning( $"[Respawn] No PlayerState found for {channel.DisplayName} ({channel.SteamId})" );
			return;
		}

		if ( _pawnByConn.TryGetValue( channel, out var oldPawn ) )
		{
			if ( oldPawn.IsValid() )
				Log.Info( $"[Respawn] destroying pawn id={oldPawn.Id} name={oldPawn.Name} owner={oldPawn.Network?.OwnerId}" );
			
			oldPawn.Destroy();

			_pawnByConn.Remove( channel ); // remove stale ref either way
		}

		var newPawn = SpawnPlayerFor( channel );
		if ( newPawn is null )
			return;

		LinkPawnAndState( newPawn, state );
	}


	private GameObject GetPlayerStateFor( Connection channel )
	{
		return Scene.GetAllObjects( true )
			.FirstOrDefault( go =>
				go.Network?.Owner == channel &&
				go.Components.Get<HealthComponent>() is not null );
	}

	private void LinkPawnAndState( GameObject pawn, GameObject state )
	{
		if ( pawn is null || state is null )
			return;

		pawn.Components.Get<PlayerLink>()?.SetState( state );
		state.Components.Get<PlayerLink>()?.SetPlayer( pawn );
	}


	/// <summary>
	/// Find the most appropriate place to respawn
	/// </summary>
	Transform FindSpawnLocation()
	{
		//
		// If they have spawn point set then use those
		//
		if ( SpawnPoints is not null && SpawnPoints.Count > 0 )
		{
			return Random.Shared.FromList( SpawnPoints, default ).WorldTransform;
		}

		//
		// If we have any SpawnPoint components in the scene, then use those
		//
		var spawnPoints = Scene.GetAllComponents<SpawnPoint>().ToArray();
		if ( spawnPoints.Length > 0 )
		{
			return Random.Shared.FromArray( spawnPoints ).WorldTransform;
		}

		//
		// Failing that, spawn where we are
		//
		return WorldTransform;
	}
}
