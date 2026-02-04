using System.Linq;
using Sandbox;

namespace UnboxedLife;

public static class PawnResolver
{
	// Owning client: find the local pawn (works across respawns)
	public static GameObject GetLocalPawn( Scene scene )
	{
		return scene.GetAllObjects( true ).FirstOrDefault( go =>
			go.Network?.IsOwner == true &&
			go.Components.Get<Sandbox.PlayerController>() is not null );
	}
}
