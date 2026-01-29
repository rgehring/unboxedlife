using Sandbox;

namespace UnboxedLife;

public sealed class ResourceWallet : Component
{
	[Property] public int Stone { get; private set; }
	[Property] public int IronOre { get; private set; }

	// Host-only mutation (authoritative)
	public void AddStone( int amount )
	{
		if ( !Networking.IsHost ) return;
		Stone += amount;
		Log.Info( $"WALLET: +{amount} Stone (total {Stone})" );
	}

	public void AddIronOre( int amount )
	{
		if ( !Networking.IsHost ) return;
		IronOre += amount;
		Log.Info( $"WALLET: +{amount} IronOre (total {IronOre})" );
	}
}
