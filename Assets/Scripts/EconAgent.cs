using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using System.Linq;
using UnityEngine.XR;
using System;

public class EconAgent : MonoBehaviour {
	AgentConfig config;
	static int uid_idx = 0;
	int uid;
	public float cash { get; private set; }
	float initCash = 100f;
	float initStock = 1;
	float prevCash = 0;
	float maxStock = 1;
	ESList profits = new ESList();
	public Dictionary<string, InventoryItem> inventory = new Dictionary<string, InventoryItem>(); 
	Dictionary<string, float> perItemCost = new Dictionary<string, float>(); //commodities stockpiled
	float taxesPaidThisRound = 0;

	public List<string> outputs { get; private set; } //can produce commodities
	HashSet<string> inputs = new HashSet<string>();
	//production has dependencies on commodities->populates stock
	//production rate is limited by assembly lines (queues/event lists)
	
	//can use profit to reinvest - produce new commodities
	//switching cost to produce new commodities - zero for now

	//from the paper (base implementation)
	// Use this for initialization
	Dictionary<string, Commodity> com {get; set;}

	public String Stats(String header)
	{
		String msg = "";
		header += uid.ToString() + ", " + outputs[0] + ", "; //profession
		foreach (var stock in inventory)
		{
			msg += stock.Value.Stats(header);
		}
		msg += header + "cash, stock, " + cash + ", n/a\n";
		msg += header + "profit, stock, " + (cash - prevCash) + ", n/a\n";
		msg += header + "taxes, idle, " + taxesPaidThisRound + ", n/a\n";
		return msg; 
	}
	void Start () {
	}
	void AddToInventory(string name, float num, float max, float price, float production)
	{
		if (inventory.ContainsKey(name))
			return;

        inventory.Add(name, new InventoryItem(name, num, max, price, production));

		perItemCost[name] = com[name].price * num;
	}
	public float PayTax(float taxRate)
	{
		var idleTax = cash * taxRate;
		cash -= idleTax;
		taxesPaidThisRound = idleTax;
		return idleTax;
	}
	public void Init(AgentConfig cfg, float _initCash, List<string> b, float _initStock, float maxstock) {
		config = cfg;
		uid = uid_idx++;
		initStock = _initStock;
		initCash = _initCash;
		maxStock = maxstock;

        if (com == null)
			com = AuctionStats.Instance.book;
		//list of commodities self can produce
		//get initial stockpiles
		outputs = b;
		cash = initCash;
		prevCash = cash;
		inputs.Clear();
		foreach (var buildable in outputs)
		{

			if (!com.ContainsKey(buildable))
				Debug.Log("commodity not recognized: " + buildable);

            if (com[buildable].dep == null)
				Debug.Log(buildable + ": null dep!");

			foreach (var dep in com[buildable].dep)
			{
				var commodity = dep.Key;
				inputs.Add(commodity);
                //Debug.Log("::" + commodity);
				AddToInventory(commodity, initStock, maxStock, com[commodity].price, com[commodity].production);
			}
			AddToInventory(buildable, 0, maxStock, com[buildable].price, com[buildable].production);
			Debug.Log("New " + gameObject.name + " has " + inventory[buildable].Quantity + " " + buildable);
		}
    }
	public void Reinit(float initCash, List<string> b)
	{
        if (com == null)
			com = AuctionStats.Instance.book;
		//list of commodities self can produce
		//get initial stockpiles
		outputs = b;
		cash = initCash;
		prevCash = cash;
		inputs.Clear();
		foreach (var buildable in outputs)
		{

			if (!com.ContainsKey(buildable))
				Debug.Log("commodity not recognized: " + buildable);

            if (com[buildable].dep == null)
				Debug.Log(buildable + ": null dep!");

			string msg = "";
			foreach (var entry in inventory)
			{
				msg += entry.Value.Quantity + " " + entry.Key + ", ";
			}
			Debug.Log(AuctionStats.Instance.round + ": " + name + " reinit2: " + msg );
			foreach (var dep in com[buildable].dep)
			{
				var commodity = dep.Key;
				inputs.Add(commodity);
                //Debug.Log("::" + commodity);
				AddToInventory(commodity, initStock, maxStock, com[commodity].price, com[commodity].production);
			}
			AddToInventory(buildable, 0, maxStock, com[buildable].price, com[buildable].production);
			//Debug.Log("New " + gameObject.name + " has " + inventory[buildable].Quantity + " " + buildable);
			Debug.Log(AuctionStats.Instance.round + ": " + name + " post reinit2: " + msg );
		}
    }

