using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using System.Linq;
using UnityEngine.XR;
using System;

public class EconAgent : MonoBehaviour {
	static int uid_idx = 0;
	int uid;
	public float cash { get; private set; }
	float initCash = 100f;
	float initStock = 1;
	float prevCash = 0;
	float maxStock = 1;
	bool starvation = false;
	bool simpleTradeAmountDet = false;
	bool onlyBuyWhatsAffordable = false;
	bool foodConsumption = false;
	ESList profits = new ESList();
	public Dictionary<string, InventoryItem> inventory = new Dictionary<string, InventoryItem>(); 
	Dictionary<string, float> perItemCost = new Dictionary<string, float>(); //commodities stockpiled
	int historyCount = 10;

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
		return msg; 
	}
	void Start () {
		cash = 0;
	}
	void AddToInventory(string name, float num, float max, float price, float production)
	{
		if (inventory.ContainsKey(name))
			return;

        inventory.Add(name, new InventoryItem(name, num, max, price, production));

		perItemCost[name] = com[name].price * num;
	}
	public void Init(float _initCash, List<string> b, float _initStock, float maxstock) {
		uid = uid_idx++;
		initStock = _initStock;
		initCash = _initCash;
		starvation = Commodities.Instance.starvation;
		maxStock = maxstock;
		simpleTradeAmountDet = Commodities.Instance.simpleTradeAmountDet;
		foodConsumption = Commodities.Instance.foodConsumption;
		onlyBuyWhatsAffordable = Commodities.Instance.onlyBuyWhatsAffordable;
		Reinit(initCash, b);
	}
	public void Reinit(float initCash, List<string> b)
	{
        if (com == null)
			com = Commodities.Instance.com;
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
		if (foodConsumption && inventory.ContainsKey("Food"))
		{
			if (inventory["Food"].Quantity >= 0.0f)
			{
				var food = inventory["Food"].Decrease(0.5f);
				starving = food <= 0;
			}
		}
		
		foreach (var entry in inventory)
		{
			entry.Value.Tick();
        }

        if (IsBankrupt() || (starvation && starving))
		{
			Debug.Log(name + ":" + outputs[0] + " is bankrupt: " + cash.ToString("c2") + " or starving where food=" + inventory["Food"].Quantity);
			taxConsumed = ChangeProfession();
		}
		foreach (var buildable in outputs)
		{
			inventory[buildable].cost = GetCostOf(buildable);
		}
		return taxConsumed;
	}

	float ChangeProfession()
	{
		string bestGood = Commodities.Instance.GetHottestGood();
		string bestProf = Commodities.Instance.GetMostProfitableProfession(outputs[0]);

		string mostDemand = bestProf;
		Debug.Log("bestGood: " + bestGood + " bestProfession: " + bestProf);
		if (bestGood != "invalid")
        {
            mostDemand = bestGood;
		} else {
			//no good profession?? probably should assert
			Assert.IsTrue(bestProf != "invalid");
		}
				
		Assert.AreEqual(outputs.Count, 1);
		Debug.Log(name + " changing from " + outputs[0] + " to " + mostDemand);
		outputs[0] = mostDemand;
		//inventory.Clear();
		List<string> b = new List<string>();
		b.Add(mostDemand);
		// todo take from irs?
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
	float FindTradeFavorability(string c)
	{
		//var avgPrice = com[c].GetAvgPrice(historyCount);
		var avgPrice = com[c].avgClearingPrice.LastAverage(historyCount);
		var lowestPrice = inventory[c].sellHistory.Min();
		var highestPrice = inventory[c].sellHistory.Max();
		if (lowestPrice == highestPrice) {
			//no history, default to half
			return .5f;
		}
		//todo SANITY check
		float favorability = Mathf.InverseLerp(lowestPrice, highestPrice, avgPrice);
		return favorability;
	}
    float FindSellCount(string c)
	{
		var avgPrice = com[c].avgClearingPrice.LastAverage(historyCount);
		var lowestPrice = inventory[c].sellHistory.Min();
		var highestPrice = inventory[c].sellHistory.Max();
		float favorability = .5f;
		if (lowestPrice != highestPrice) {
			favorability = Mathf.InverseLerp(lowestPrice, highestPrice, avgPrice);
			favorability = Mathf.Clamp(favorability, 0, 1);
		}
		float numAsks = favorability * inventory[c].Surplus();
		//leave some to eat if food
		numAsks = Mathf.Min(numAsks, inventory[c].Surplus()-1); 
		numAsks = Mathf.Floor(numAsks);
		if (simpleTradeAmountDet) {
			numAsks = inventory[c].Surplus();
		}
		Debug.Log("FindSellCount " + c + ": avgPrice: " + avgPrice.ToString("c2") + " favorability: " + favorability.ToString("n2") + " numAsks: " + numAsks.ToString("n2") + " highestPrice: " + highestPrice.ToString("c2") + ", lowestPrice: " + lowestPrice.ToString("c2"));
		return numAsks;
	}
	float FindBuyCount(string c)
	{
		var avgPrice = com[c].avgClearingPrice.LastAverage(historyCount);
		var lowestPrice = inventory[c].buyHistory.Min();
		var highestPrice = inventory[c].buyHistory.Max();
		//todo SANITY check
		float favorability = .5f;
		if (lowestPrice != highestPrice) {
			favorability = Mathf.InverseLerp(lowestPrice, highestPrice, avgPrice);
			favorability = Mathf.Clamp(favorability, 0, 1);
		}
		//float favorability = FindTradeFavorability(c);
		float numBids = (1 - favorability) * inventory[c].Deficit();
		numBids = Mathf.Floor(numBids);
		if (simpleTradeAmountDet) {
			numBids = inventory[c].Deficit();
		}
		
		Debug.Log("FindBuyCount " + c + ": avgPrice: " + avgPrice.ToString("c2") + " favorability: " + (1-favorability).ToString("n2") + " numBids: " + numBids.ToString("n2") + " highestPrice: " + highestPrice.ToString("c2") + ", lowestPrice: " + lowestPrice.ToString("c2"));
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
				if (onlyBuyWhatsAffordable)
				{
					float maxPrice = Mathf.Min(cash / numBids, stock.Value.GetPrice());
					buyPrice = Mathf.Min(buyPrice, maxPrice);
				}
				Assert.IsTrue(buyPrice > 0);
				if (buyPrice > 1000)
				{
					Debug.Log(stock.Key + "buyPrice: " + buyPrice.ToString("c2") + " : " + stock.Value.minPriceBelief.ToString("n2") + "<" + stock.Value.maxPriceBelief.ToString("n2"));
					Assert.IsFalse(buyPrice > 1000);
				}
				Debug.Log(this.name + " wants to buy " + numBids.ToString("n2") + stock.Key + " for " + buyPrice.ToString("c2") + " each");
				Assert.IsFalse(numBids < 0);
				bids.Add(stock.Key, new Offer(stock.Value.commodityName, buyPrice, numBids, this));
			}
        }
        return bids;
	}
	public void Produce(Dictionary<string, Commodity> com, ref float idleTax) {
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
			Debug.Log(name + " upperbound: " + upperBound + " has produce: " + numProduced);
			Assert.IsTrue(numProduced >= 0);

			//build and add to stockpile
			var buildable_cost = 0f;
			foreach (var dep in com[buildable].dep)
			{
				var stock = inventory[dep.Key].Quantity;
				var numUsed = dep.Value * numProduced;
				numUsed = Mathf.Clamp(numUsed, 0, stock);
				buildable_cost += numUsed * inventory[dep.Key].meanPriceThisRound;
                inventory[dep.Key].Decrease(numUsed);
			}
			Debug.Log(name + " has " + cash.ToString("c2") + " made " + numProduced.ToString("n2") + " " + buildable + " total: " + inventory[buildable].Quantity);
			inventory[buildable].Increase(numProduced);
			Assert.IsFalse(float.IsNaN(numProduced));
			//this condition is worrisome 
//			Assert.IsTrue(inventory[buildable].Quantity >= 0);

			//create ask outside

			producedThisRound += numProduced;
		}

#if false //this is in the paper
		if (producedThisRound > 0 && revenueThisRound > 0)
		{
			idleTax = Mathf.Abs(cash * .05f); 
		}
#endif
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
			float sellPrice = sellStock.GetPrice();

			if (sellQuantity > 0 && sellPrice > 0)
			{
				Debug.Log(name + " wants to sell for " + sellQuantity + " for " + sellPrice.ToString("c2") + ", has in stock" + inventory[commodityName].Surplus());
				asks.Add(commodityName, new Offer(commodityName, sellPrice, sellQuantity, this));
			}
		}
		return asks;
	}
    //get the cost of a commodity
    float GetCostOf(string commodity)
	{
		var com = Commodities.Instance.com;
		float cost = 0;
		foreach (var dep in com[commodity].dep)
		{
			var depCommodity = dep.Key;
			var numDep = dep.Value;
			// TODO take average of stock pile history instead of last price
			var depCost = inventory[depCommodity].buyHistory.Last();
			cost += numDep * depCost.price;
		}
		return cost;
	}
	// Update is called once per frame
	void Update () {
	}
}
