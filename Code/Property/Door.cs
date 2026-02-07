using Sandbox;

namespace UnboxedLife;

public sealed class Door : Component
{
	[Sync( SyncFlags.FromHost )] public bool IsOpen { get; private set; }
	[Sync( SyncFlags.FromHost )] public bool IsLocked { get; private set; }

	[Property] public GameObject DoorPivot { get; set; }

	[Property] public float OpenYawDegrees { get; set; } = 90f;

	// Matches your old “OpenSpeed” smoothing feel (higher = snappier)
	[Property] public float OpenSpeed { get; set; } = 3f;

	[Property] public SoundEvent OpenSound { get; set; }
	[Property] public SoundEvent CloseSound { get; set; }

	private Rotation _closedLocalRot;
	private Rotation _openLocalRot;

	// 0..1 smoothed parameter
	private float _t;

	private bool _initialized;
	private bool _lastIsOpen;

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
}
