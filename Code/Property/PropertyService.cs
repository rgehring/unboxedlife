using Sandbox;
using System.Linq;
using static Sandbox.Component;

namespace UnboxedLife;

public sealed class PropertyService : Component, INetworkListener
{
	public void OnDisconnected( Connection connection )
	{
		if ( !Networking.IsHost ) return;

		var sid = connection.SteamId;

		// Clear any zones owned by this SteamId
		foreach ( var zone in Scene.GetAllComponents<PropertyZone>() )
		{
			if ( zone.OwnerSteamId == sid )
				zone.ClearOwner_AndReset(); // host-only method you add
		}
	}

	public void OnConnected( Connection connection ) { }
	public void OnActive( Connection connection ) { }
}
