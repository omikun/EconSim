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
	public int debug = 0;
	public float cash { get; private set; }
	float prevCash = 0;
	float maxStock = 1;
	ESList profits = new ESList();
	//has a set of commodities in stock
	public Dictionary<string, CommodityStock> stockPile = new Dictionary<string, CommodityStock>(); //commodities stockpiled
	Dictionary<string, float> stockPileCost = new Dictionary<string, float>(); //commodities stockpiled

	//can produce a set of commodities
	public List<string> buildables { get; private set; }
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
		header += uid.ToString() + ", " + buildables[0] + ", "; //profession
		foreach (var stock in stockPile)
		{
			msg += stock.Value.Stats(header);
		}
		msg += header + "cash, stock, " + cash + "\n";
		msg += header + "profit, stock, " + (cash - prevCash) + "\n";
		return msg; 
	}
	void Start () {
		cash = 0;
	}
	void AddToStockPile(string name, float num, float max, float price, float production)
	{
		if (stockPile.ContainsKey(name))
			return;

        stockPile.Add(name, new CommodityStock(name, num, max, price, production));

		//book keeping
		//Debug.Log(gameObject.name + " adding " + name + " to stockpile");
		stockPileCost[name] = com[name].price * num;
	}
	public void Init(float initCash, List<string> b, float initNum=5, float maxstock=10) {
        if (com == null)
			com = Commodities.Instance.com;
		//list of commodities self can produce
		//get initial stockpiles
		uid = uid_idx++;
		buildables = b;
		cash = initCash;
		prevCash = cash;
		maxStock = maxstock;
		foreach (var buildable in buildables)
		{
            Debug.Log("New " + gameObject.name + " has " + cash.ToString("c2") + " builds: " + buildable);

			if (!com.ContainsKey(buildable))
				Debug.Log("commodity not recognized: " + buildable);

            if (com[buildable].dep == null)
				Debug.Log(buildable + ": null dep!");

			foreach (var dep in com[buildable].dep)
			{
				var commodity = dep.Key;
                //Debug.Log("::" + commodity);
				AddToStockPile(commodity, initNum, maxStock, com[commodity].price, com[commodity].production);
			}
			AddToStockPile(buildable, 0, maxStock, com[buildable].price, com[buildable].production);
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
	public bool IsBankrupt()
	{
		return cash < bankruptcyThreshold;
	}
    public float Tick()
	{
		float taxConsumed = 0;
		bool starving = false;
		if (stockPile.ContainsKey("Food"))
		{
            stockPile["Food"].quantity -= .1f;
            var food = stockPile["Food"].quantity;
            starving = false;//food < -25;
		}
		
		foreach (var entry in stockPile)
		{
			entry.Value.Tick();
        }

        if (IsBankrupt() || starving == true)
		{
			Debug.Log(name + ":" + buildables[0] + " is bankrupt: " + cash.ToString("c2") + " or starving " + starving);
			taxConsumed = ChangeProfession();
		}
		foreach (var buildable in buildables)
		{
			stockPile[buildable].cost = GetCostOf(buildable);
		}
		return taxConsumed;
	}

	float ChangeProfession()
	{
		string bestGood = Commodities.Instance.GetHottestGood(10);
		string bestProf = Commodities.Instance.GetMostProfitableProfession(10);

		string mostDemand = bestProf;
		if (bestGood != "invalid")
        {
            mostDemand = bestGood;
		}
				
		Assert.AreEqual(buildables.Count, 1);
		buildables[0] = mostDemand;
		stockPile.Clear();
		List<string> b = new List<string>();
		b.Add(mostDemand);
		var initCash = 100f;
		// todo take from irs?
		Init(initCash, b);
		return initCash;
	}

	const float bankruptcyThreshold = -100;
	/*********** Trading ************/
	public void modify_cash(float quant)
	{
		cash += quant;
	}
	public void Modify_Quantity(string commodity, float quantity)
	{
		stockPile[commodity].quantity += quantity;
	}
	public float Buy(string commodity, float quantity, float price)
	{
		var boughtQuantity = stockPile[commodity].Buy(quantity, price);
//	Debug.Log(name + " has " + cash.ToString("c2") + " buying " + quantity.ToString("n2") + " " + commodity + " for " + price.ToString("c2"));
		cash -= price * boughtQuantity;
		return boughtQuantity;
	}
	public void Sell(string commodity, float quantity, float price)
	{
		stockPile[commodity].Sell(-quantity, price);
//		Debug.Log(name + " has " + cash.ToString("c2") + " selling " + quantity.ToString("n2") +" " +  commodity + " for " + price.ToString("c2"));
		cash += price * quantity;
	}
	public void RejectAsk(string commodity, float price)
	{
		stockPile[commodity].updatePriceBelief(true, price, false);
	}
	public void RejectBid(string commodity, float price)
	{
		stockPile[commodity].updatePriceBelief(false, price, false);
	}

	int historyCount = 10;
    /*********** Produce and consume; enter asks and bids to auction house *****/
    float FindSellCount(string c)
	{
		var avgPrice = com[c].GetAvgPrice(historyCount);
		var lowestPrice = stockPile[c].sellHistory.Min();
		var highestPrice = stockPile[c].sellHistory.Max();
		//todo SANITY check
		float favorability = Mathf.InverseLerp(lowestPrice, highestPrice, avgPrice);
		favorability = Mathf.Clamp(favorability, 0, 1);
		float numAsks = (favorability) * stockPile[c].Surplus();
		//why max of 1??? numAsks = Mathf.Max(1, numAsks);

//		Debug.Log("avgPrice: " + avgPrice.ToString("c2") + " favoribility: " + favorability + " numAsks: " + numAsks.ToString("0.00"));
		return Mathf.Floor(numAsks);
	}
	float FindBuyCount(string c)
	{
		var avgPrice = com[c].GetAvgPrice(historyCount);
		var lowestPrice = stockPile[c].buyHistory.Min();
		var highestPrice = stockPile[c].buyHistory.Max();
		//todo SANITY check
		float favorability = Mathf.InverseLerp(lowestPrice, highestPrice, avgPrice);
		favorability = Mathf.Clamp(favorability, 0, 1);
		float numBids = (1 - favorability) * stockPile[c].Deficit();
		//WHY??? 
		//TODO find prices of all dependent commodities, then get relative importance, and split numBids on all commodities proportional to value (num needed/price)
		//numBids = Mathf.Max(1, numBids);
		

//		Debug.Log("avgPrice: " + avgPrice.ToString("c2") + " favoribility: " + favorability.ToString("n2") + " numBids: " + numBids.ToString("n2"));
		return Mathf.Floor(numBids);
	}
	public TradeSubmission Consume(Dictionary<string, Commodity> com) {
        var bids = new TradeSubmission();
        //replenish depended commodities
        foreach (var stock in stockPile)
		{
			//don't buy agent output
			if (buildables.Contains(stock.Key)) continue;

			var numBids = FindBuyCount(stock.Key);
			if (numBids > 0)
			{
				//maybe buy less if expensive?
				float buyPrice = stock.Value.GetPrice();
				if (buyPrice > 1000)
				{
					Debug.Log(stock.Key + "buyPrice: " + buyPrice.ToString("c2") + " : " + stock.Value.minPriceBelief.ToString("n2") + "<" + stock.Value.maxPriceBelief.ToString("n2"));
				}
				if (numBids < 0)
				{
					Debug.Log(stock.Key + " buying negative " + numBids.ToString("n2") + " for " + buyPrice.ToString("c2"));
				}
				bids.Add(stock.Key, new Trade(stock.Value.commodityName, buyPrice, numBids, this));
			}
        }
        return bids;
	}
	public TradeSubmission Produce(Dictionary<string, Commodity> com, ref float idleTax) {
        var asks = new TradeSubmission();
		//TODO sort buildables by profit

		//build as many as one can TODO don't build things that won't earn a profit
		float producedThisRound = 0;
		float revenueThisRound = 0;
		foreach (var buildable in buildables)
		{
			//get list of dependent commodities
			float numProduced = float.MaxValue; //amt agent can produce for commodity buildable
			string sStock = ", has in stock";
			//find max that can be made w/ available stock
			if (!com.ContainsKey(buildable))
			{
				Debug.Log("not a commodity: " + buildable);
			}
			foreach (var dep in com[buildable].dep)
			{
				//get num commodities you can build
				var numNeeded = dep.Value;
				var numAvail = stockPile[dep.Key].quantity;
				numProduced = Mathf.Min(numProduced, numAvail/numNeeded);
				sStock += " " + dep.Key + ": " + numAvail + "/" + dep.Key;
			}
			//can only build fixed rate at a time
			//can't produce more than what's in stock
			var upperBound = Mathf.Min(stockPile[buildable].productionRate, stockPile[buildable].Deficit());
			numProduced = Mathf.Clamp(numProduced, 0, upperBound);;
			//build and add to stockpile
			foreach (var dep in com[buildable].dep)
			{
				var stock = stockPile[dep.Key].quantity;
				var numUsed = dep.Value * numProduced;
				numUsed = Mathf.Clamp(numUsed, 0, stock);
                stockPile[dep.Key].quantity -= numUsed;
			}
			// WTF is production?? numProduced *= stockPile[buildable].production;
			//numProduced = Mathf.Max(numProduced, 0);
			stockPile[buildable].quantity += numProduced;
			if (float.IsNaN(numProduced))
				Debug.Log(buildable + " numproduced is nan!");
			Assert.IsFalse(float.IsNaN(numProduced));

            var buildStock = stockPile[buildable];
            float sellQuantity = FindSellCount(buildable);
            float sellPrice = buildStock.GetPrice();
			//HACK, so economy is always flowing somewhere
			//numProduced = Mathf.Max(1, numProduced);
			//sellPrice = Mathf.Max(1, sellPrice);

//            Debug.Log(name + " has " + cash.ToString("c2") + " made " + numProduced.ToString("n2") + " " + buildable + sellPrice.ToString("c2") + sStock);

			if (numProduced > 0 && sellPrice > 0)
			{
				asks.Add(buildable, new Trade(buildable, sellPrice, buildStock.quantity, this));
			}

			producedThisRound += numProduced;
			revenueThisRound += sellQuantity * sellPrice;
		}

		if (producedThisRound > 0 && revenueThisRound > 0)
		{
			idleTax = Mathf.Abs(cash * .05f); 
		}

		//alternative build: only make what is most profitable...?
		foreach (var buildable in buildables)
		{
			//cost to make c = price of dependents + overhead (labor, other)
			//profit = price of c - cost of c

			//number to produce c = f(profit/stock) (want to maximize!)

			//if c_price < c_cost 
			//just buy c, don't produce any
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
			var depCost = stockPile[depCommodity].buyHistory.Last();
			cost += numDep * depCost.price;
		}
		return cost;
	}
	// Update is called once per frame
	void Update () {
	}
}
