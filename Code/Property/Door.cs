using Sandbox;

namespace UnboxedLife;

public enum DoorSecurityLevel
{
	Basic = 1,        // lockpick, ram
	Reinforced = 2,   // advanced lockpick, ram
	Secure = 3        // explosives / keycard / keypad
}


public sealed class Door : Component
{
	[Sync( SyncFlags.FromHost )] public bool IsOpen { get; private set; }
	[Sync( SyncFlags.FromHost )] public bool IsLocked { get; private set; }
	[Sync( SyncFlags.FromHost )] public bool IsBeingLockpicked { get; private set; }
	[Sync( SyncFlags.FromHost )] public float LockpickProgress01 { get; private set; }
	[Sync( SyncFlags.FromHost )] public SteamId LockpickerSteamId { get; private set; } // who started lockpicking
	private int _lockpickSessionId = 0;

	// client-side sound edge tracking
	private bool _lastBeingLockpicked = false;

	[Property] public float LockpickCancelDistance { get; set; } = 140f;

	[Property] public SoundEvent LockpickStartSound { get; set; }
	[Property] public SoundEvent LockpickLoopSound { get; set; }
	[Property] public SoundEvent LockpickSuccessSound { get; set; }
	[Property] public SoundEvent LockpickFailSound { get; set; }
	private SoundHandle _lockpickLoopHandle;


	[Property] public DoorSecurityLevel SecurityLevel { get; set; } = DoorSecurityLevel.Basic;

	[Property] public GameObject DoorPivot { get; set; }

	[Property] public float OpenYawDegrees { get; set; } = 90f;
	[Property] public float OpenSpeed { get; set; } = 3f;

	[Property] public SoundEvent OpenSound { get; set; }
	[Property] public SoundEvent CloseSound { get; set; }

	private Rotation _closedLocalRot;
	private Rotation _openLocalRot;

	// 0..1 smoothed parameter
	private float _t;

	private bool _initialized;
	private bool _lastIsOpen;
	public bool IsLockpickable =>
	IsLocked && SecurityLevel <= DoorSecurityLevel.Reinforced;

	public bool IsRamBreakable =>
		IsLocked && SecurityLevel == DoorSecurityLevel.Basic;

	public string GetLockHint()
	=> IsLocked ? "Unlock (Keys)" : "Lock (Keys)";


	protected override void OnStart()
	{
		DoorPivot ??= GameObject;

		_closedLocalRot = DoorPivot.LocalRotation;
		_openLocalRot = _closedLocalRot * Rotation.FromYaw( OpenYawDegrees );

		_t = IsOpen ? 1f : 0f;
		_lastIsOpen = IsOpen;
		_initialized = true;

		ApplyVisualImmediate();
	}

	protected override void OnUpdate()
	{
		if ( !_initialized || DoorPivot is null ) return;

		// lockpick sounds driven by state transitions (runs on clients too)
		if ( _lastBeingLockpicked != IsBeingLockpicked )
		{
			_lastBeingLockpicked = IsBeingLockpicked;

			if ( IsBeingLockpicked )
			{
				PlayIfSet( LockpickStartSound );

				if ( _lockpickLoopHandle.IsValid() )
					_lockpickLoopHandle.Stop();

				_lockpickLoopHandle = default;

				if ( LockpickLoopSound is not null )
					_lockpickLoopHandle = Sound.Play( LockpickLoopSound, WorldPosition );
			}
			else
			{
				if ( _lockpickLoopHandle.IsValid() )
				{
					_lockpickLoopHandle.Stop();
					_lockpickLoopHandle = default;
				}

				if ( !IsLocked ) PlayIfSet( LockpickSuccessSound );
				else PlayIfSet( LockpickFailSound );
			}
		}


		// play sounds when state flips (clients will also run this when sync updates arrive)
		if ( _lastIsOpen != IsOpen )
		{
			_lastIsOpen = IsOpen;

			if ( IsOpen ) PlayIfSet( OpenSound );
			else PlayIfSet( CloseSound );
		}

		// smooth toward target
		var target = IsOpen ? 1f : 0f;
		_t = MathX.Lerp( _t, target, Time.Delta * OpenSpeed );

		// snap when close enough to avoid asymptotic never-finish
		if ( Math.Abs( _t - target ) < 0.001f )
			_t = target;

		DoorPivot.LocalRotation = Rotation.Lerp( _closedLocalRot, _openLocalRot, _t );
	}

	public bool SetLockedHost( Connection caller, bool locked )
	{
		if ( !Networking.IsHost ) return false;
		if ( caller is null ) return false;

		// Use the pivot (or root) position for zone lookup so door roots/pivots don't lie outside.
		var probePos = (DoorPivot?.IsValid() ?? false) ? DoorPivot.WorldPosition : WorldPosition;

		var zone = PropertyZoneRegistry.FindZoneAt( probePos );
		if ( zone is null )
		{
			Log.Info( $"[Door] No zone at door pos={probePos} (door={GameObject.Name})" );
			return false;
		}

		// Government zone: allow Police (or other gov jobs) WITHOUT needing zone.IsOwned
		if ( zone.IsGovernment )
		{
			if ( !IsPolice( caller ) )
			{
				Log.Info( $"[Door] Gov zone: denied. caller={caller.DisplayName} steamId={caller.SteamId} zoneId={zone.PropertyId}" );
				return false;
			}

			// Allowed -> apply lock below
		}
		else
		{
			// Normal property: must be owned + caller must have access
			if ( !zone.IsOwned )
			{
				Log.Info( $"[Door] Zone not owned. zoneId={zone.PropertyId} door={GameObject.Name}" );
				return false;
			}

			if ( !zone.HasAccess( caller.SteamId ) )
			{
				Log.Info( $"[Door] No access. caller={caller.DisplayName} steamId={caller.SteamId} zoneOwner={zone.OwnerSteamId} zoneId={zone.PropertyId}" );
				return false;
			}
		}

		// Only after permission checks do we allow no-op success.
		if ( IsLocked == locked )
			return true;

		IsLocked = locked;
		Log.Info( $"[Door] SetLockedHost applied after={IsLocked} zoneId={zone.PropertyId} gov={zone.IsGovernment}" );
		return true;
	}

