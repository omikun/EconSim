﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using System.Linq;
using UnityEngine.XR;
using System;
using Sirenix.Reflection.Editor;
using DG.Tweening;
using UnityEngine.UIElements;

public class EconAgent : MonoBehaviour {
	protected AgentConfig config;
	public static int uid_idx = 0;
	protected int uid;
	public float cash { get; protected set; }
	protected float prevCash;
	protected float foodExpense = 0;
	protected float initCash = 100f;
	protected float initStock = 1;
	protected float maxStock = 1;
	public float Profit { get; protected set; }
	public float TaxableProfit { get; protected set; }
	public Dictionary<string, InventoryItem> inventory = new();
	protected Dictionary<string, float> perItemCost = new();
	float taxesPaidThisRound = 0;
	bool boughtThisRound = false;
	bool soldThisRound = false;
	WaitNumRoundsNotTriggered noSaleIn = new();
	WaitNumRoundsNotTriggered noPurchaseIn = new();

	public string Profession {
		get { return outputNames[0]; }
	}
	public List<string> outputNames { get; protected set; } //can produce commodities
	protected HashSet<string> inputs = new();
	//production has dependencies on commodities->populates stock
	//production rate is limited by assembly lines (queues/event lists)
	
	//can use profit to reinvest - produce new commodities
	//switching cost to produce new commodities - zero for now

	//from the paper (base implementation)
	// Use this for initialization
	protected AuctionBook book {get; set;}
	protected AuctionStats auctionStats;
	protected Dictionary<string, float> producedThisRound = new();
	public float numProducedThisRound = 0;
	protected string log = "";

