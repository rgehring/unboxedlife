namespace UnboxedLife;

public sealed class CryptoMiningComponent : Component
{
	[Property] public int IncomePerSecond { get; set; } = 1;
	[Property] public float PayoutIntervalSeconds { get; set; } = 1.0f;

	private float _accum;

	// cache
	private Connection _cachedOwner;
	private BankAccount _cachedBank;
	private TimeSince _sinceResolve;

	protected override void OnUpdate()
	{
		if ( !Networking.IsHost ) return;

		var owner = GameObject.Network?.Owner;
		if ( owner is null ) return;

		// If owner changed or we don't have a bank cached, resolve occasionally
		if ( _cachedOwner != owner )
		{
			_cachedOwner = owner;
			_cachedBank = null;
			_sinceResolve = 999f;
		}

		if ( _cachedBank is null && _sinceResolve > 0.5f )
		{
			_cachedBank = FindOwnersBankAccount( owner );
			_sinceResolve = 0f;
		}

		_accum += Time.Delta;
		if ( _accum < PayoutIntervalSeconds )
			return;

		var intervals = (int)(_accum / PayoutIntervalSeconds);
		_accum -= intervals * PayoutIntervalSeconds;

		var amount = IncomePerSecond * intervals;
		if ( amount <= 0 ) return;

		_cachedBank?.AddMoney( amount );
	}

	private BankAccount FindOwnersBankAccount( Connection owner )
	{
		var state = Scene.GetAllObjects( true )
			.FirstOrDefault( go =>
				go.Network?.Owner == owner &&
				go.Components.Get<BankAccount>() != null );

		return state?.Components.Get<BankAccount>();
	}
}
