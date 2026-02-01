using Sandbox;

namespace UnboxedLife;

public sealed class RockChunk : Interactable
{
	[Property] public int StoneAmount { get; set; } = 1;

	protected override void OnStart()
	{
		Prompt = $"Pick up Rock (+{StoneAmount})";
	}

	public override void Interact( GameObject interactor )
	{
		// InteractHost already ensures host + distance via Interactable.cs
		var wallet = interactor.Components.Get<ResourceWallet>();
		if ( wallet is null )
		{
			Log.Warning( "CHUNK: Interactor has no ResourceWallet" );
			return;
		}

		wallet.AddStone( StoneAmount );
		Log.Info( $"CHUNK: {interactor.Name} picked up {GameObject.Name} (+{StoneAmount})" );

		GameObject.Destroy();
	}
}
