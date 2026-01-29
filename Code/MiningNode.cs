using Sandbox;
using System.Threading.Tasks;

namespace UnboxedLife;

public sealed class MiningNode : Interactable
{
	[Property] public string NodeName { get; set; } = "Rock";
	[Property] public int MaxHits { get; set; } = 5;
	[Property] public float RespawnSeconds { get; set; } = 30f;
	[Property] public float SecondsPerHit { get; set; } = 1.0f;

	[Property] public int StonePerNode { get; set; } = 10;

	private int _hitsRemaining;
	private bool _depleted;
	private float _nextHitTime;

	protected override void OnStart()
	{
		_hitsRemaining = MaxHits;
		_depleted = false;
		Prompt = $"Mine {NodeName}";
	}

	// This is required by your base class.
	// It will only be called via InteractHost(), which already checks host + distance. :contentReference[oaicite:1]{index=1}
	public override void Interact( GameObject interactor )
	{
		if ( Time.Now < _nextHitTime )
			return;

		_nextHitTime = Time.Now + SecondsPerHit;

		if ( _depleted ) return;

		var wallet = interactor.Components.Get<ResourceWallet>();
		if ( wallet is null )
		{
			Log.Warning( "MINING: Interactor has no ResourceWallet" );
			return;
		}

		_hitsRemaining--;
		Log.Info( $"MINING: {interactor.Name} hit {GameObject.Name} ({_hitsRemaining}/{MaxHits})" );

		if ( _hitsRemaining > 0 )
			return;

		_depleted = true;

		// Reward for mining the node
		wallet.AddStone( StonePerNode );

		Log.Info( $"MINING: {GameObject.Name} depleted. Respawn in {RespawnSeconds}s" );

		SetNodeEnabled( false );
		_ = RespawnLater();
	}

	private async Task RespawnLater()
	{
		await Task.DelaySeconds( RespawnSeconds );

		if ( !GameObject.IsValid() ) return;

		_hitsRemaining = MaxHits;
		_depleted = false;
		SetNodeEnabled( true );

		Log.Info( $"MINING: {GameObject.Name} respawned" );
	}

	private void SetNodeEnabled( bool enabled )
	{
		// Model renderers on this object and children
		foreach ( var mr in GameObject.Components.GetAll<ModelRenderer>( FindMode.EverythingInSelfAndChildren ) )
			mr.Enabled = enabled;

		// Colliders on this object and children
		foreach ( var col in GameObject.Components.GetAll<Collider>( FindMode.EverythingInSelfAndChildren ) )
			col.Enabled = enabled;
	}
}
