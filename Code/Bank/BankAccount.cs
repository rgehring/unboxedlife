namespace UnboxedLife;

public sealed class BankAccount : Component
{
	// Host owns this value, regardless of who owns the networked object.
	[Sync( SyncFlags.FromHost ), Property]
	public int Money { get; private set; }

	protected override void OnStart()
	{
		// Optional: set a starting balance once, host-only.
		if ( Networking.IsHost && Money == 0 )
			Money = 10000;
	}

	public void AddMoney( int amount )
	{
		if ( !Networking.IsHost ) return;
		if ( amount <= 0 ) return;
		Money += amount;
	}

	public void RemoveMoney( int amount )
	{
		if ( !Networking.IsHost ) return;
		if ( amount <= 0 ) return;
		Money -= amount;
	}

	public bool TrySpend( int amount )
	{
		if ( !Networking.IsHost ) return false;
		if ( amount <= 0 ) return true;
		if ( Money < amount ) return false;

		Money -= amount;
		return true;
	}
}
