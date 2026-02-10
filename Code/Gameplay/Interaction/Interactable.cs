namespace UnboxedLife;

public enum PropertyAction
{
	Use,
	Build,
	OpenDoor,
	LockDoor,
	LockpickDoor
}

[Title( "Interactable" )]
public abstract class Interactable : Component
{
	// NEW: security integration (not used yet but will be)
	[Property] public bool RequirePropertyAccess { get; set; } = false;
	[Property] public PropertyAction Action { get; set; } = PropertyAction.Use;

	[Property] public string Prompt { get; set; } = "Use";

	// Max distance this interactable can be used from
	[Property] public float UseDistance { get; set; } = 120f;

	public virtual string GetPrompt( GameObject interactor = null )
	{
		return Prompt;
	}

	public virtual bool CanPreview( GameObject interactor )
	{
		// UI-only. Must not rely on host-only state.
		// Default: show prompt if you're in range & (optionally) access checks pass via cached RPC.
		return true;
	}


	// Host-only validation gate
	public virtual bool CanInteract( GameObject interactor )
	{
		if ( interactor is null ) return false;

		var dist = interactor.WorldPosition.Distance( GameObject.WorldPosition );
		if ( dist > UseDistance ) return false;

		if ( !Networking.IsHost ) return false;

		if ( RequirePropertyAccess )
		{
			var owner = interactor.Network?.Owner;
			if ( owner is null ) return false;

			var zone = PropertyZoneRegistry.FindZoneAt( GameObject.WorldPosition );
			if ( zone is null ) return false; // choose: no zone => deny for secured items

			// access check uses SteamId
			if ( !zone.HasAccess( owner.SteamId ) )
				return false;
		}

		return true;
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
