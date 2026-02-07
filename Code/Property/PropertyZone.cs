namespace UnboxedLife;

public sealed class PropertyZone : Component
{
	[Property] public string PropertyId { get; set; }
	[Property] public string DisplayName { get; set; }
	[Property] public Collider ZoneCollider { get; set; }
	[Sync( SyncFlags.FromHost )] public SteamId OwnerSteamId { get; private set; }
	[Sync( SyncFlags.FromHost )] public string OwnerName { get; private set; } = "";
	[Sync] public bool IsForSale { get; private set; } = true;
	[Sync] public int PurchasePrice { get; set; }
	[Sync( SyncFlags.FromHost )] public int LastPaidPrice { get; private set; }     // NEW: remember last paid price (host writes; clients can read)
	private readonly HashSet<SteamId> _allowed = new();     // Host-only list (MVP)
	public bool IsOwned => OwnerSteamId != default;
	
	protected override void OnDestroy() => PropertyZoneRegistry.Unregister( this );
	protected override void OnStart()
	{
		ZoneCollider ??= Components.Get<Collider>( FindMode.InSelf | FindMode.InChildren );
		PropertyZoneRegistry.Register( this );
	}

	public bool Contains( Vector3 position )
	{
		if ( ZoneCollider is null ) return false;
		return ZoneCollider.GetWorldBounds().Contains( position );
	}

	// Host-only: claim ownership (you’ll call this from a purchase interaction)
	public bool TryBuy( SteamId buyerId, string buyerName, int paidPrice )
	{
		if ( !Networking.IsHost ) return false;
		if ( buyerId == default ) return false;
		if ( IsOwned ) return false;

		OwnerSteamId = buyerId;
		OwnerName = buyerName ?? "";
		LastPaidPrice = paidPrice;

		IsForSale = false;
		_allowed.Clear();
		return true;
	}

	public int GetSellRefund( int refundPercent )
	{
		// percent like 50 = 50%
		if ( refundPercent <= 0 ) return 0;
		if ( LastPaidPrice <= 0 ) return 0;
		return (LastPaidPrice * refundPercent) / 100;
	}

	// Host-only: sell/abandon while still connected
	public void Sell()
	{
		if ( !Networking.IsHost ) return;
		ClearOwner_AndReset();
	}

	public void ClearOwner_AndReset()
	{
		if ( !Networking.IsHost ) return;

		OwnerSteamId = default;
		OwnerName = "";
		LastPaidPrice = 0;

		_allowed.Clear();
		IsForSale = true;
	}
	public bool HasAccess( SteamId steamId )
	{
		if ( OwnerSteamId == default ) return false;
		if ( OwnerSteamId == steamId ) return true;
		return _allowed.Contains( steamId );
	}



	public void AddGuest( SteamId guest )
	{
		if ( !Networking.IsHost ) return;
		if ( guest == default ) return;
		_allowed.Add( guest );
	}

	public bool AddAllowed( SteamId steamId )
	{
		if ( !Networking.IsHost ) return false;
		if ( OwnerSteamId == default ) return false;
		return _allowed.Add( steamId );
	}

	public bool RemoveAllowed( SteamId steamId )
	{
		if ( !Networking.IsHost ) return false;
		return _allowed.Remove( steamId );
	}
}