	public float TaxProfit(float taxRate)
	{
		var profit = GetProfit();
		if (profit <= 0)
			return profit;
		var taxAmt = profit * taxRate;
		cash -= taxAmt;
		return profit - taxAmt;
	}
	public float GetProfit()
	{
		var profit = cash - prevCash;
		prevCash = cash;
		return profit;
	}
	const float bankruptcyThreshold = 0;
	public bool IsBankrupt()
	{
		return cash < bankruptcyThreshold;
	}
    public float Tick()
	{
		Debug.Log("agents ticking!");
		float taxConsumed = 0;

		bool starving = false;
		if (config.foodConsumption && inventory.ContainsKey("Food"))
		{
			if (inventory["Food"].Quantity >= config.foodConsumptionRate)
			{
				var food = inventory["Food"].Decrease(0.5f);
				starving = food <= 0;
			}
		}
		
		foreach (var entry in inventory)
		{
			entry.Value.Tick();
        }

        if (IsBankrupt() || (config.starvation && starving))
		{
			Debug.Log(name + " producing " + outputs[0] + " is bankrupt: " + cash.ToString("c2") + " or starving where food=" + inventory["Food"].Quantity);
			taxConsumed = -cash + ChangeProfession(); //existing debt + 
		}
		foreach (var buildable in outputs)
		{
			inventory[buildable].cost = GetCostOf(buildable);
		}
		return taxConsumed;
	}

	float ChangeProfession()
	{
		string bestGood = AuctionStats.Instance.GetHottestGood();
		string bestProf = AuctionStats.Instance.GetMostProfitableProfession(outputs[0]);

		string mostDemand = bestProf;
		Debug.Log("bestGood: " + bestGood + " bestProfession: " + bestProf);
		Assert.AreEqual(outputs.Count, 1);
		Debug.Log(name + " changing from " + outputs[0] + " to " + mostDemand);

		if (bestGood != "invalid")
        {
            mostDemand = bestGood;
			outputs[0] = mostDemand;
		}
				
		if (config.clearInventory) 
		{
			inventory.Clear();
		}
		List<string> b = new List<string>();
		b.Add(mostDemand);
		Reinit(initCash, b);
		return initCash;
	}

	/*********** Trading ************/
	public void modify_cash(float quant)
	{
		cash += quant;
	}
	public void ClearRoundStats()
	{
		foreach( var item in inventory)
		{
			item.Value.ClearRoundStats();
		}
		taxesPaidThisRound = 0;
	}
	public float Buy(string commodity, float quantity, float price)
	{
		Assert.IsFalse(outputs.Contains(commodity)); //agents shouldn't buy what they produce
		var boughtQuantity = inventory[commodity].Buy(quantity, price);
		Debug.Log(name + " has " + cash.ToString("c2") + " want to buy " + quantity.ToString("n2") + " " + commodity + " for " + price.ToString("c2") + " bought " + boughtQuantity.ToString("n2"));
		cash -= price * boughtQuantity;
		return boughtQuantity;
	}
	public void Sell(string commodity, float quantity, float price)
	{
		Assert.IsTrue(inventory[commodity].Quantity >= 0);
		inventory[commodity].Sell(quantity, price);
		Assert.IsTrue(inventory[commodity].Quantity >= 0);
		//		Debug.Log(name + " has " + cash.ToString("c2") + " selling " + quantity.ToString("n2") +" " +  commodity + " for " + price.ToString("c2"));
		cash += price * quantity;
	}
	public void UpdateSellerPriceBelief(in Offer trade, in Commodity commodity) 
	{
		inventory[commodity.name].UpdateSellerPriceBelief(name, in trade, in commodity);
	}
	public void UpdateBuyerPriceBelief(in Offer trade, in Commodity commodity) 
	{
		inventory[commodity.name].UpdateBuyerPriceBelief(name, in trade, in commodity);
	}

