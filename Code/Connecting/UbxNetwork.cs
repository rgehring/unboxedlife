namespace UnboxedLife;

/// <summary>
/// Creates a networked game lobby and assigns player prefabs to connected clients.
/// </summary>
[Title( "Ubx Custom Network Helper" )]
[Category( "Ubx Networking" )]
[Icon( "electrical_services" )]

public sealed class UbxNetwork : Component, Component.INetworkListener
{	
	[Property] public List<GameObject> SpawnPoints { get; set; }
	[Property] public bool StartServer { get; set; } = true;
	[Property] public GameObject CitizenPrefab { get; set; } // what players ALWAYS FIRST spawn in as when joining the server
	[Property] public GameObject PolicePrefab { get; set; }
	[Property] public GameObject ThiefPrefab { get; set; }
	[Property] public GameObject PlayerStatePrefab { get; set; } // a persistent object that holds player-specific data (like money, job, etc) that exists independently of the player's pawn and can persist across respawns

	private readonly Dictionary<Connection, GameObject> _stateByConn = new();
	private readonly Dictionary<Connection, GameObject> _pawnByConn = new();
	[Property] public List<ShopItemDef> ShopItems { get; set; } = new();
	private ShopItemDef FindShopItem( string id )
		=> ShopItems?.FirstOrDefault( x => x != null && x.Id == id );


	public void OnDisconnected( Connection channel )
	{
		if ( !Networking.IsHost ) return;
		if ( channel is null ) return;

		// 1) Pawn
		if ( _pawnByConn.TryGetValue( channel, out var pawn ) )
		{
			_pawnByConn.Remove( channel );

			if ( pawn.IsValid() )
				pawn.Destroy();
		}

		// 2) State
		if ( _stateByConn.TryGetValue( channel, out var state ) )
		{
			_stateByConn.Remove( channel );

			if ( state.IsValid() )
				state.Destroy();
		}

		Log.Info( $"[UbxNetwork] OnDisconnected cleanup: conn={channel.DisplayName} pawns={_pawnByConn.Count} states={_stateByConn.Count}" );
	}


	private GameObject EnsurePlayerStateFor( Connection channel )
	{
		if ( _stateByConn.TryGetValue( channel, out var cached ) && cached.IsValid() )
			return cached;

		// try existing (linked/search)
		var existing = GetPlayerStateFor( channel );
		if ( existing is not null && existing.IsValid() )
		{
			_stateByConn[channel] = existing;
			return existing;
		}

		if ( !PlayerStatePrefab.IsValid() )
		{
			Log.Warning( $"[UbxNetwork] PlayerStatePrefab not set - cannot create PlayerState for {channel.DisplayName}" );
			return null;
		}

		var state = PlayerStatePrefab.Clone();
		state.NetworkMode = NetworkMode.Object;

		// IMPORTANT: assign ownership to that player's connection
		state.NetworkSpawn( channel ); // owner will be the connection provided

		_stateByConn[channel] = state;
		Log.Info( $"[UbxNetwork] State spawned: conn={channel.DisplayName} stateId={state.Id} owner={state.Network?.OwnerId}" );

		return state;

	}

	private BankAccount GetBankAccountFor( Connection channel )
	{
		var state = EnsurePlayerStateFor( channel );
		return state?.Components.Get<BankAccount>();
	}



	[Rpc.Host]
	public void RequestBuyItemRpc( string itemId, Vector3 pos, Rotation rot )
	{
		if ( !Networking.IsHost )
			return;

		var buyer = Rpc.Caller;
		if ( buyer is null )
			return;

		var item = FindShopItem( itemId );
		if ( item is null )
		{
			Log.Warning( $"[Shop] Unknown item '{itemId}'" );
			return;
		}

		if ( !item.Prefab.IsValid() )
		{
			Log.Warning( $"[Shop] Prefab not set for '{itemId}'" );
			return;
		}

		// Optional anti-abuse: don't allow spawning very far from the buyer's pawn
		if ( _pawnByConn.TryGetValue( buyer, out var pawn ) && pawn.IsValid() )
		{
			if ( (pos - pawn.WorldPosition).Length > 500f )
			{
				Log.Warning( $"[Shop] rejected spawn too far for '{itemId}'" );
				return;
			}
		}

		var bank = GetBankAccountFor( buyer );
		if ( bank is null )
			return;

		if ( !bank.TrySpend( item.Price ) )
			return;

		var go = item.Prefab.Clone( pos, rot );
		go.NetworkMode = NetworkMode.Object;
		go.NetworkSpawn( buyer );
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

		Log.Info( $"'{channel.DisplayName}' has joined the game" );

		var state = EnsurePlayerStateFor( channel );
		if ( state is null ) return;

		var pawn = SpawnPlayerFor( channel );
		if ( pawn is null ) return;

		LinkPawnAndState( pawn, state );
		Log.Info( $"[UbxNetwork] OnActive done: conn={channel.DisplayName} pawns={_pawnByConn.Count} states={_stateByConn.Count}" );

	}

