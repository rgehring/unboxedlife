using Sandbox;
using Sandbox.UI;
using UnboxedLife;

public sealed class GameHud : PanelComponent
{
    protected override void OnStart()
    {
        Panel.AddChild<MainHud>(); // MainHud can be a .razor panel or a Panel class
    }
}
