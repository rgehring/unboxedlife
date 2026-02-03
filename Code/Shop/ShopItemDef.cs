using Sandbox;

namespace UnboxedLife;

public enum ShopCategory
{
	Devices,
	Food,
	Drink
}

[System.Serializable]
public sealed class ShopItemDef
{
	[Property] public string Id { get; set; } = "item_id";
	[Property] public string Name { get; set; } = "Item";
	[Property] public int Price { get; set; } = 100;
	[Property] public ShopCategory Category { get; set; } = ShopCategory.Devices;

	// Drag a prefab here (same workflow you liked)
	[Property] public GameObject Prefab { get; set; }
}
