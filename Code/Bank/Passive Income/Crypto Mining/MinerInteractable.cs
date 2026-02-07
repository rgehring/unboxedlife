namespace UnboxedLife;

public sealed class MinerInteractable : Interactable
{
	[Property] public CryptoMiningComponent Miner { get; set; }

	protected override void OnStart()
	{
		Miner ??= Components.Get<CryptoMiningComponent>( FindMode.InSelf | FindMode.InAncestors );

		// Secure it by property access
		RequirePropertyAccess = true;
		Action = PropertyAction.Use;
		Prompt = "Manage Miner";
	}

	public override void Interact( GameObject interactor )
	{
		if ( !Networking.IsHost ) return;

		// For now, just confirm access + interaction path works
		Log.Info( $"Miner used by {interactor.Name}" );

		// Later: open UI, withdraw balance, etc.
	}
}
