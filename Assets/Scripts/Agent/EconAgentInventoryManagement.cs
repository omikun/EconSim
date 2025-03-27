using UnityEngine;
using UnityEngine.Assertions;

public partial class EconAgent
{
    protected float initStock = 1;
    protected float maxStock = 1;
    public Inventory inventory = new();

    protected bool isSellable(string itemName)
    {
        return !inRecipe(itemName);
    }

    protected bool isConsumable(string itemName)
    {
        return inRecipe(itemName) || (outputName != "Food" && itemName == "Food");
    }

    protected bool inRecipe(string itemName)
    {
        if (book.ContainsKey(outputName) == false)
            return false;
        return (book[outputName].recipe.ContainsKey(itemName));
    }

    public void PrintInventory(string label)
    {
        string msg = "";
        foreach (var entry in inventory)
        {
            msg += entry.Value.Quantity + " " + entry.Key + ", ";
        }

        Debug.Log(auctionStats.round + ": " + name + " " + label + " reinit2: " + msg + " cash: " + CashString);
    }

    public float Buy(string commodity, float quantity, float price)
    {
        if (this is Government)
        {
            Debug.Log(auctionStats.round + " gov buying " + quantity.ToString("n0") + " " + commodity);
        }

        Assert.IsTrue(quantity > 0);

        inventory[commodity].Buy(quantity, price);
        Cash -= price * quantity;
        Debug.Log(name + " has " + Cash.ToString("c2")
                  + " after wanting to buy " + quantity.ToString("n2") + " " + commodity
                  + " for " + price.ToString("c2") + " and bought " + quantity.ToString("n2"));
        Assert.IsFalse(outputName.Contains(commodity), name + " buying own output: " + outputName);
        return quantity;
    }

    public virtual void Sell(string commodity, float quantity, float price)
    {
        if (this is Government)
        {
            Debug.Log(auctionStats.round + " gov selling " + quantity.ToString("n0") + " " + commodity);
        }

        Assert.IsTrue(inventory[commodity].Quantity >= 0);
        inventory[commodity].Sell(quantity, price);
        Assert.IsTrue(inventory[commodity].Quantity >= 0);
        Cash += price * quantity;
        Debug.Log(name + " has " + Cash.ToString("c2")
                  + " after asking " + quantity.ToString("n2") + " " + commodity
                  + " for " + price.ToString("c2") + " and sold " + quantity.ToString("n2"));
    }

    public void UpdateSellerPriceBelief(in Offer trade, in ResourceController rsc)
    {
        inventory[rsc.name].UpdateSellerPriceBelief(name, in trade, in rsc);
    }

    public void UpdateBuyerPriceBelief(in Offer trade, in ResourceController rsc)
    {
        inventory[rsc.name].UpdateBuyerPriceBelief(name, in trade, in rsc);
    }

    protected void AddToInventory(string name, float num, float max, ResourceController rsc)
    {
        if (inventory.ContainsKey(name))
            return;

        inventory.Add(name, new InventoryItem(this, auctionStats, name, num, max, rsc));
    }
}