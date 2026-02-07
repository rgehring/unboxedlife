using Sandbox;

namespace UnboxedLife;

public sealed class DoorInteractable : Interactable
{
	[Property] public Door Door { get; set; }

	protected override void OnStart()
	{
		Door ??= Components.Get<Door>( FindMode.InSelf | FindMode.InAncestors );
	}

	public override string GetPrompt( GameObject interactor = null )
	{
		if ( Door is null )
			return base.GetPrompt( interactor );

		return Action switch
		{
			PropertyAction.OpenDoor => Door.IsOpen ? "Close" : (Door.IsLocked ? "Locked" : "Open"),
			PropertyAction.LockDoor => Door.IsLocked ? "Unlock" : "Lock",
			_ => base.GetPrompt( interactor )
		};
	}


	public override bool CanInteract( GameObject interactor )
	{
		if ( interactor is null ) return false;

		// distance check
		var dist = interactor.WorldPosition.Distance( GameObject.WorldPosition );
		if ( dist > UseDistance ) return false;

		// host-only enforcement
		if ( !Networking.IsHost ) return false;

		if ( Door is null ) return false;

		// who is interacting (SteamId comes from the pawn's network owner)
		var owner = interactor.Network?.Owner;
		if ( owner is null ) return false;

		var zone = PropertyZoneRegistry.FindZoneAt( GameObject.WorldPosition );

		switch ( Action )
		{
			case PropertyAction.OpenDoor:
				// Anyone can open/close if unlocked, regardless of ownership
				return !Door.IsLocked;

			case PropertyAction.LockDoor:
				// Must be inside a property, property must be owned, and caller must have access
				if ( zone is null ) return false;
				if ( !zone.IsOwned ) return false;          // <- denies lock/unlock on unowned property
				return zone.HasAccess( owner.SteamId );

			default:
				return true;
		}
	}


	public override void Interact( GameObject interactor )
	{
		// Host only, because InteractHost already required Networking.IsHost
		switch ( Action )
		{
			case PropertyAction.OpenDoor:
				Door.ToggleOpenHost();
				break;

			case PropertyAction.LockDoor:
				Door.ToggleLockHost();
				break;
		}
	}
}
