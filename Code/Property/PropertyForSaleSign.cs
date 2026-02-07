using Sandbox;

namespace UnboxedLife;

public sealed class PropertyForSaleSign : Interactable
{
	[Property] public PropertyZone Zone { get; set; }
	[Property] public int PriceOverride { get; set; } = 0;

	// NEW: enable selling + refund percent
	[Property] public bool AllowSell { get; set; } = true;
	[Property] public int RefundPercent { get; set; } = 50; // 0 = no refund

	private int EffectivePrice => (PriceOverride > 0) ? PriceOverride : (Zone?.PurchasePrice ?? 0);


	protected override void OnStart()
	{
		UseDistance = 120f;
		RequirePropertyAccess = false; // must stay false to allow buying

		Zone ??= Components.Get<PropertyZone>( FindMode.InAncestors );

		RefreshPrompt();
	}

	protected override void OnUpdate()
	{
		// Keep it up-to-date for all clients as ownership changes
		RefreshPrompt(); //TODO: Cache the last known state and only update if it changes, to avoid unnecessary string updates
	}

	private void RefreshPrompt()
	{
		if ( Zone is null )
		{
			Prompt = "Invalid Property";
			return;
		}

		if ( !Zone.IsOwned )
		{
			var price = EffectivePrice;
			Prompt = price > 0 ? $"Buy {Zone.DisplayName} (${price})" : $"Buy {Zone.DisplayName}";
			return;
		}

		var localSid = Connection.Local?.SteamId ?? default;
		var isYou = (localSid != default && Zone.OwnerSteamId == localSid);

		if ( isYou )
		{
			if ( AllowSell )
			{
				var refund = Zone.GetSellRefund( RefundPercent );
				Prompt = refund > 0
					? $"Sell {Zone.DisplayName} (+${refund})"
					: $"Sell {Zone.DisplayName}";
			}
			else
			{
				Prompt = $"Owned by you: {Zone.DisplayName}";
			}
		}
		else
		{
			Prompt = $"Owned by {Zone.OwnerName}";
		}
	}

	public override void Interact( GameObject interactor )
	{
		if ( !Networking.IsHost ) return;
		if ( Zone is null ) return;

		var buyerConn = interactor.Network?.Owner;
		if ( buyerConn is null ) return;

		var buyerSid = buyerConn.SteamId;

		// If owned, only owner can sell (MVP)
		if ( Zone.IsOwned )
		{
			if ( buyerSid != Zone.OwnerSteamId )
				return;

			if ( !AllowSell )
				return;

			var refund = Zone.GetSellRefund( RefundPercent );
			if ( refund > 0 )
				FindBankAccount( buyerConn )?.AddMoney( refund );

			Zone.ClearOwner_AndReset();
			Log.Info( $"[Property] {buyerConn.DisplayName} sold PropertyId: {Zone.PropertyId} (refund ${refund})" );
			return;
		}

		// BUY
		var price = EffectivePrice;

		if ( price > 0 )
		{
			var bank = FindBankAccount( buyerConn );
			if ( bank is null ) return;
			if ( !bank.TrySpend( price ) ) return;
		}

		if ( Zone.TryBuy( buyerSid, buyerConn.DisplayName, price ) )
		{
			Log.Info( $"[Property] {buyerConn.DisplayName} bought PropertyId: {Zone.PropertyId} (DisplayName: {Zone.DisplayName}) for ${price}" );
		}
	}

	private BankAccount FindBankAccount( Connection owner )
	{
		// MVP: scan for the owner's PlayerState bank
		var state = Scene.GetAllObjects( true )
			.FirstOrDefault( go =>
				go.Network?.Owner == owner &&
				go.Components.Get<BankAccount>() != null );

		return state?.Components.Get<BankAccount>();
	}
}