    /*********** Produce and consume; enter asks and bids to auction house *****/
    float FindSellCount(string c)
	{
		if (inventory[c].Surplus() < 1)
		{
			return 0;
		}

		float numAsks = Mathf.Floor(inventory[c].Surplus());
		if (config.enablePriceFavorability) {
			var avgPrice = com[c].avgBidPrice.LastAverage(config.historySize);
			var lowestPrice = inventory[c].sellHistory.Min();
			var highestPrice = inventory[c].sellHistory.Max();
			float favorability = .5f;
			if (true || lowestPrice != highestPrice)
			{
				favorability = Mathf.InverseLerp(lowestPrice, highestPrice, avgPrice);
				favorability = Mathf.Clamp(favorability, 0, 1);
			}
			//sell at least 1
			numAsks = Mathf.Max(1, favorability * inventory[c].Surplus());
			//leave some to eat if food
			if (c == "Food" && config.foodConsumption)
			{
				numAsks = Mathf.Min(numAsks, inventory[c].Surplus() - 1);
			}
			numAsks = Mathf.Floor(numAsks);

			Debug.Log(AuctionStats.Instance.round + " " + name + " FindSellCount " + c + ": avgPrice: " + avgPrice.ToString("c2") + " favorability: " + favorability.ToString("n2") + " numAsks: " + numAsks.ToString("n2") + " highestPrice: " + highestPrice.ToString("c2") + ", lowestPrice: " + lowestPrice.ToString("c2"));
			Assert.IsTrue(numAsks <= inventory[c].Quantity);
		}
		return numAsks;
	}
	float FindBuyCount(string c)
	{
		float numBids = Mathf.Floor(inventory[c].Deficit());
		if (config.enablePriceFavorability)
		{
			var avgPrice = com[c].avgBidPrice.LastAverage(config.historySize);
			var lowestPrice = inventory[c].buyHistory.Min();
			var highestPrice = inventory[c].buyHistory.Max();
			//todo SANITY check
			float favorability = .5f;
			if (lowestPrice != highestPrice)
			{
				favorability = Mathf.InverseLerp(lowestPrice, highestPrice, avgPrice);
				favorability = Mathf.Clamp(favorability, 0, 1);
			}
			//float favorability = FindTradeFavorability(c);
			numBids = (1 - favorability) * inventory[c].Deficit();
			numBids = Mathf.Floor(numBids);
			numBids = Mathf.Max(0, numBids);

			Debug.Log(AuctionStats.Instance.round + " " + name + " FindBuyCount " + c + ": avgPrice: " + avgPrice.ToString("c2") + " favorability: " + (1 - favorability).ToString("n2") + " numBids: " + numBids.ToString("n2") + " highestPrice: " + highestPrice.ToString("c2") + ", lowestPrice: " + lowestPrice.ToString("c2"));
			Assert.IsTrue(numBids <= inventory[c].Deficit());
		}
		return numBids;
	}
	public Offers Consume(Dictionary<string, Commodity> com) {
        var bids = new Offers();
        //replenish depended commodities
        foreach (var stock in inventory)
		{
			if (outputs.Contains(stock.Key)) continue;
			if (!inputs.Contains(stock.Key)) continue;

			var numBids = FindBuyCount(stock.Key);
			if (numBids > 0 && cash > 0)
			{
				//maybe buy less if expensive?
				float buyPrice = stock.Value.GetPrice();
				if (config.onlyBuyWhatsAffordable)
				{
					buyPrice = Mathf.Min(cash / numBids, buyPrice);
				}
				if (config.buyerBuysAskPrice)
				{
					buyPrice = 0;
				} else {
					Assert.IsTrue(buyPrice > 0);
				}
				if (buyPrice > 1000)
				{
					Debug.Log(stock.Key + "buyPrice: " + buyPrice.ToString("c2") + " : " + stock.Value.minPriceBelief.ToString("n2") + "<" + stock.Value.maxPriceBelief.ToString("n2"));
					Assert.IsFalse(buyPrice > 1000);
				}
				Debug.Log(AuctionStats.Instance.round + ": " + this.name + " wants to buy " + numBids.ToString("n2") + stock.Key + " for " + buyPrice.ToString("c2") + " each" + " min/maxPriceBeliefs " + stock.Value.minPriceBelief.ToString("c2") + "/" + stock.Value.maxPriceBelief.ToString("c2"));
				Assert.IsFalse(numBids < 0);
				bids.Add(stock.Key, new Offer(stock.Value.commodityName, buyPrice, numBids, this));
				stock.Value.bidPrice = buyPrice;
				stock.Value.bidQuantity += numBids;

			}
        }
        return bids;
	}
	public float Produce(Dictionary<string, Commodity> com) {
		//TODO sort buildables by profit

		//build as many as one can TODO don't build things that won't earn a profit
		float producedThisRound = 0;
		foreach (var buildable in outputs)
		{
			Debug.Log(name + " producing " + buildable + " currently in stock " + inventory[buildable].Quantity);
			//get list of dependent commodities
			float numProduced = float.MaxValue; //amt agent can produce for commodity buildable
			//find max that can be made w/ available stock
			Assert.IsTrue(com.ContainsKey(buildable));
			foreach (var dep in com[buildable].dep)
			{
				var numNeeded = dep.Value;
				var numAvail = inventory[dep.Key].Quantity;
				numProduced = Mathf.Min(numProduced, numAvail/numNeeded);
				Debug.Log(name + "can produce " + numProduced + " w/" + numAvail + "/" + numNeeded + dep.Key );
			}
			//can only build fixed rate at a time
			//can't produce more than what's in stock
			var upperBound = Mathf.Min(inventory[buildable].productionRate, inventory[buildable].Deficit());
			numProduced = Mathf.Clamp(numProduced, 0, upperBound);
			Debug.Log(name + " upperbound: " + upperBound + " production rate: " + inventory[buildable].productionRate + " room: " + inventory[buildable].Deficit());
			numProduced = Mathf.Floor(numProduced);
			Debug.Log(name + " upperbound: " + upperBound + " producing: " + numProduced);
			Assert.IsTrue(numProduced >= 0);

			//build and add to stockpile
			var buildable_cost = 0f;
			foreach (var dep in com[buildable].dep)
			{
				var stock = inventory[dep.Key].Quantity;
				var numUsed = dep.Value * numProduced;
				numUsed = Mathf.Clamp(numUsed, 0, stock);
				buildable_cost += numUsed * inventory[dep.Key].meanCostThisRound;
                inventory[dep.Key].Decrease(numUsed);
			}
			inventory[buildable].Increase(numProduced);
			Debug.Log(name + " has " + cash.ToString("c2") + " made " + numProduced.ToString("n2") + " " + buildable + " total: " + inventory[buildable].Quantity);
			Assert.IsFalse(float.IsNaN(numProduced));
			//this condition is worrisome 
//			Assert.IsTrue(inventory[buildable].Quantity >= 0);

			//create ask outside

			producedThisRound += numProduced;
		}
		return producedThisRound;

	}

