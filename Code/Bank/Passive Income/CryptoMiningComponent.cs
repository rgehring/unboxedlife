namespace UnboxedLife;

public sealed class CryptoMiningComponent : Component
{
	[Property] public int IncomePerSecond { get; set; } = 1;
	[Property] public float PayoutIntervalSeconds { get; set; } = 1.0f;

	float _accum;

	protected override void OnUpdate()
	{
		// Only the host should run the economy loop
		if ( !Networking.IsHost ) return;

		var owner = GameObject.Network?.Owner;
		if ( owner is null ) return;

		_accum += Time.Delta;
		if ( _accum < PayoutIntervalSeconds )
			return;

		// Pay out in whole seconds worth of income
		var intervals = (int)(_accum / PayoutIntervalSeconds);
		_accum -= intervals * PayoutIntervalSeconds;

		var amount = IncomePerSecond * intervals * (int)PayoutIntervalSeconds;
		if ( amount <= 0 ) amount = IncomePerSecond * intervals; // covers small intervals

		FindOwnersBankAccount( owner )?.AddMoney( amount );
	}

	BankAccount FindOwnersBankAccount( Connection owner )
	{
		// Find the owner's persistent PlayerState by ownership + BankAccount component
		var state = Scene.GetAllObjects( true )
			.FirstOrDefault( go =>
				go.Network?.Owner == owner &&
				go.Components.Get<BankAccount>() != null );

		return state?.Components.Get<BankAccount>();
	}
}
