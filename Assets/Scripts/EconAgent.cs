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
	public Dictionary<string, InventoryItem> inventory = new();
	Dictionary<string, float> perItemCost = new();
	float taxesPaidThisRound = 0;

	public List<string> outputNames { get; private set; } //can produce commodities
	HashSet<string> inputs = new();
	//production has dependencies on commodities->populates stock
	//production rate is limited by assembly lines (queues/event lists)
	
	//can use profit to reinvest - produce new commodities
	//switching cost to produce new commodities - zero for now

	//from the paper (base implementation)
	// Use this for initialization
	Dictionary<string, Commodity> book {get; set;}
	Dictionary<string, float> producedThisRound = new();
	string log = "";

	public String Stats(String header)
	{
		header += uid.ToString() + ", " + outputNames[0] + ", "; //profession
		foreach (var stock in inventory)
		{
			log += stock.Value.Stats(header);
		}
		log += header + "cash, stock, " + cash + ", n/a\n";
		log += header + "profit, stock, " + (cash - prevCash) + ", n/a\n";
		log += header + "taxes, idle, " + taxesPaidThisRound + ", n/a\n";
		foreach (var (good, quantity) in producedThisRound)
		{
			log += header + good + ", produced, " + quantity + ", n/a\n";
		}
		producedThisRound.Clear();
		var ret = log;
		log = "";
		return ret; 
	}
	void Start () {
	}
	void AddToInventory(string name, float num, float max, float price, float production)
	{
		if (inventory.ContainsKey(name))
			return;

        inventory.Add(name, new InventoryItem(name, num, max, price, production));

		perItemCost[name] = book[name].price * num;
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

        if (book == null)
			book = AuctionStats.Instance.book;
		//list of commodities self can produce
		//get initial stockpiles
		outputNames = b;
		cash = initCash;
		prevCash = cash;
		inputs.Clear();
		foreach (var buildable in outputNames)
		{

			if (!book.ContainsKey(buildable))
				Debug.Log("commodity not recognized: " + buildable);

            if (book[buildable].recipe == null)
				Debug.Log(buildable + ": null dep!");

			foreach (var dep in book[buildable].recipe)
			{
				var commodity = dep.Key;
				inputs.Add(commodity);
                //Debug.Log("::" + commodity);
				AddToInventory(commodity, initStock, maxStock, book[commodity].price, book[commodity].production);
			}
			AddToInventory(buildable, 0, maxStock, book[buildable].price, book[buildable].production);
			Debug.Log("New " + gameObject.name + " has " + inventory[buildable].Quantity + " " + buildable);
		}
    }
	public void Reinit(float initCash, List<string> buildables)
	{
        if (book == null)
			book = AuctionStats.Instance.book;
		//list of commodities self can produce
		//get initial stockpiles
		outputNames = buildables;
		cash = initCash;
		prevCash = cash;
		inputs.Clear();
		foreach (var outputName in outputNames)
		{

			if (!book.ContainsKey(outputName))
				Debug.Log("commodity not recognized: " + outputName);

			var output = book[outputName];
            if (output.recipe == null)
				Debug.Log(outputName + ": null dep!");

			PrintInventory("before reinit");
			
			inventory["Food"].Increase(2);
			foreach (var dep in output.recipe)
			{
				var com = book[dep.Key];
				inputs.Add(com.name);
				AddToInventory(com.name, 0, maxStock, com.price, com.production);
			}
			AddToInventory(outputName, 0, maxStock, output.price, output.production);
			
			PrintInventory("post reinit");
		}
    }

	public void PrintInventory(string label)
	{
			string msg = "";
			foreach (var entry in inventory)
			{
				msg += entry.Value.Quantity + " " + entry.Key + ", ";
			}
			Debug.Log(AuctionStats.Instance.round + ": " + name + " " + label + " reinit2: " + msg );
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
			Debug.Log(name + " producing " + outputNames[0] + " is bankrupt: " + cash.ToString("c2") 
				+ " or starving where food=" + inventory["Food"].Quantity);
			taxConsumed = -cash + ChangeProfession(); //existing debt + 
		}
		// cost updated on EconAgent::Produce()
		// foreach (var buildable in outputs)
		// {
		// 	inventory[buildable].cost = GetCostOf(buildable);
		// }
		return taxConsumed;
	}

	float ChangeProfession()
	{
		string bestGood = AuctionStats.Instance.GetHottestGood();
		string bestProf = AuctionStats.Instance.GetMostProfitableProfession(outputNames[0]);

		string mostDemand = bestProf;
		Debug.Log("bestGood: " + bestGood + " bestProfession: " + bestProf);
		Assert.AreEqual(outputNames.Count, 1);
		Debug.Log(name + " changing from " + outputNames[0] + " to " + mostDemand);

		if (bestGood != "invalid")
        {
            mostDemand = bestGood;
			outputNames[0] = mostDemand;
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
		Assert.IsFalse(outputNames.Contains(commodity)); //agents shouldn't buy what they produce
		var boughtQuantity = inventory[commodity].Buy(quantity, price);
		Debug.Log(name + " has " + cash.ToString("c2") 
			+ " want to buy " + quantity.ToString("n2") + " " + commodity 
			+ " for " + price.ToString("c2") + " bought " + boughtQuantity.ToString("n2"));
		cash -= price * boughtQuantity;
		return boughtQuantity;
	}
	public void Sell(string commodity, float quantity, float price)
	{
		Assert.IsTrue(inventory[commodity].Quantity >= 0);
		inventory[commodity].Sell(quantity, price);
		Assert.IsTrue(inventory[commodity].Quantity >= 0);
		// Debug.Log(name + " has " + cash.ToString("c2") 
		// 	+ " selling " + quantity.ToString("n2") +" " +  commodity 
		// 	+ " for " + price.ToString("c2"));
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
		var numAsks = inventory[c].FindSellCount(book[c], config.historySize, config.enablePriceFavorability);

		//leave some to eat if food
		if (c == "Food" && config.foodConsumption)
		{
			numAsks = Mathf.Min(numAsks, Mathf.Max(0,inventory[c].Surplus() - 1));
		}

		return numAsks;
	}
	public Offers Consume(Dictionary<string, Commodity> book) {
        var bids = new Offers();
		if (cash <= 0)
			return bids;

        //replenish depended commodities
        foreach (var item in inventory)
		{
			var stock = item.Value;
			if (!inputs.Contains(stock.name) || outputNames.Contains(stock.name)) 
				continue;

			var numBids = stock.FindBuyCount(book[stock.name], 
												config.historySize, 
												config.enablePriceFavorability);
			if (numBids <= 0)
				continue;

			//maybe buy less if expensive?
			float buyPrice = stock.GetPrice();
			if (config.onlyBuyWhatsAffordable)
				buyPrice = Mathf.Min(cash / numBids, buyPrice);
			Assert.IsTrue(buyPrice > 0);

			bids.Add(stock.name, new Offer(stock.name, buyPrice, numBids, this));
			stock.bidPrice = buyPrice;
			stock.bidQuantity += numBids;

			//debug and sanity check
			if (buyPrice > 1000)
			{
				Debug.Log(stock.name + "buyPrice: " + buyPrice.ToString("c2") 
					+ " : " + stock.minPriceBelief.ToString("n2") 
					+ "<" + stock.maxPriceBelief.ToString("n2"));
				Assert.IsFalse(buyPrice > 1000);
			}
			Debug.Log(AuctionStats.Instance.round + ": " + this.name 
				+ " wants to buy " + numBids.ToString("n2") + stock.name 
				+ " for " + buyPrice.ToString("c2") 
				+ " each min/maxPriceBeliefs " + stock.minPriceBelief.ToString("c2") 
				+ "/" + stock.maxPriceBelief.ToString("c2"));
			Assert.IsFalse(numBids < 0);
		}
        return bids;
	}
	public float Produce(Dictionary<string, Commodity> book) {
		float producedThisRound = 0;
		foreach (var outputName in outputNames)
		{
			Assert.IsTrue(book.ContainsKey(outputName));
			var com = book[outputName];
			var stock = inventory[outputName];
			var numProduced = CalculateNumProduced(com, stock);

			var inputCosts = "";
			ConsumeInput(com, numProduced, ref inputCosts);

			//auction wide multiplier (e.g. richer ore vien or forest fire)
			var multiplier = com.productionMultiplier;
			if (numProduced == 0f || multiplier == 0f)
				continue;

			stock.Produced(numProduced * multiplier, numProduced * GetCostOf(com)); 

			Debug.Log(AuctionStats.Instance.round + " " + name 
				+ " has " + cash.ToString("c2") 
				+ " made " + numProduced.ToString("n2") + " " + outputName 
				+ " total: " + stock.Quantity 
				+ " cost: " + stock.cost.ToString("c2") 
				+ inputCosts);
			Assert.IsFalse(float.IsNaN(numProduced));

			this.producedThisRound[outputName] = numProduced;
			producedThisRound += numProduced;
		}
		return producedThisRound;
	}

	void ConsumeInput(Commodity com, float numProduced, ref string msg)
	{
		foreach (var dep in com.recipe)
		{
			var stock = inventory[dep.Key].Quantity;
			var numUsed = dep.Value * numProduced;
			Assert.IsTrue(stock >= numUsed);
			inventory[dep.Key].Decrease(numUsed);
			msg += dep.Key + ": " + inventory[dep.Key].meanCost.ToString("c2");
		}
	}

	//build as many as one can 
	float CalculateNumProduced(Commodity com, InventoryItem inven)
	{
		float numProduced = inven.Deficit(); //can't produce more than max stock
		//find max that can be made w/ available stock
		foreach (var dep in com.recipe)
		{
			var numNeeded = dep.Value;
			var numAvail = inventory[dep.Key].Quantity;
			numProduced = Mathf.Min(numProduced, numAvail / numNeeded);
			Debug.Log(AuctionStats.Instance.round + " " + name 
				+ "can produce " + numProduced 
				+ " w/" + numAvail + "/" + numNeeded + " " + dep.Key);
		}
		//can only build fixed rate at a time
		numProduced = Mathf.Clamp(numProduced, 0, inven.GetProductionRate());
		numProduced = Mathf.Floor(numProduced);

		Debug.Log(AuctionStats.Instance.round + " " + name 
			+ " producing " + com.name 
			+ " currently in stock " + inven.Quantity 
			+ " production rate: " + inven.GetProductionRate() 
			+ " room: " + inven.Deficit());
		Assert.IsTrue(numProduced >= 0);
		return numProduced;
	}
	
    float GetCostOf(Commodity com)
	{
		float cost = 0;
		foreach (var dep in com.recipe)
		{
			var depCommodity = dep.Key;
			var numDep = dep.Value;
			var depCost = inventory[depCommodity].meanCost;
			cost += numDep * depCost;
		}
		return cost;
	}

	public virtual Offers CreateAsks()
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
			var stock = inventory[commodityName];
			float sellQuantity = FindSellCount(commodityName);
			//float sellPrice = sellStock.GetPrice();
			float sellPrice = inventory[commodityName].cost * config.profitMarkup;

			if (sellQuantity > 0 && sellPrice > 0)
			{
				Debug.Log(AuctionStats.Instance.round + ": " + name 
					+ " wants to sell " + sellQuantity + " " + commodityName 
					+ " for " + sellPrice.ToString("c2") 
					+ ", has in stock" + inventory[commodityName].Surplus());
				Assert.IsTrue(sellQuantity <= inventory[commodityName].Quantity);
				asks.Add(commodityName, new Offer(commodityName, sellPrice, sellQuantity, this));
				stock.askPrice = sellPrice;
				stock.askQuantity = sellQuantity;
			}
		}
		return asks;
	}

	void Update () {
	}
}
