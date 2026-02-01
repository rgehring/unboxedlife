using Sandbox;
using System;

namespace UnboxedLife;

public sealed class ResourceWallet : Component
{
	[Sync, Property] public int Stone { get; private set; }
	[Sync, Property] public int IronOre { get; private set; }


	//public event Action<int>? StoneChanged;
	public event Action Changed;

	protected override void OnStart()
	{
		
	}

	// Host-only mutation (authoritative)
	public void AddStone( int amount )
	{
		if ( !Networking.IsHost ) return;

		Stone += amount;
		Log.Info( $"WALLET: +{amount} Stone (total {Stone})" );
		Changed?.Invoke();
	}

	public void AddIronOre( int amount )
	{
		if ( !Networking.IsHost ) return;

		IronOre += amount;
		Log.Info( $"WALLET: +{amount} IronOre (total {IronOre})" );
		Changed?.Invoke();
	}
}
