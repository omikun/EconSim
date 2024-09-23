using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using System.Linq;
using UnityEngine.XR;
using System;
using Sirenix.Reflection.Editor;
using DG.Tweening;

public class EconAgent : MonoBehaviour {
	AgentConfig config;
	static int uid_idx = 0;
	int uid;
	public float cash { get; private set; }
	float prevCash;
	float foodExpense = 0;
	float initCash = 100f;
	float initStock = 1;
	float maxStock = 1;
	ESList profits = new ESList();
	public Dictionary<string, InventoryItem> inventory = new();
	Dictionary<string, float> perItemCost = new();
	float taxesPaidThisRound = 0;
	bool boughtThisRound = false;
	bool soldThisRound = false;
	WaitNumRoundsNotTriggered noSaleIn = new();
	WaitNumRoundsNotTriggered noPurchaseIn = new();

	public List<string> outputNames { get; private set; } //can produce commodities
	HashSet<string> inputs = new();
	//production has dependencies on commodities->populates stock
	//production rate is limited by assembly lines (queues/event lists)
	
	//can use profit to reinvest - produce new commodities
	//switching cost to produce new commodities - zero for now

	//from the paper (base implementation)
	// Use this for initialization
	AuctionBook book {get; set;}
	Dictionary<string, float> producedThisRound = new();
	public float numProducedThisRound = 0;
	string log = "";

	public float Income() 
	{
		return cash - prevCash;
	}

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
		foodExpense = 0;
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
			var food = inventory["Food"];
			foodExpense += food.cost * config.foodConsumptionRate;

