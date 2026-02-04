namespace UnboxedLife;

public sealed class ConsumableInteractable : Interactable
{
	[Property] public float HungerGain { get; set; } = 0f;
	[Property] public float ThirstGain { get; set; } = 0f;

	[Property] public bool DestroyOnUse { get; set; } = true;

	public override void Interact( GameObject interactor )
	{
		if ( !Networking.IsHost ) return;

		// Preferred: pawn -> PlayerLink.State
		var state = interactor.Components.Get<PlayerLink>()?.State;

		// Fallback: find state by same owner as the interactor pawn
		if ( state is null )
		{
			var owner = interactor.Network?.Owner;
			if ( owner is not null )
			{
				state = Scene.GetAllObjects( true )
					.FirstOrDefault( go =>
						go.Network?.Owner == owner &&
						go.Components.Get<NeedsComponent>() is not null );
			}
		}

		var needs = state?.Components.Get<NeedsComponent>();
		if ( needs is null )
			return;

		if ( HungerGain > 0 ) needs.AddHunger( HungerGain );
		if ( ThirstGain > 0 ) needs.AddThirst( ThirstGain );

		if ( DestroyOnUse )
			GameObject.Destroy();
	}
}
