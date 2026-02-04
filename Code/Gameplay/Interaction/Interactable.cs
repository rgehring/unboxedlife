namespace UnboxedLife;

public abstract class Interactable : Component
{
	[Property] public string Prompt { get; set; } = "Use";

	// Max distance this interactable can be used from
	[Property] public float UseDistance { get; set; } = 120f;

	// Host-only validation gate
	public virtual bool CanInteract( GameObject interactor )
	{
		if ( interactor is null ) return false;

		var dist = interactor.WorldPosition.Distance( GameObject.WorldPosition );
		return dist <= UseDistance;
	}

	// Host performs the action
	public abstract void Interact( GameObject interactor );

	public void InteractHost( GameObject interactor )
	{
		if ( !Networking.IsHost ) return;
		if ( !CanInteract( interactor ) ) return;

		Log.Info( $"InteractHost: {interactor.Name} -> {GameObject.Name}" );

		Interact( interactor );
	}

}