	public virtual String Stats(String header)
	{
		header += uid.ToString() + ", " + outputNames[0] + ", "; //profession
		foreach (var stock in inventory)
		{
			log += stock.Value.Stats(header);
		}
		log += header + "cash, stock, " + cash + ", n/a\n";
		log += header + "profit, stock, " + Profit + ", n/a\n";
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
	protected void AddToInventory(string name, float num, float max, float price, float production)
	{
		if (inventory.ContainsKey(name))
			return;

        inventory.Add(name, new InventoryItem(auctionStats, name, num, max, price, production));

		perItemCost[name] = book[name].marketPrice * num;
	}
	public float PayWealthTax(float amountExempt, float taxRate)
	{
		var taxableAmount = cash - amountExempt;
		if (taxableAmount <= 0)
			return 0f;
		var tax = taxableAmount * taxRate;
		cash -= tax;
		taxesPaidThisRound = tax;
		return tax;
	}
	public void Pay(float amount)
	{
		cash -= amount;
	}
	public virtual void Init(AgentConfig cfg, AuctionStats at, float _initCash, List<string> b, float _initStock, float maxstock) {
		config = cfg;
		uid = uid_idx++;
		initStock = _initStock;
		initCash = _initCash;
		maxStock = maxstock;

		book = at.book;
		auctionStats = at;
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
				AddToInventory(commodity, initStock, maxStock, book[commodity].marketPrice, book[commodity].production);
			}
			AddToInventory(buildable, 0, maxStock, book[buildable].marketPrice, book[buildable].production);
			Debug.Log("New " + gameObject.name + " has " + inventory[buildable].Quantity + " " + buildable);
		}
    }
	public void Reinit(float initCash, List<string> buildables, Government gov = null)
	{
		//TODO reinit book and auctionStats? 
		//list of commodities self can produce
		//get initial stockpiles
		outputNames = buildables;
		if (initCash != -1f)
		{
			cash = initCash;
		}
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
			
			if (gov != null)
			{
				gov.Welfare(this);
			} else {
				inventory["Food"].Increase(2); //spawn food only at start of game
			}
			foreach (var dep in output.recipe)
			{
				var com = book[dep.Key];
				inputs.Add(com.name);
				AddToInventory(com.name, 0, maxStock, com.marketPrice, com.production);
			}
			AddToInventory(outputName, 0, maxStock, output.marketPrice, output.production);
			
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
			Debug.Log(auctionStats.round + ": " + name + " " + label + " reinit2: " + msg );
	}

	public float TaxProfit(float taxRate)
	{
		if (TaxableProfit <= 0)
			return 0;
		var taxAmt = TaxableProfit * taxRate;
		cash -= taxAmt;
		return TaxableProfit - taxAmt;
	}
	//want to control when profit gets calculated in round
	public void CalculateProfit()
	{
		var prevProfit = Profit;
		Profit = cash - prevCash;
		prevCash = cash;
		if (prevProfit < 0)
		{
			Profit += prevProfit;
		}
	}
	const float bankruptcyThreshold = 0;
	public bool IsBankrupt()
	{
		return cash < bankruptcyThreshold;
	}
    public virtual float Tick(Government gov, ref bool changedProfession, ref bool bankrupted, ref bool starving)
	{
		Debug.Log("agents ticking!");
		float taxConsumed = 0;

		if (config.foodConsumption && inventory.ContainsKey("Food"))
		{
			var food = inventory["Food"];

			var foodConsumed = config.foodConsumptionRate;
			if (config.useFoodConsumptionCurve)
				foodConsumed = config.foodConsumptionCurve.Evaluate(food.Quantity/food.maxQuantity);
			if (food.Quantity >= foodConsumed)
			{
				var foodAmount = food.Decrease(foodConsumed);
				foodExpense += food.cost * foodConsumed;
				Debug.Log(auctionStats.round + ": " + name + "consumed " + foodConsumed.ToString("n2") + " food expense " + foodExpense);
			}
			starving = food.Quantity <= 0.1f;
		}
		
		foreach (var entry in inventory)
		{
			entry.Value.Tick();
        }


		//ClearRoundStats();

		bool changeProfessionAfterNRounds =  (config.earlyProfessionChange && (noSaleIn.Count() >= config.changeProfessionAfterNDays));
		changedProfession = (config.declareBankruptcy && IsBankrupt()) || (config.starvation && starving);
		bankrupted = IsBankrupt();
        if (config.changeProfession && (changedProfession || changeProfessionAfterNRounds))
		{
			Debug.Log(auctionStats.round + " " + name + " producing " + outputNames[0] + " is bankrupt: " + cash.ToString("c2") 
				+ " or starving where food=" + inventory["Food"].Quantity
				+ " or " + config.changeProfessionAfterNDays + " days no sell");
			bool resetCash = changedProfession;
			//gov absorbs debt or cash on change profession
			//probably should be more complex than this
			//like agent takes out a loan, if after a certain point can declare bankruptcy and get out of debt
				//this only makes sense if there is high demand and supply of inputs exist
				//if high demand but no supply, change role to supplier??
			//gov can hand out food if starving
			//change jobs when not profitable, 
			//these 3 things can be separate events instead of rolled into one
			taxConsumed += ChangeProfession(gov, resetCash); 
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
	public float Food()
	{
		return inventory["Food"].Quantity;
	}
	public virtual float EvaluateHappiness()
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

	float ChangeProfession(Government gov, bool resetCash = true)
	{
		string bestGood = auctionStats.GetHottestGood();
		float profit = 0f;
		string mostDemand = auctionStats.GetMostProfitableProfession(ref profit, Profession);

		Assert.AreEqual(outputNames.Count, 1);
		if (bestGood != "invalid")
        {
            mostDemand = bestGood;
		}
		Debug.Log(auctionStats.round + " " + name + " changing from " + Profession + " to " + mostDemand + " --  bestGood: " + bestGood + " bestProfession: " + mostDemand);
		
		List<string> b = new List<string>();
		if (mostDemand != "invalid")
			b.Add(mostDemand);
		else
			b.Add(Profession);

		if (config.clearInventory) 
		{
			inventory.Clear();
		}
		var existingCash = cash;
		var rc = (resetCash) ? initCash : 0;
		Reinit(rc, b, gov);
		//return amount of money taken from gov
		return rc - existingCash;
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
		if (this is Government)
		{
			Debug.Log(auctionStats.round + " gov buying " + quantity.ToString("n0") + " " + commodity);
		}
		Assert.IsFalse(outputNames.Contains(commodity)); //agents shouldn't buy what they produce
		var boughtQuantity = inventory[commodity].Buy(quantity, price);
		Debug.Log(name + " has " + cash.ToString("c2") 
			+ " want to buy " + quantity.ToString("n2") + " " + commodity 
			+ " for " + price.ToString("c2") + " bought " + boughtQuantity.ToString("n2"));
		cash -= price * boughtQuantity;
		boughtThisRound = true;
		return boughtQuantity;
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
		// Debug.Log(name + " has " + cash.ToString("c2") 
		// 	+ " selling " + quantity.ToString("n2") +" " +  commodity 
		// 	+ " for " + price.ToString("c2"));
		//reset food consumption count?
		soldThisRound = true;
		foodExpense = Mathf.Max(0, foodExpense - Mathf.Max(0,Profit));
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
    protected virtual float FindSellCount(string c)
	{
		var numAsks = inventory[c].FindSellCount(book[c], config.historySize, config.enablePriceFavorability);

		//leave some to eat if food
		if (c == "Food" && config.foodConsumption)
		{
			numAsks = Mathf.Min(numAsks, Mathf.Max(0,inventory[c].Surplus() - 1));
		}

		return numAsks;
	}
	public virtual Offers Consume(AuctionBook book) {
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
			if (config.baselineAuction)
			{
				buyPrice = book[item.name].marketPrice;
				buyPrice *= UnityEngine.Random.Range(.97f, 1.03f);
				buyPrice = Mathf.Max(buyPrice, .01f);
			}
			if (config.sanityCheckBuyPrice)
			{
				buyPrice = book[item.name].setPrice;
			}
			if (config.sanityCheckBuyQuant)
			{
				foreach (var dep in book[Profession].recipe)
				{
					if (dep.Key == item.name)
					{
						var numNeeded = dep.Value;
						numBids = numNeeded * inventory[Profession].GetProductionRate() * 2 ;
						break;
					}
				}
			}
			Debug.Log(auctionStats.round + " " + name + " bidding " + buyPrice.ToString("c2") + " for " + numBids.ToString("n2") + " " + item.name);
			if (config.onlyBuyWhatsAffordable)	//TODO this only accounts for 1 com, what about others?
				//buyPrice = Mathf.Min(cash / numBids, buyPrice);
				numBids = (float)(int)Mathf.Min(cash/buyPrice, numBids);
			Assert.IsTrue(buyPrice > 0);
			Assert.IsTrue(numBids > 0);

			bids.Add(item.name, new Offer(item.name, buyPrice, numBids, this));
			item.bidPrice = buyPrice;
			item.bidQuantity += numBids;

			//debug and sanity check
			if (buyPrice > 1000)
			{
				Debug.Log(item.name + "buyPrice: " + buyPrice.ToString("c2") 
					+ " : " + item.minPriceBelief.ToString("n2") 
					+ "<" + item.maxPriceBelief.ToString("n2"));
				//Assert.IsFalse(buyPrice > 1000);
			}
			Debug.Log(auctionStats.round + ": " + this.name 
				+ " wants to buy " + numBids.ToString("n2") + item.name 
				+ " for " + buyPrice.ToString("c2") 
				+ " each min/maxPriceBeliefs " + item.minPriceBelief.ToString("c2") 
				+ "/" + item.maxPriceBelief.ToString("c2"));
			Assert.IsFalse(numBids < 0);
		}
        return bids;
	}
	public float CalcMinProduction()
	{
		float minTotalProduced = float.MaxValue; 
		foreach (var outputName in outputNames)
		{
			Assert.IsTrue(book.ContainsKey(outputName));
			var com = book[outputName];
			var stock = inventory[outputName];
			var numProduced = CalculateNumProduced(com, stock);
			minTotalProduced = Mathf.Min(minTotalProduced, numProduced);

		}
		return minTotalProduced;
	}
	public virtual float Produce() {
		foreach (var outputName in outputNames)
		{
			if (!book.ContainsKey(outputName))
			{
				Debug.Log(auctionStats.round + " " + name + " not valid output " + outputName);
			}
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

			Debug.Log(auctionStats.round + " " + name 
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
	protected float CalculateNumProduced(ResourceController rsc, InventoryItem item)
	{
		float numProduced = item.Deficit(); //can't produce more than max stock
		//find max that can be made w/ available stock
		foreach (var dep in rsc.recipe)
		{
			var numNeeded = dep.Value;
			var numAvail = inventory[dep.Key].Quantity;
			numProduced = Mathf.Min(numProduced, numAvail / numNeeded);
			Debug.Log(auctionStats.round + " " + name 
				+ "can produce " + numProduced 
				+ " w/" + numAvail + "/" + numNeeded + " " + dep.Key);
		}
		//can only build fixed rate at a time
		numProduced = Mathf.Clamp(numProduced, 0, item.GetProductionRate());
		numProduced = Mathf.Floor(numProduced);

		Debug.Log(auctionStats.round + " " + name 
			+ " producing " + rsc.name 
			+ " currently in stock " + item.Quantity 
			+ " production rate: " + item.GetProductionRate() 
			+ " produced: " + numProduced
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
	
    protected float GetCostOf(ResourceController rsc)
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

	// public virtual GetSellPrice()
	// {
	// 	var baseSellPrice = book[commodityName].price;
	// 	baseSellPrice *= UnityEngine.Random.Range(.97f, 1.03f);
	// 	sellPrice = Mathf.Max(sellPrice, baseSellPrice);
	// }

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
			if (config.baselineAuction)
			{
				var baseSellPrice = book[commodityName].marketPrice;
				var delta = .03f;
				var min = 1f - delta;
				var max = 1f + delta;
				baseSellPrice *= UnityEngine.Random.Range(min, max);
				sellPrice = baseSellPrice;
				//sellPrice = Mathf.Max(sellPrice*min, baseSellPrice);
			}
			if (config.sanityCheckSellPrice)
			{
				sellPrice = book[commodityName].setPrice;
			}
			if (config.sanityCheckSellQuant)
			{
				sellQuantity = item.Value.GetProductionRate() * 2;
				sellQuantity = Mathf.Min(sellQuantity, inventory[commodityName].Surplus());
			}

			if (sellQuantity > 0 && sellPrice > 0)
			{
				Debug.Log(auctionStats.round + ": " + name 
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
