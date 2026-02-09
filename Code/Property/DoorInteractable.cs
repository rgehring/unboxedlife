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

		var lockHint = Door.IsLocked ? "Unlock (Keys)" : "Lock (Keys)";

		return Action switch
		{
			PropertyAction.OpenDoor =>
				Door.IsLocked
					? $"Locked — {lockHint}"
					: (Door.IsOpen ? $"Close — {lockHint}" : $"Open — {lockHint}"),

			PropertyAction.LockpickDoor =>
				Door.IsBeingLockpicked ? "Lockpicking..." : "Lockpick",

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

		var state = interactor.Components.Get<PlayerLink>()?.State;
		var job = state?.Components.Get<JobComponent>()?.CurrentJob ?? JobId.Citizen;

		if ( zone is not null && zone.IsGovernment )
		{
			// Police can operate government doors normally.
			// Lockpicking is allowed for anyone who has the tool.
			if ( Action == PropertyAction.LockpickDoor )
			{
				if ( !Door.IsLocked ) return false;
				if ( Door.IsBeingLockpicked ) return false;

				var equip = interactor.Components.Get<EquipComponent>();
				return equip is not null && equip.ActiveSlot == EquipComponent.Slot.Lockpick;
			}

			if ( job != JobId.Police )
			{
				// Non-police cannot open locked doors and cannot lock/unlock gov doors
				return Action switch
				{
					PropertyAction.OpenDoor => !Door.IsLocked,
					PropertyAction.LockDoor => false,
					_ => false
				};
			}

			// Police path
			return Action switch
			{
				PropertyAction.OpenDoor => !Door.IsLocked,
				PropertyAction.LockDoor => false,
				_ => false
			};
		}


		switch ( Action )
		{
			case PropertyAction.OpenDoor:
				// Anyone can open/close if unlocked, regardless of ownership
				return !Door.IsLocked;

			case PropertyAction.LockDoor:
				return false; // keys-only

			case PropertyAction.LockpickDoor:
				if ( zone is null ) return false;
				if ( !Door.IsLocked ) return false;
				if ( Door.IsBeingLockpicked ) return false;
				//Optional: police-only area; no lockpicking OR no lockpicking unowned zones
				//if ( zone.IsGovernment ) return false;     
				//if ( !zone.IsOwned ) return false;

				// Must have lockpick equipped
				var equip = interactor.Components.Get<EquipComponent>();
				if ( equip is null || equip.ActiveSlot != EquipComponent.Slot.Lockpick )
					return false;

				// Optional: only allow non-owners to lockpick (prevents owner using lockpick pointlessly)
				// if ( zone.HasAccess( owner.SteamId ) ) return false;

				return true;

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

			case PropertyAction.LockpickDoor:
				// duration can be a property later; start with 5s
				Door.StartLockpickHost( interactor.Network?.Owner, 5f );
				break;

		}
	}
}
