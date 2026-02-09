namespace UnboxedLife;

public sealed class EquipComponent : Component
{
	public enum Slot
	{
		Empty = 0,
		Fists = 1,
		Pistol = 2,
		Lockpick = 3,
		Keys = 4
	}


	// Host-authoritative equip state that persists across respawns
	[Sync( SyncFlags.FromHost )]
	public Slot ActiveSlot { get; private set; } = Slot.Empty;

	protected override void OnUpdate()
	{
		if ( GameObject.Network?.IsOwner != true )
			return;

		var wheel = Input.MouseWheel.y;

		if ( wheel > 0.0f )
			RequestCycleRpc( +1 );
		else if ( wheel < 0.0f )
			RequestCycleRpc( -1 );
	}

	[Rpc.Host]
	private void RequestCycleRpc( int dir )
	{
		if ( !Networking.IsHost )
			return;

		if ( GameObject.Network?.Owner != Rpc.Caller )
			return;

		var next = ((int)ActiveSlot + dir) % 5;
		if ( next < 0 ) next += 5;


		ActiveSlot = (Slot)next;
	}
}
