using Sandbox;

namespace UnboxedLife;

public sealed class DoorInteractable : Interactable
{
	[Property] public Door Door { get; set; }
	private static GameObject GetState( GameObject interactor )
	=> interactor?.Components.Get<PlayerLink>()?.State;

	private static EquipComponent GetEquip( GameObject interactor )
		=> GetState( interactor )?.Components.Get<EquipComponent>( FindMode.InSelf | FindMode.InChildren );

	private static JobId GetJob( GameObject interactor )
		=> GetState( interactor )?.Components.Get<JobComponent>()?.CurrentJob ?? JobId.Citizen;


	public override bool CanPreview( GameObject interactor )
	{
		if ( interactor is null ) return false;
		if ( Door is null ) return false;

		var equip = GetEquip( interactor );

		return Action switch
		{
			PropertyAction.LockpickDoor =>
				Door.IsLocked &&
				!Door.IsBeingLockpicked &&
				equip is not null &&
				equip.ActiveSlot == EquipComponent.Slot.Lockpick,

			PropertyAction.OpenDoor =>
				// If locked: only show OpenDoor when holding keys (so it can say "Unlock (Keys)")
				(!Door.IsLocked) ||
				(equip is not null && equip.ActiveSlot == EquipComponent.Slot.Keys),

			_ => true
		};
	}

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
		if ( Action == PropertyAction.LockpickDoor )
		{
			Log.Info( $"[Lockpick][HOST][CanInteract] interactor={interactor.Name} door={(Door?.GameObject?.Name ?? "null")} interactGO={GameObject.Name}" );
		}

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

		var zonePos = (Door?.DoorPivot?.IsValid() ?? false) ? Door.DoorPivot.WorldPosition : Door.WorldPosition;
		var zone = PropertyZoneRegistry.FindZoneAt( zonePos );

		var job = GetJob( interactor );


		if ( zone is not null && zone.IsGovernment )
		{
			// Police can operate government doors normally.
			// Lockpicking is allowed for anyone who has the tool.
			if ( Action == PropertyAction.LockpickDoor )
			{
				if ( Door is null )
				{
					Log.Info( $"[Lockpick][HOST][CanInteract] DENY: Door null" );
					return false;
				}

				if ( !Door.IsLocked )
				{
					Log.Info( $"[Lockpick][HOST][CanInteract] DENY: Door not locked" );
					return false;
				}

				if ( Door.IsBeingLockpicked )
				{

					//Log.Info( $"[Lockpick][HOST][CanInteract] DENY: Already being lockpicked" );
					// allow "Use" again only for the same lockpicker
					return owner.SteamId == Door.LockpickerSteamId;
				}

				var equip = GetEquip( interactor );
				if ( equip is null )
				{
					Log.Info( $"[Lockpick][HOST][CanInteract] DENY: No EquipComponent" );
					return false;
				}

				if ( equip.ActiveSlot != EquipComponent.Slot.Lockpick )
				{
					Log.Info( $"[Lockpick][HOST][CanInteract] DENY: Not holding lockpick slot={equip.ActiveSlot}" );
					return false;
				}

				Log.Info( $"[Lockpick][HOST][CanInteract] ALLOW (gov). slot={equip.ActiveSlot}" );
				return true;
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
				if ( zone is null )
				{
					Log.Info( $"[Lockpick][HOST][CanInteract] DENY: zone null at pos={GameObject.WorldPosition}" );
					return false;
				}

				if ( !Door.IsLocked )
				{
					Log.Info( $"[Lockpick][HOST][CanInteract] DENY: Door not locked" );
					return false;
				}

				if ( Door.IsBeingLockpicked )
				{
					Log.Info( $"[Lockpick][HOST][CanInteract] DENY: Already being lockpicked" );
					return false;
				}

				var equip = GetEquip(interactor);
				if ( equip is null || equip.ActiveSlot != EquipComponent.Slot.Lockpick )
				{
					Log.Info( $"[Lockpick][HOST][CanInteract] DENY: must equip lockpick. equip={(equip is null ? "null" : equip.ActiveSlot.ToString())}" );
					return false;
				}

				Log.Info( $"[Lockpick][HOST][CanInteract] ALLOW (non-gov). zone={(zone?.PropertyId ?? "null")}" );
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
				{
					var conn = interactor.Network?.Owner;
					if ( conn is null ) return;

					if ( Door.IsBeingLockpicked )
					{
						Door.CancelLockpickHost( conn );
					}
					else
					{
						Door.StartLockpickHost( conn, 10f );
					}
					break;
				}
		}
	}
}
