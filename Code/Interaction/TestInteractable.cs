using Sandbox;

namespace UnboxedLife;

public sealed class TestInteractable : Interactable
{
	public override void Interact( GameObject interactor )
	{
		Log.Info( $"INTERACT: {interactor.Name} used {GameObject.Name}" );
	}
}
