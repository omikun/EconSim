using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using System.Linq;

public class CommodityStock {
	public string commodityName { get; private set; }
	const float significant = 0.25f;
	const float sig_imbalance = .33f;
	const float lowInventory = .1f;
	const float highInventory = 2f;
	public List<float> priceHistory = new List<float>();

	public CommodityStock (string _name, float _quantity=1, float _maxQuantity=10, 
					float _meanPrice=1, float _production=1)
	{
		commodityName = _name;
		quantity = _quantity;
		maxQuantity = _maxQuantity;
		minPriceBelief = _meanPrice / 2;
		maxPriceBelief = _meanPrice * 2;
		meanCost = _meanPrice;
		priceHistory.Add(_meanPrice);
		production = _production;
	}
	public float Buy(float quant, float price)
	{
		//update meanCost of units in stock
        var totalCost = meanCost * this.quantity + price * quant;
        this.meanCost = totalCost / this.quantity;
		var leftOver = quant - Deficit();
		this.quantity += quant;
		if (leftOver > 0)
		{
			//this.quantity -= leftOver;
			Debug.Log("Bought too much! Max: " + this.quantity + " " + quant.ToString("n2") + " leftover: " + leftOver.ToString("n2"));
		}
		//update price belief
		updatePriceBelief(false, price);
		return quant;//leftOver;
	}
	public void Sell(float quant, float price)
	{
		this.quantity += quant;
		updatePriceBelief(true, price);
	}
	public float GetPrice()
	{
		minPriceBelief = Mathf.Clamp(minPriceBelief, 0.1f, 900);
		maxPriceBelief = Mathf.Clamp(maxPriceBelief, 1.1f, 1000);
		var p = Random.Range(minPriceBelief, maxPriceBelief);
		if (p > 1000f)
		{
            Assert.IsTrue(p <= 1000f);
			Debug.Log("price beliefs: " + minPriceBelief.ToString("n2") + ", " + maxPriceBelief.ToString("n2"));
		}

		return p;
	}
	public void updatePriceBelief(bool sell, float price, bool success=true)
	{
		minPriceBelief = Mathf.Clamp(minPriceBelief, 0.1f, 900);
		maxPriceBelief = Mathf.Clamp(maxPriceBelief, 1.1f, 1000);
//        Debug.Log(commodityName + " bounds: " + minPriceBelief.ToString("n2") + " < " + maxPriceBelief.ToString("n2"));
		if (minPriceBelief > maxPriceBelief)
		{
            Assert.IsTrue(minPriceBelief < maxPriceBelief);
			Debug.Log(commodityName + " ERROR " + minPriceBelief.ToString("n2") + " > "  + maxPriceBelief.ToString("n2"));
		}

		priceHistory.Add(price);
        var buy = !sell;
		var mean = (minPriceBelief + maxPriceBelief) / 2;
		var deltaMean = mean - price; //TODO or use auction house mean price?
//		Debug.Log("mean: " + mean.ToString("c2") + " price " + price.ToString("c2") + " dMean: " + deltaMean.ToString("c2"));
		if (success)
		{
			if ((sell && deltaMean < -significant * mean) //undersold
             || (buy  && deltaMean >  significant * mean))//overpaid
            {
				minPriceBelief -= deltaMean / 4; 		//shift toward mean
				maxPriceBelief -= deltaMean / 4;
            }
			minPriceBelief += wobble * mean;
			maxPriceBelief -= wobble * mean;
			if (minPriceBelief > maxPriceBelief)
			{
				var avg = (minPriceBelief + maxPriceBelief) / 2f;
				minPriceBelief = avg * (1 - wobble);
				maxPriceBelief = avg * (1 + wobble);
			}
            wobble /= 2;
		} else {
            minPriceBelief -= deltaMean / 2;        //shift toward mean
            maxPriceBelief -= deltaMean / 2;

			//if low inventory and can't buy or high inventory and can't sell
			if ((buy  && this.quantity < maxQuantity * lowInventory)
             || (sell && this.quantity > maxQuantity * highInventory))
            {
                //wobble += 0.05f;
            } else {
                //wobble -= 0.02f;
				//if too much demand or supply
				//new mean might be 0%-200% of market rate 
				//shift more based on market supply/demand
			}
			minPriceBelief -= wobble * mean;
			maxPriceBelief += wobble * mean;
		}

		//clamp to sane values
		//if (maxPriceBelief > 1000)
			//Debug.Log("ERROR " + maxPriceBelief.ToString("c2") + " > 1000");
	//	if (maxPriceBelief < 0 || minPriceBelief < 0)
//			Debug.Log("ERROR negative " + minPriceBelief.ToString("c2") + " " + maxPriceBelief.ToString("c2") );

        if (minPriceBelief < maxPriceBelief)
            minPriceBelief = maxPriceBelief / 2;
		Assert.IsTrue(minPriceBelief < maxPriceBelief);
		
		minPriceBelief = Mathf.Clamp(minPriceBelief, 0.1f, 900);
		maxPriceBelief = Mathf.Clamp(maxPriceBelief, 1.1f, 1000);

		if (float.IsNaN(minPriceBelief))
		{
			Debug.Log(commodityName + ": NaN! wobble" + wobble.ToString("n2") + " mean: " + mean.ToString("c2") + " sold price: " +price.ToString("c2"));
		}
	}
	//TODO change quantity based on historical price ranges & deficit
	public float Deficit() { return maxQuantity - quantity; }
	public float Surplus() { return quantity; }
	public float wobble = .02f;
	public float quantity;
	public float maxQuantity;
	public float minPriceBelief {
		get { return _minPriceBelief; }
		set {
			//if (value > 1000)
//                Debug.Log("minPriceBelief old: " + _minPriceBelief.ToString("c2") + " new: " + value.ToString("c2"));
			_minPriceBelief = value;
		}
	}
	private float _minPriceBelief;
	public float maxPriceBelief;
	public float meanCost; //total cost spent to acquire stock
	//number of units produced per turn = production * productionRate
	public float production; //scaler of productionRate
	public float productionRate = 1; //# of assembly lines
}
public class EconAgent : MonoBehaviour {
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

