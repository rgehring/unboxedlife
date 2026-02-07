public sealed class DoorAccess : Component
{
	private PropertyZone _zone;

	protected override void OnStart()
	{
		_zone = PropertyZoneRegistry.FindZoneAt( WorldPosition );
	}

	public bool CanOpen( SteamId steamId )
	{
		var zone = PropertyZoneRegistry.FindZoneAt( WorldPosition );
		if ( zone is null )
			return true;

		return zone.HasAccess( steamId );
	}

}