			if (food.Quantity >= config.foodConsumptionRate)
			{
				var foodAmount = food.Decrease(config.foodConsumptionRate);
				Debug.Log(AuctionStats.Instance.round + ": " + name + " food expense " + foodExpense);
				starving = foodAmount <= 0;
			}
		}
		
		foreach (var entry in inventory)
		{
			entry.Value.Tick();
        }

		profits.Add(cash - prevCash);

		//ClearRoundStats();

		bool changeProfession =  (config.earlyProfessionChange 
			&& (noSaleIn.Count() >= config.changeProfessionAfterNDays));
		changeProfession |= IsBankrupt() || (config.starvation && starving);
        if (changeProfession)
		{
			Debug.Log(AuctionStats.Instance.round + " " + name + " producing " + outputNames[0] + " is bankrupt: " + cash.ToString("c2") 
				+ " or starving where food=" + inventory["Food"].Quantity
				+ " or " + config.changeProfessionAfterNDays + " days no sell");
			taxConsumed = -cash + ChangeProfession(); //existing debt + 
			noSaleIn.Reset();
			noPurchaseIn.Reset();
		}

		// cost updated on EconAgent::Produce()
		// foreach (var buildable in outputs)
		// {
		// 	inventory[buildable].cost = GetCostOf(buildable);
		// }
		return taxConsumed;
	}
	public AnimationCurve foodToHappy;
	public AnimationCurve cashToHappy;
	public float EvaluateHappiness()
	{
		//if hungry, less happy
		var happy = 1f;
		happy *= foodToHappy.Evaluate(inventory["Food"].Availability());
		//var profitRate = profits.TakeLast(config.historySize).Average();
		//happy *= cashToHappy.Evaluate(profitRate/config.historySize);
		return happy;
		//if close to bankruptcy, maybe not happy
		//if both, really angry
		//if hungry for n days, have chance of dying
		//if really happy, more likely to reproduce
		// do this at the beginning? DOTween.Init(true, true, LogBehaviour.Verbose).SetCapacity()

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
		noSaleIn.Tick();
		noPurchaseIn.Tick();

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
		boughtThisRound = true;
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
		//reset food consumption count?
		soldThisRound = true;
		foodExpense = Mathf.Max(0, foodExpense - Mathf.Max(0,Income()));
		 cash += price * quantity;
	}
	public void UpdateSellerPriceBelief(in Offer trade, in ResourceController rsc) 
	{
		inventory[rsc.name].UpdateSellerPriceBelief(name, in trade, in rsc);
	}
	public void UpdateBuyerPriceBelief(in Offer trade, in ResourceController rsc) 
	{
		inventory[rsc.name].UpdateBuyerPriceBelief(name, in trade, in rsc);
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
	public Offers Consume(AuctionBook book) {
        var bids = new Offers();
		if (cash <= 0)
			return bids;

        //replenish depended commodities
        foreach (var entry in inventory)
		{
			var item = entry.Value;
			if (!inputs.Contains(item.name) || outputNames.Contains(item.name)) 
				continue;

			var numBids = item.FindBuyCount(book[item.name], 
												config.historySize, 
												config.enablePriceFavorability);
			if (numBids <= 0)
				continue;

			//maybe buy less if expensive?
			float buyPrice = item.GetPrice();
			if (config.onlyBuyWhatsAffordable)
				buyPrice = Mathf.Min(cash / numBids, buyPrice);
			Assert.IsTrue(buyPrice > 0);

			bids.Add(item.name, new Offer(item.name, buyPrice, numBids, this));
			item.bidPrice = buyPrice;
			item.bidQuantity += numBids;

			//debug and sanity check
			if (buyPrice > 1000)
			{
				Debug.Log(item.name + "buyPrice: " + buyPrice.ToString("c2") 
					+ " : " + item.minPriceBelief.ToString("n2") 
					+ "<" + item.maxPriceBelief.ToString("n2"));
				Assert.IsFalse(buyPrice > 1000);
			}
			Debug.Log(AuctionStats.Instance.round + ": " + this.name 
				+ " wants to buy " + numBids.ToString("n2") + item.name 
				+ " for " + buyPrice.ToString("c2") 
				+ " each min/maxPriceBeliefs " + item.minPriceBelief.ToString("c2") 
				+ "/" + item.maxPriceBelief.ToString("c2"));
			Assert.IsFalse(numBids < 0);
		}
        return bids;
	}
	public float Produce(AuctionBook book) {
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
		}
		numProducedThisRound = this.producedThisRound.Sum(x => x.Value);
		return numProducedThisRound;
	}
	//build as many as one can 
	//TODO what if don't want to produce as much as one can? what if costs are high rn?
	float CalculateNumProduced(ResourceController rsc, InventoryItem item)
	{
		float numProduced = item.Deficit(); //can't produce more than max stock
		//find max that can be made w/ available stock
		foreach (var dep in rsc.recipe)
		{
			var numNeeded = dep.Value;
			var numAvail = inventory[dep.Key].Quantity;
			numProduced = Mathf.Min(numProduced, numAvail / numNeeded);
			Debug.Log(AuctionStats.Instance.round + " " + name 
				+ "can produce " + numProduced 
				+ " w/" + numAvail + "/" + numNeeded + " " + dep.Key);
		}
		//can only build fixed rate at a time
		numProduced = Mathf.Clamp(numProduced, 0, item.GetProductionRate());
		numProduced = Mathf.Floor(numProduced);

		Debug.Log(AuctionStats.Instance.round + " " + name 
			+ " producing " + rsc.name 
			+ " currently in stock " + item.Quantity 
			+ " production rate: " + item.GetProductionRate() 
			+ " room: " + item.Deficit());
		Assert.IsTrue(numProduced >= 0);
		return numProduced;
	}
	void ConsumeInput(ResourceController rsc, float numProduced, ref string msg)
	{
		foreach (var dep in rsc.recipe)
		{
			var stock = inventory[dep.Key].Quantity;
			var numUsed = dep.Value * numProduced;
			Assert.IsTrue(stock >= numUsed);
			inventory[dep.Key].Decrease(numUsed);
			msg += dep.Key + ": " + inventory[dep.Key].meanCost.ToString("c2");
		}
	}
	
    float GetCostOf(ResourceController rsc)
	{
		float cost = 0;
		foreach (var dep in rsc.recipe)
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
			// + cost of food since last sell
			var expense = Mathf.Max(0, foodExpense);
			float sellPrice = inventory[commodityName].cost * config.profitMarkup + expense;

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
