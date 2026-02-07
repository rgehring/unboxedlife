using Sandbox;
using static Sandbox.Component;

namespace UnboxedLife;

public sealed class PropertyDisconnectCleanup : Component, INetworkListener
{
	public void OnDisconnected( Connection connection )
	{
		if ( !Networking.IsHost ) return;
		if ( connection is null ) return;

		var sid = connection.SteamId;

		foreach ( var zone in Scene.GetAllComponents<PropertyZone>() )
		{
			if ( zone.OwnerSteamId == sid )
				zone.ClearOwner_AndReset();
		}
	}

	public void OnConnected( Connection connection ) { }
	public void OnActive( Connection connection ) { }
}
