using Sandbox;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace UnboxedLife;

public sealed class MiningNode : Interactable
{
	[Property] public string NodeName { get; set; } = "Rock";
	[Property] public int MaxHits { get; set; } = 5;
	[Property] public float RespawnSeconds { get; set; } = 30f;
	[Property] public float SecondsPerHit { get; set; } = 1.0f;

	// Total stone that would have been granted per node (we now distribute this via chunks)
	[Property] public int StonePerNode { get; set; } = 10;

	// Chunk behavior (Option 2: spawn stationary chunks near/under the node)
	[Property] public int MinChunks { get; set; } = 3;
	[Property] public int MaxChunks { get; set; } = 5;
	[Property] public float ChunkScatterRadius { get; set; } = 25f;
	[Property] public float ChunkDownOffset { get; set; } = 10f; // spawn slightly "under" the node
	[Property] public float ChunkScale { get; set; } = 0.35f;

	[Property] public float ChunkColliderRadius { get; set; } = 9f;

	private int _hitsRemaining;
	private bool _depleted;
	private float _nextHitTime;
	private Model _chunkModel;

	// Keep track so we can clean them up on respawn
	private readonly List<GameObject> _spawnedChunks = new();

	protected override void OnStart()
	{
		_hitsRemaining = MaxHits;
		_depleted = false;
		Prompt = $"Mine {NodeName}";

		var sourceMr = GameObject.Components.Get<ModelRenderer>( FindMode.EverythingInSelfAndChildren );
		_chunkModel = sourceMr?.Model;

		if ( _chunkModel is null )
			Log.Warning( $"MINING: No source model found on {GameObject.Name} (chunks will be invisible)." );
	}

	public override void Interact( GameObject interactor )
	{
		if ( Time.Now < _nextHitTime )
			return;

		_nextHitTime = Time.Now + SecondsPerHit;

		if ( _depleted )
			return;

		_hitsRemaining--;
		Log.Info( $"MINING: {interactor.Name} hit {GameObject.Name} ({_hitsRemaining}/{MaxHits})" );

		if ( _hitsRemaining > 0 )
			return;

		_depleted = true;

		// Hide/disable the main node
		SetNodeEnabled( false );

		// Spawn pickup chunks (host-authoritative; InteractHost ensures we're host)
		SpawnChunks();

		Log.Info( $"MINING: {GameObject.Name} depleted. Respawn in {RespawnSeconds}s" );
		_ = RespawnLater();
	}

	private void SpawnChunks()
	{
		// Safety: only spawn on host
		if ( !Networking.IsHost )
			return;

		// Clean up any previous leftovers (in case)
		CleanupChunks();

		int chunkCount = Random.Shared.Next( MinChunks, MaxChunks + 1 );

		// We'll try to reuse the node's model as the chunk model (simple starter approach)
		var sourceMr = GameObject.Components.Get<ModelRenderer>( FindMode.EverythingInSelfAndChildren );
		var sourceModel = sourceMr?.Model;

		// Split StonePerNode across chunks (random-ish, but sums to StonePerNode)
		var amounts = SplitTotalIntoRandomParts( StonePerNode, chunkCount );

		for ( int i = 0; i < chunkCount; i++ )
		{
			var chunkGo = new GameObject( true, $"RockChunk_{i + 1}" );
			chunkGo.WorldPosition =
				GameObject.WorldPosition
				+ Vector3.Random.WithZ( 0 ).Normal * Random.Shared.NextSingle() * ChunkScatterRadius
				- Vector3.Up * ChunkDownOffset;

			chunkGo.WorldRotation = Rotation.FromYaw( Random.Shared.NextSingle() * 360f );
			chunkGo.WorldScale = Vector3.One * ChunkScale;

			// Visual
			if ( _chunkModel is not null )
			{
				var mr = chunkGo.Components.Create<ModelRenderer>();
				mr.Model = _chunkModel;
			}


			// Collider (must NOT ignore traces)
			var col = chunkGo.Components.Create<SphereCollider>();
			col.Radius = ChunkColliderRadius;

			// Interactable pickup
			var chunk = chunkGo.Components.Create<RockChunk>();
			chunk.StoneAmount = amounts[i];
			chunk.UseDistance = UseDistance; // reuse same distance as node (or set your own)

			_spawnedChunks.Add( chunkGo );
		}
	}

	private static int[] SplitTotalIntoRandomParts( int total, int parts )
	{
		// Example: total=10 parts=3 => [3,5,2] etc.
		var result = new int[parts];
		int remaining = total;

		for ( int i = 0; i < parts; i++ )
		{
			int left = parts - i;
			if ( left == 1 )
			{
				result[i] = remaining;
				break;
			}

			// Keep at least 1 for each remaining part
			int maxThis = remaining - (left - 1);
			int val = Random.Shared.Next( 1, maxThis + 1 );
			result[i] = val;
			remaining -= val;
		}

		return result;
	}

	private async Task RespawnLater()
	{
		await Task.DelaySeconds( RespawnSeconds );

		if ( !GameObject.IsValid() )
			return;

		_hitsRemaining = MaxHits;
		_depleted = false;

		// Remove any unpicked chunks
		CleanupChunks();

		SetNodeEnabled( true );
		Log.Info( $"MINING: {GameObject.Name} respawned" );
	}

	private void CleanupChunks()
	{
		for ( int i = _spawnedChunks.Count - 1; i >= 0; i-- )
		{
			var go = _spawnedChunks[i];
			if ( go.IsValid() )
				go.Destroy();

			_spawnedChunks.RemoveAt( i );
		}
	}

	private void SetNodeEnabled( bool enabled )
	{
		foreach ( var mr in GameObject.Components.GetAll<ModelRenderer>( FindMode.EverythingInSelfAndChildren ) )
			mr.Enabled = enabled;

		foreach ( var col in GameObject.Components.GetAll<Collider>( FindMode.EverythingInSelfAndChildren ) )
			col.Enabled = enabled;
	}
}
