using UnityEngine;
using UnityEngine.Assertions;

public class Consumer
{
    protected EconAgent agent;

    public Consumer(EconAgent a)
    {
        agent = a;
    }

    public virtual float SelectPrice(string com)
    {
	    // float buyPrice = agent.inventory[com].GetPrice();
	    var buyPrice = agent.book[com].marketPrice;
	    var delta = agent.config.buyPriceDelta;
	    var min = 1f - delta;
	    var max = 1f + delta;
	    buyPrice *= UnityEngine.Random.Range(min, max);
	    buyPrice = Mathf.Max(buyPrice, .01f);
	    return buyPrice;
    }

    public virtual float SelectBuyQuantity(string com)
    {
	    var numBids = agent.inventory[com].FindBuyCount(agent.book[com], 
		    agent.config.historySize, 
		    agent.config.enablePriceFavorability);
	    return numBids;
    }

    public virtual Offers Consume(AuctionBook book)
    {
	    var bids = new Offers();
	    if (agent.Cash <= 0)
		    return bids;
	    foreach (var (com, item) in agent.inventory)
	    {
		    if (!agent.inputs.Contains(item.name) || agent.outputName.Contains(item.name)) 
			    continue;
		    CreateBids(book, bids, item);
	    }

	    return bids;
    }

    public void CreateBids(AuctionBook book, Offers bids, InventoryItem item)
    {
	    var numBids = SelectBuyQuantity(item.name);
	    if (numBids <= 0)
		    return;
	    var buyPrice = SelectPrice(item.name);
	    
	    Debug.Log(agent.name + " wants to buy " + numBids + item.name + " for " + buyPrice.ToString(("c2")));
	    bids.Add(item.name, new Offer(item.name, buyPrice, numBids, agent));
	    item.bidPrice = buyPrice;
	    item.bidQuantity += numBids;
	    Assert.IsTrue(buyPrice > 0, "buy price = " + buyPrice);
	    Assert.IsTrue(numBids > 0, "numBids = " + numBids);
    }
}

public class SanityCheckConsumer : Consumer
{
	public SanityCheckConsumer(EconAgent a) : base(a) { }

	public virtual float SelectPrice(string com)
	{
		return agent.book[com].setPrice;
	}
	public override float SelectBuyQuantity(string com)
	{
		float numBids = 0;
		foreach (var dep in agent.book[agent.Profession].recipe)
		{
			if (dep.Key == com)
			{
				var numNeeded = dep.Value;
				numBids = numNeeded * agent.inventory[agent.Profession].GetProductionRate() * agent.config.sanityCheckTradeVolume ;
				break;
			}
		}
		return numBids;
	}
	
}
public class QoLConsumer : Consumer
{
	public QoLConsumer(EconAgent a) : base(a) { }

	public virtual float SelectPrice(string com)
	{
		return agent.book[com].setPrice;
	}
	public override float SelectBuyQuantity(string com)
	{
		float numBids = 0;
		var recipe = agent.book[com].recipe;
		if (recipe.ContainsKey(com))
		{
			var numNeeded = recipe[com];
			numBids = numNeeded; //QoL based on what??
		}
		return numBids;
	}
	
}