	private GameObject SpawnPlayerFor( Connection channel )
	{
		if ( !Networking.IsHost )
			return null;

		if ( _pawnByConn.TryGetValue( channel, out var existing ) && existing.IsValid() )
		{
			Log.Warning( $"[Spawn] prevented double-spawn for {channel.DisplayName}. Existing pawn={existing.Id}" );
			return existing;
		}

		var state = EnsurePlayerStateFor( channel );
		var job = state?.Components.Get<JobComponent>()?.CurrentJob ?? JobId.Citizen;

		var prefab = job switch
		{
			JobId.Police => PolicePrefab,
			JobId.Thief => ThiefPrefab,
			_ => CitizenPrefab
		};

		if ( !prefab.IsValid() )
			return null;

		var startLocation = FindSpawnLocation().WithScale( 1 );

		var player = prefab.Clone( startLocation, name: $"PrefabType:{prefab} | DisplayName:{channel.DisplayName}" );
		player.NetworkMode = NetworkMode.Object;
		player.NetworkSpawn( channel );

		Log.Info( $"[Spawn] pawn id={player.Id} name={player.Name} owner={player.Network?.OwnerId} job={job}" );

		_pawnByConn[channel] = player;
		Log.Info( $"[UbxNetwork] Pawn spawned: conn={channel.DisplayName} pawnId={player.Id} owner={player.Network?.OwnerId}" );

		return player;
	}

	public void SetJobAndRespawn( Connection channel, JobId job )
	{
		if ( !Networking.IsHost ) return;

		var state = EnsurePlayerStateFor( channel );
		if ( state is null ) return;

		var jobComp = state.Components.Get<JobComponent>();
		if ( jobComp is null ) return;
		if ( jobComp.CurrentJob == job ) return;

		jobComp.SetJobHost( job );
		Respawn( channel );
	}

	/// <summary>
	/// Host only - Respawn the player for the given connection
	/// </summary>
	public void Respawn( Connection channel )
	{
		if ( !Networking.IsHost ) return;

		var state = EnsurePlayerStateFor( channel );
		if ( state is null )
		{
			Log.Warning( $"[Respawn] No PlayerState for {channel.DisplayName} ({channel.SteamId})" );
			return;
		}

		_pawnByConn.TryGetValue( channel, out var oldPawn );

		if ( oldPawn is not null && oldPawn.IsValid() )
		{
			Log.Info( $"[Respawn] destroying pawn id={oldPawn.Id} name={oldPawn.Name} owner={oldPawn.Network?.OwnerId}" );
			oldPawn.Destroy();
		}

		_pawnByConn.Remove( channel );

		var newPawn = SpawnPlayerFor( channel );
		if ( newPawn is null ) return;

		LinkPawnAndState( newPawn, state );
		Log.Info( $"[UbxNetwork] Respawn done: conn={channel.DisplayName} pawns={_pawnByConn.Count} states={_stateByConn.Count}" );

	}

	private GameObject GetPlayerStateFor( Connection channel, GameObject knownPawn = null )
	{
		// Best: if we have a pawn, use the link
		var linked = knownPawn?.Components.Get<PlayerLink>()?.State;
		if ( linked is not null && linked.IsValid() )
			return linked;

		// Next best: cache
		if ( _stateByConn.TryGetValue( channel, out var cached ) && cached.IsValid() )
			return cached;

		// Fallback: scene search
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