	public Offers CreateAsks()
	{
		//sell everything not needed by output
        var asks = new Offers();

		foreach (var item in inventory)
		{
			var commodityName = item.Key;
			if (inputs.Contains(commodityName))
			{
				continue;
			}
			var sellStock = inventory[commodityName];
			float sellQuantity = FindSellCount(commodityName);
			//float sellPrice = sellStock.GetPrice();
			float sellPrice = inventory[commodityName].cost * config.profitMarkup;

			if (sellQuantity > 0 && sellPrice > 0)
			{
				Debug.Log(AuctionStats.Instance.round + ": " + name + " wants to sell " + sellQuantity + " " + commodityName + " for " + sellPrice.ToString("c2") + ", has in stock" + inventory[commodityName].Surplus());
				Assert.IsTrue(sellQuantity <= inventory[commodityName].Quantity);
				asks.Add(commodityName, new Offer(commodityName, sellPrice, sellQuantity, this));
				sellStock.askPrice = sellPrice;
				sellStock.askQuantity = sellQuantity;
			}
		}
		return asks;
	}
    //get the cost of a commodity
    float GetCostOf(string commodity)
	{
		var com = AuctionStats.Instance.book;
		float cost = 0;
		foreach (var dep in com[commodity].dep)
		{
			var depCommodity = dep.Key;
			var numDep = dep.Value;
			var depCost = inventory[depCommodity].meanCost;
			cost += numDep * depCost;
		}
		return cost;
	}
	// Update is called once per frame
	void Update () {
	}
}