	// Host-only: resolve police from the caller's current pawn.
	// Replace the internals to match your project's job system.
	private bool IsPolice( Connection caller )
	{
		// Find the pawn owned by this connection (host-side)
		var pawn = Scene.GetAllObjects( true )
			.FirstOrDefault( go =>
				go.Network?.Owner == caller &&
				go.Components.Get<Sandbox.PlayerController>() is not null );

		if ( pawn is null )
			return false;

		var state = pawn.Components.Get<PlayerLink>()?.State;
		var job = state?.Components.Get<JobComponent>()?.CurrentJob ?? JobId.Citizen;

		return job == JobId.Police;
	}

	public async void StartLockpickHost( Connection picker, float durationSeconds )
	{
		if ( !Networking.IsHost ) return;
		if ( picker is null ) return;
		if ( !IsLocked ) return;
		if ( IsBeingLockpicked ) return;

		IsBeingLockpicked = true;
		LockpickProgress01 = 0f;
		LockpickerSteamId = picker.SteamId;

		_lockpickSessionId++;
		var session = _lockpickSessionId;

		bool success = false;	//If you plan to use it later for XP gain, alert system, stats, etc.
		_ = success;			//(but silence the warning) assigned but never used


		try
		{
			var start = Time.Now;

			while ( Time.Now - start < durationSeconds )
			{
				if ( !GameObject.IsValid() ) return;

				// cancelled or superseded
				if ( session != _lockpickSessionId ) return;

				// door got unlocked by something else
				if ( !IsLocked ) return;

				// picker gone
				if ( picker is null ) return;

				var pawn = GetPawnFor( picker );
				if ( pawn is null )
					return;

				// cancellation: swapped tools
				if ( !IsHoldingLockpick( pawn ) )
					return;

				// cancellation: moved too far (use pivot probe)
				var doorPos = (DoorPivot?.IsValid() ?? false) ? DoorPivot.WorldPosition : WorldPosition;
				var dist = pawn.WorldPosition.Distance( doorPos );
				if ( dist > LockpickCancelDistance )
					return;

				LockpickProgress01 = (float)((Time.Now - start) / durationSeconds);
				await GameTask.Yield();
			}

			// Success only if still locked at end
			if ( IsLocked )
			{
				IsLocked = false;
				success = true;
			}
		}
		finally
		{
			// If we exited due to cancel/fail, keep door locked.
			LockpickProgress01 = 0f;
			IsBeingLockpicked = false;
			LockpickerSteamId = default;

			// Bump session so any in-flight loop stops immediately
			_lockpickSessionId++;
		}
	}

	private GameObject GetPawnFor( Connection c )
	{
		if ( c is null ) return null;

		return Scene.GetAllObjects( true )
			.FirstOrDefault( go =>
				go.Network?.Owner == c &&
				go.Components.Get<Sandbox.PlayerController>() is not null );
	}

	private bool IsHoldingLockpick( GameObject pawn )
	{
		var equip = pawn?.Components.Get<EquipComponent>( FindMode.InSelf | FindMode.InChildren );
		return equip is not null && equip.ActiveSlot == EquipComponent.Slot.Lockpick;
	}


	public bool CancelLockpickHost( Connection caller )
	{
		if ( !Networking.IsHost ) return false;
		if ( caller is null ) return false;
		if ( !IsBeingLockpicked ) return false;

		// only the lockpicker can cancel
		if ( LockpickerSteamId != default && caller.SteamId != LockpickerSteamId )
			return false;

		_lockpickSessionId++; // stop loop
		IsBeingLockpicked = false;
		LockpickProgress01 = 0f;
		LockpickerSteamId = default;

		return true;
	}


	public void ToggleOpenHost()
	{
		if ( !Networking.IsHost ) return;
		if ( IsLocked ) return;

		IsOpen = !IsOpen;

		// host will also play sound next update tick via the flip detection
	}

	public void ToggleLockHost()
	{
		if ( !Networking.IsHost ) return;
		IsLocked = !IsLocked;
	}

	private void ApplyVisualImmediate()
	{
		if ( DoorPivot is null ) return;
		DoorPivot.LocalRotation = Rotation.Lerp( _closedLocalRot, _openLocalRot, _t );
	}

	private void PlayIfSet( SoundEvent snd )
	{
		if ( snd is null ) return;
		Sound.Play( snd, WorldPosition );
	}

	protected override void OnDestroy()
	{
		if ( _lockpickLoopHandle.IsValid() )
			_lockpickLoopHandle.Stop();
	}

}