	public float GetProfit()
	{
		var profit = cash - prevCash;
		prevCash = cash;
		return profit;
	}
    public void Tick()
	{
		bool starving = false;
		if (stockPile.ContainsKey("Food"))
		{
            stockPile["Food"].quantity -= .1f;
            var food = stockPile["Food"].quantity;
            starving = false;//food < -25;
		}
		if (cash < bankruptcyThreshold || starving == true)
		{
			Debug.Log(name + ":" + buildables[0] + " is bankrupt: " + cash.ToString("c2") + " or starving " + starving);
			ChangeProfession();
		}
	}

	void ChangeProfession()
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
		Init(100, b);
	}

	const float bankruptcyThreshold = -50;
	/*********** Trading ************/
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
		var lowestPrice = stockPile[c].priceHistory.Min();
		var highestPrice = stockPile[c].priceHistory.Max();
		//todo SANITY check
		float favorability = Mathf.InverseLerp(lowestPrice, highestPrice, avgPrice);
		favorability = Mathf.Clamp(favorability, 0, 1);
		float numAsks = (favorability) * stockPile[c].Surplus();
		numAsks = Mathf.Max(1, numAsks);

//		Debug.Log("avgPrice: " + avgPrice.ToString("c2") + " favoribility: " + favorability + " numAsks: " + numAsks.ToString("0.00"));
		return numAsks;
	}
	float FindBuyCount(string c)
	{
		var avgPrice = com[c].GetAvgPrice(historyCount);
		var lowestPrice = stockPile[c].priceHistory.Min();
		var highestPrice = stockPile[c].priceHistory.Max();
		//todo SANITY check
		float favorability = Mathf.InverseLerp(lowestPrice, highestPrice, avgPrice);
		favorability = Mathf.Clamp(favorability, 0, 1);
		float numBids = (1 - favorability) * stockPile[c].Deficit();
		numBids = Mathf.Max(1, numBids);

//		Debug.Log("avgPrice: " + avgPrice.ToString("c2") + " favoribility: " + favorability.ToString("n2") + " numBids: " + numBids.ToString("n2"));
		return numBids;
	}
	public TradeSubmission Consume(Dictionary<string, Commodity> com) {
        var bids = new TradeSubmission();
        //replenish depended commodities
        foreach (var stock in stockPile)
		{
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
	public TradeSubmission Produce(Dictionary<string, Commodity> com) {
        var asks = new TradeSubmission();
		//TODO sort buildables by profit

		//build as many as one can TODO don't build things that won't earn a profit
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
				sStock += " " + numAvail + " " + dep.Key;
			}
			numProduced = Mathf.Max(0, numProduced);//sanity check
			//can only build 1 at a time
			numProduced = Mathf.Min(numProduced, stockPile[buildable].productionRate);
			//build and add to stockpile
			foreach (var dep in com[buildable].dep)
			{
				var stock = stockPile[dep.Key].quantity;
				var numUsed = dep.Value * numProduced;
				numUsed = Mathf.Min(numUsed, stock);
                stockPile[dep.Key].quantity -= numUsed;
			}
			numProduced *= stockPile[buildable].production;
			stockPile[buildable].quantity += numProduced;
			if (float.IsNaN(numProduced))
				Debug.Log(buildable + " numproduced is nan!");

            var buildStock = stockPile[buildable];
            float sellPrice = FindSellCount(buildable); buildStock.GetPrice();
			//HACK, so economy is always flowing somewhere
			//numProduced = Mathf.Max(1, numProduced);
			//sellPrice = Mathf.Max(1, sellPrice);

//            Debug.Log(name + " has " + cash.ToString("c2") + " made " + numProduced.ToString("n2") + " " + buildable + sellPrice.ToString("c2") + sStock);

			if (numProduced > 0 && sellPrice > 0)
			{
				asks.Add(buildable, new Trade(buildable, sellPrice, buildStock.quantity, this));
			} else {
				cash -= Mathf.Abs(cash*.2f);
			}
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
	// Update is called once per frame
	void Update () {
	}
}
