using Sandbox;
using System.Linq;

namespace UnboxedLife;

public sealed class JobTerminalInteractable : Interactable
{
	[Property] public JobId SetJobTo { get; set; } = JobId.Citizen;

	public override void Interact( GameObject interactor )
	{
		// Host-only because InteractHost already enforced host. :contentReference[oaicite:2]{index=2}
		var owner = interactor.Network?.Owner;
		if ( owner is null ) return;

		var net = Scene.GetAllComponents<UbxNetwork>().FirstOrDefault();
		net?.SetJobAndRespawn( owner, SetJobTo );
	}

	public override string GetPrompt( GameObject interactor = null )
		=> $"Become {SetJobTo}";
}
