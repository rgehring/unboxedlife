namespace UnboxedLife;

public static class PropertyZoneRegistry
{
	private static readonly List<PropertyZone> Zones = new();

	public static void Register( PropertyZone zone )
	{
		if ( !Zones.Contains( zone ) )
			Zones.Add( zone );
	}

	public static void Unregister( PropertyZone zone )
	{
		Zones.Remove( zone );
	}

	public static PropertyZone FindZoneAt( Vector3 position )
	{
		for ( int i = Zones.Count - 1; i >= 0; i-- )
		{
			var zone = Zones[i];
			if ( zone is null || !zone.IsValid )
			{
				Zones.RemoveAt( i );
				continue;
			}

			if ( zone.Contains( position ) )
				return zone;
		}

		return null;
	}

}
