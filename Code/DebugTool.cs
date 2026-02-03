using Sandbox;
namespace UnboxedLife;

public sealed class DebugTool : Component
{
	[Property] public float DamageAmount { get; set; } = 25f;
	[Property] public GameObject MinerPrefab { get; set; }
	[Property] public float SpawnDistance { get; set; } = 120f;

	protected override void OnUpdate()
	{
		// Run only on the owning client (works whether this component is on pawn or PlayerState)
		if ( GameObject.Network?.IsOwner != true )
			return;

		if ( !Input.Pressed( "F" ) )
			return;

		// Always target the actual pawn (your project marks it with NetworkIdentification)
		var pawn = Scene.GetAllObjects( true )
			.FirstOrDefault( go =>
				go.Network?.IsOwner == true &&
				go.Components.Get<NetworkIdentification>() != null );

		if ( pawn is null )
		{
			Log.Warning( "DebugTool: couldn't find local pawn" );
			return;
		}

		var pc = pawn.Components.Get<Sandbox.PlayerController>();

		var eyePos = pc?.EyePosition ?? pawn.WorldPosition;
		var forward = pc?.EyeAngles.Forward ?? pawn.WorldRotation.Forward;

		var start = eyePos;
		var end = start + forward * 2000f;

		var tr = Scene.Trace
			.FromTo( start, end )
			.IgnoreGameObjectHierarchy( pawn.Root )
			.Run();

		// Visible “laser” + hit marker (world-space)
		DebugOverlay.Line( start, tr.EndPosition, Color.Red, 2f );
		if ( tr.Hit )
			DebugOverlay.Sphere( new Sphere( tr.EndPosition, 4f ), Color.Yellow, 2f, overlay: true );

		// ~2–3 feet in Source units (tune as you like)
		var pos = eyePos + forward * 80f + Vector3.Up * 10f;

		// Face the same direction the player is looking
		var rot = Rotation.LookAt( forward, Vector3.Up );

		RequestSpawnMinerAtRpc( pos, rot );
	}


	[Button]
	public void AddMoney()
	{
		RequestAddMoneyRpc( 500 );
		Log.Info( $"DebugTool: requested to ADD 500 money" );

	}

	[Button]
	public void RemoveMoney()
	{
		RequestRemoveMoneyRpc( 500 );
		Log.Info( $"DebugTool: requested to REMOVE 500 money" );
	}

	[Rpc.Host]
	private void RequestAddMoneyRpc( int amount )
	{
		if ( !Networking.IsHost ) return;

		var owner = GameObject.Network?.Owner;
		if ( owner is null ) return;

		var state = Scene.GetAllObjects( true )
			.FirstOrDefault( go =>
				go.Network?.Owner == owner &&
				go.Components.Get<HealthComponent>() is not null );

		state?.Components.Get<BankAccount>()?.AddMoney( amount );
	}

	[Rpc.Host]
	private void RequestRemoveMoneyRpc( int amount )
	{
		if ( !Networking.IsHost ) return;

		var owner = GameObject.Network?.Owner;
		if ( owner is null ) return;

		var state = Scene.GetAllObjects( true )
			.FirstOrDefault( go =>
				go.Network?.Owner == owner &&
				go.Components.Get<HealthComponent>() is not null );

		state?.Components.Get<BankAccount>()?.RemoveMoney( amount );
	}


	[Rpc.Host]
	private void DamageMeRpc( float amount )
	{
		if ( !Networking.IsHost ) return;

		var owner = GameObject.Network?.Owner;
		if ( owner is null ) return;

		// Find the client-owned PlayerState for this pawn’s owner (same idea as UbxNetwork.GetPlayerStateFor)
		var state = Scene.GetAllObjects( true )
			.FirstOrDefault( go =>
				go.Network?.Owner == owner &&
				go.Components.Get<HealthComponent>() is not null );

		state?.Components.Get<HealthComponent>()?.Damage( amount );
	}

	[Rpc.Host]
	private void RequestSpawnMinerAtRpc( Vector3 pos, Rotation rot )
	{
		if ( !Networking.IsHost )
			return;

		if ( !MinerPrefab.IsValid() )
		{
			Log.Warning( "DebugTool: MinerPrefab not set" );
			return;
		}

		var owner = GameObject.Network?.Owner;
		if ( owner is null )
			return;

		var miner = MinerPrefab.Clone( pos, rot );
		miner.NetworkMode = NetworkMode.Object;
		miner.NetworkSpawn( owner );

		Log.Info( $"Spawned miner at {pos}" );
	}


}
