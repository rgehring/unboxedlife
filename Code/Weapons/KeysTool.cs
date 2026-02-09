using Sandbox;

namespace UnboxedLife;

public sealed class KeysTool : Component
{
	[Property] public float TraceDistance { get; set; } = 200f;
	[Property] public float EyeHeight { get; set; } = 64f;

	// spam control
	private TimeSince _sinceStatusLog;
	private TimeSince _sinceInputLog;

	protected override void OnUpdate()
	{
		// local owner only
		if ( GameObject.Network?.IsOwner != true )
		{
			if ( _sinceStatusLog > 1f )
			{
				_sinceStatusLog = 0;
				//Log.Info( "[KeysTool] Not owner - skipping" );
			}
			return;
		}

		var equip = Components.Get<EquipComponent>();
		if ( equip is null )
		{
			if ( _sinceStatusLog > 1f )
			{
				_sinceStatusLog = 0;
				//Log.Info( "[KeysTool] No EquipComponent" );
			}
			return;
		}

		if ( equip.ActiveSlot != EquipComponent.Slot.Keys )
		{
			if ( _sinceStatusLog > 1f )
			{
				_sinceStatusLog = 0;
				// so far fixed so we dont need this log at the moment
				// Log.Info( $"[KeysTool] Not in Keys slot (ActiveSlot={equip.ActiveSlot})" );
			}
			return;
		}

		var a1 = Input.Pressed( "attack1" );
		var a2 = Input.Pressed( "attack2" );

		if ( (a1 || a2) && _sinceInputLog > 0.1f )
		{
			_sinceInputLog = 0;
			Log.Info( $"[KeysTool] Input pressed: attack1={a1} attack2={a2}" );
		}

		bool? wantLocked = null;
		if ( a1 ) wantLocked = true;
		else if ( a2 ) wantLocked = false;
		else return;

		var pawn = PawnResolver.GetLocalPawn( Scene );
		var pc = pawn.Components.Get<PlayerController>();

		if ( pc is null )
		{
			Log.Info( "[KeysTool] No PlayerController" );
			return;
		}

		var tr = TraceUtil.TraceFromEyes(
			pawn,
			pc,
			EyeHeight,
			TraceDistance
		); 
		if ( !tr.Hit )
		{
			Log.Info( "[KeysTool] Trace did not hit" );
			return;
		}

		var door = tr.GameObject?.Components.Get<Door>( FindMode.InSelf );
		if ( door is null )
		{
			Log.Info( $"[KeysTool] Trace hit {tr.GameObject?.Name} but no Door found in ancestors" );
			return;
		}

		Log.Info( $"[KeysTool] Request lock={wantLocked.Value} on door={door.GameObject.Name} id={door.GameObject.Id}" );
		RequestSetLockRpc( door.GameObject, wantLocked.Value );
	}

	[Rpc.Host]
	private void RequestSetLockRpc( GameObject doorObject, bool locked )
	{
		Log.Info( $"[KeysTool][HOST] RPC received locked={locked} caller={Rpc.Caller?.DisplayName}" );

		if ( !Networking.IsHost ) return;
		if ( GameObject.Network?.Owner != Rpc.Caller )
		{
			Log.Info( "[KeysTool][HOST] Reject: caller does not own this pawn" );
			return;
		}

		if ( doorObject is null || !doorObject.IsValid() )
		{
			Log.Info( "[KeysTool][HOST] Reject: invalid door object" );
			return;
		}

		var door = doorObject.Components.Get<Door>( FindMode.InSelf | FindMode.InAncestors );
		if ( door is null )
		{
			Log.Info( "[KeysTool][HOST] Reject: Door component not found" );
			return;
		}

		var ok = door.SetLockedHost( Rpc.Caller, locked );
		Log.Info( $"[KeysTool][HOST] SetLockedHost ok={ok} nowLocked={door.IsLocked}" );

		ConfirmLockResultRpc( ok, locked );
	}

	[Rpc.Owner]
	private void ConfirmLockResultRpc( bool ok, bool locked )
	{
		Log.Info( ok
			? (locked ? "[Keys] Locked" : "[Keys] Unlocked")
			: "[Keys] No access" );
	}
}
