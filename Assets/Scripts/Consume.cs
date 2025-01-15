using System.Collections.Generic;
using System.Linq;
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

    public virtual Offers CreateBids(AuctionBook book)
    {
	    var bids = new Offers();
	    if (agent.Cash <= 0)
		    return bids;
	    foreach (var (com, item) in agent.inventory)
	    {
		    if (!agent.inputs.Contains(item.name) || agent.outputName.Contains(item.name)) 
			    continue;
		    CreateBid(book, bids, item);
	    }

	    return bids;
    }

    public void CreateBid(AuctionBook book, Offers bids, InventoryItem item)
    {
	    var numBids = SelectBuyQuantity(item.name);
	    if (numBids <= 0)
		    return;
	    var buyPrice = SelectPrice(item.name);
	    
	    Debug.Log(agent.name + " wants to buy " + numBids + item.name 
	              + " for " + buyPrice.ToString(("c2")));
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
				numBids = numNeeded * agent.inventory[agent.Profession].GetMaxProductionRate() * agent.config.sanityCheckTradeVolume ;
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

	public override Offers CreateBids(AuctionBook book)
	{
	    var bids = new Offers();
	    if (agent.Cash <= 0)
		    return bids;
	    //if not profiting, just buy food if needed
	    var buyInputs = 0f;
	    if (agent.Profit > 0)
	    {
		    //how many days of food left?
		    //get days of food DoF/$
		    //how much money can be earned through selling?
	    }
	    //get ratio of inputs
	    //get how much profit from outputs
	    //get existing utility - num days of food/price
	    //FIXME this only works for farmer (buys non foods) and wood/tool producers that only consume food
	    foreach (var (com, item) in agent.inventory)
	    {
		    if (!agent.inputs.Contains(item.name) || agent.outputName.Contains(item.name)) 
			    continue;
		    if (item.name == "Food")
		    {
			    BuyFood(bids, item);
		    }
		    else
		    {
			    //CreateBids(book, bids, item);
			    BuyNonFood(bids);
			    break;
		    }
	    }

	    return bids;
	}

	void BuyFood(Offers bids, InventoryItem item)
	{
		var foodCom = agent.book["Food"];
		var affordNumFood = agent.Cash/foodCom.marketPrice;
		List<float> numBidsLowInv = new List<float> { 1, 1, 2, 3, 5 };
		List<float> numBidsMidInv = new List<float> { 0, 1, 1, 2, 3 };
		List<float> numBidsHighInv = new List<float> { 0, 0, 1, 1, 2 };
		
		var numBids = numBidsLowInv;
		if (item.Quantity >= 10)
			numBids = numBidsHighInv;
		if (item.Quantity >= 5)
			numBids = numBidsMidInv;
		
		float numBid = numBids[0];
		if (affordNumFood >= 10) numBid = numBids[4];
		if (affordNumFood >= 5)  numBid = numBids[3];
		if (affordNumFood >= 3)  numBid = numBids[2];
		if (affordNumFood >= 2)  numBid = numBids[1];

	    var buyPrice = SelectPrice(item.name);
		bids.Add(item.name, new Offer(item.name, buyPrice, numBid, agent));
		item.bidPrice = buyPrice;
		item.bidQuantity += numBid;
		Assert.IsTrue(buyPrice > 0, "buy price = " + buyPrice);
		Assert.IsTrue(numBid > 0, "numBid = " + numBid);
	}

	void BuyNonFood(Offers bids)
	{
		//assumes no food in inputs
		float totalParts = 0;
		foreach (var (c, it) in agent.inventory)
		{
			if (!agent.inputs.Contains(it.name) || agent.outputName.Contains(it.name))
				continue;
			totalParts += agent.book[agent.outputName].recipe[c];
		}

		//QoL of wood vs tool, 2 wood to 1 tool
		float numBids = 0;
		var recipe = agent.book[agent.outputName].recipe;
		var bestUtility = 0f; //item.value / mktprice
		//find utility of each input and output item from 1 to quantity
		//ex for wood, want to buy wood w/ all money maybe
		//find utility of each additional wood from 1 to affordable quant
		//same for tool, where 1 tool = 2 wood
		//use numNeeded such as log10(n/numNeeded)
		Dictionary<string, List<float>> utilityIndices = new();
		foreach (var (c, it) in agent.inventory)
		{
			if (!agent.inputs.Contains(it.name) || !agent.outputName.Contains(it.name))
				continue;
			//for loop to check how much to sell from 1 to Quantity
			if (recipe.ContainsKey(c))
				continue;
			utilityIndices[c] = GenerateUtilityIndex(c, it, recipe);
			Debug.Log(agent.name + " buynonfood utilities for " + c + " " + utilityIndices);
		}
		//then calculate all possible permutations to find optimal result
		//if item=tool, utility should be > 10food - 2wood
		//think at the margins!
		//for each additional additional purchase of a tool, is it better than 10food - 2wood?
		//if value of 1 tool 2 wood is < value of 10 food
		var totalBidPrice = 0f;
		var increment = 1f;
		var incrementalBidPrice = GetInputCost(recipe);
		while (agent.Cash >= totalBidPrice)
		{
			var inputNiceness = NicenessOf("Tool", recipe, increment) + NicenessOf("Wood", recipe, increment);
			var outputNiceness = NicenessOf("Food", recipe, increment);
			if (inputNiceness > outputNiceness)
				break;
			if (agent.Cash >= totalBidPrice + incrementalBidPrice)
			{
				totalBidPrice += incrementalBidPrice;
				numBids = increment;
			}
			increment += 1f;
		}

		if (numBids > 0)
		{
			var item = agent.inventory["Wood"];
			AddBid(bids, item, numBids, recipe);
			item = agent.inventory["Tool"];
			AddBid(bids, item, numBids, recipe);
		}
	}
	//sums number of each good * their market price to produce 1 batch of output
	float GetInputCost(Recipe recipe)
	{
		float cost = 0f;
		foreach (var (c, it) in agent.inventory)
		{
			if (!agent.inputs.Contains(it.name))
				continue;
			cost += recipe[c] * agent.book[c].marketPrice;
		}

		return cost;
	}
	void AddBid(Offers bids, InventoryItem item, float numBids, Recipe recipe)
	{
		var c = item.name;
		var buyPrice = SelectPrice(c);
		var numNeeded = recipe[c];
		bids.Add(c, new Offer(c, buyPrice, numBids * numNeeded, agent));
		item.bidPrice = buyPrice;
		item.bidQuantity = numBids;
	}

	List<float> GenerateUtilityIndex(string c, InventoryItem item, Recipe recipe)
	{
		var rsc = agent.book[c];
		var marketPrice = rsc.marketPrice;
		var numAffordable = agent.Cash / marketPrice;
		var utilityIndex = Enumerable.Range(1, (int)numAffordable).Select(x => NicenessOf(c, recipe, x)).ToList();
		return utilityIndex;
	}
	float NicenessOf(string c, Recipe recipe, float delta = 1f)
	{
		var marketPrice = agent.book[c].marketPrice;
		var utility = UtilityOf(c, recipe, delta);	
		var niceness = utility / marketPrice;
		return niceness;
	}

	float UtilityOf(string c, Recipe recipe, float delta = 1f)
	{
		var numNeeded = (c == "Food") ? agent.book[c].productionPerBatch : recipe[c];
		var quant = agent.inventory[c].Quantity;
		
		return Mathf.Log10((quant + delta*numNeeded) / quant); 
	}

	public override float SelectBuyQuantity(string com)
	{
		float numBids = 0;
		var recipe = agent.book[agent.outputName].recipe;
		if (recipe.ContainsKey(com))
		{
			var numNeeded = recipe[com];
			numBids = numNeeded; //QoL based on what??
		}

		//buy if below a certain price/qol threshold
		//start with some initial threshold?
		//ex food=1, qol = log(1)/$1
		
		//if food = qol, and everything else is relative to food, or money
		// if can turn this com to output that can be sold, can be turned to money
		//scale out with amount of cash	
		
		numBids = agent.inventory[com].Deficit();
		Debug.Log(agent.name + " QoLConsumer bidding " + numBids + " " + com);
		return numBids;
	}
	
}