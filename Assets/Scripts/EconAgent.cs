using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

public class CommodityStock {
	const float significant = 0.25f;
	const float sig_imbalance = .33f;
	const float lowInventory = .1f;
	const float highInventory = 2f;

	public CommodityStock (float _quantity=1, float _maxQuantity=10, 
					float _meanPrice=1, float _production=1)
	{
		quantity = _quantity;
		maxQuantity = _maxQuantity;
		minPriceBelief = _meanPrice / 2;
		maxPriceBelief = _meanPrice * 2;
		meanCost = _meanPrice;
		production = _production;
	}
	public void Buy(float quant, float price)
	{
		//update meanCost of units in stock
        var totalCost = meanCost * this.quantity + price * quant;
        this.meanCost = totalCost / this.quantity;
		this.quantity += quant;
		//update price belief
		updatePriceBelief(false, price);
	}
	public void Sell(float quant, float price)
	{
		this.quantity += quant;
		updatePriceBelief(true, price);
	}
	public float GetPrice()
	{
		return Random.Range(minPriceBelief, maxPriceBelief);
	}
	public void updatePriceBelief(bool sell, float price, bool success=true)
	{
        var buy = !sell;
		var mean = (minPriceBelief + maxPriceBelief) / 2;
		var deltaMean = mean - price; //TODO or use auction house mean price?
		if (success)
		{
			if ((sell && deltaMean < -significant * mean) //undersold
             || (buy  && deltaMean >  significant * mean))//overpaid
            {
				minPriceBelief -= deltaMean / 2; 		//shift toward mean
				maxPriceBelief -= deltaMean / 2;
            }
			minPriceBelief += wobble * mean;
			maxPriceBelief -= wobble * mean;
		} else {
            minPriceBelief -= deltaMean / 2;        //shift toward mean
            maxPriceBelief -= deltaMean / 2;

			//if low inventory and can't buy or high inventory and can't sell
			if ((buy  && this.quantity < lowInventory)
             || (sell && this.quantity > highInventory))
            {
                wobble *= 2;
            } else {
				//if too much demand or supply
				//new mean might be 0%-200% of market rate 
				//shift more based on market supply/demand
			}
			minPriceBelief -= wobble * mean;
			maxPriceBelief += wobble * mean;
		}
		minPriceBelief = Mathf.Max(0.1f, minPriceBelief);
		maxPriceBelief = Mathf.Max(0.1f, maxPriceBelief);
	}
	//TODO change quantity based on historical price ranges & deficit
	public float Deficit() { return maxQuantity - quantity; }
	public float Surplus() { return quantity; }
	public float wobble = .05f;
	public float quantity;
	public float maxQuantity;
	public float minPriceBelief;
	public float maxPriceBelief;
	public float meanCost; //total cost spent to acquire stock
	//number of units produced per turn = production * productionRate
	public float production; //scaler of productionRate
	public float productionRate = 1; //# of assembly lines
}
public class EconAgent : MonoBehaviour {
	public int debug = 0;
	float cash = 0;
	float maxStock = 1;
	//has a set of commodities in stock
	Dictionary<string, CommodityStock> stockPile = new Dictionary<string, CommodityStock>(); //commodities stockpiled
	Dictionary<string, float> stockPileCost = new Dictionary<string, float>(); //commodities stockpiled

	//can produce a set of commodities
	List<string> buildables;
	//production has dependencies on commodities->populates stock
	//production rate is limited by assembly lines (queues/event lists)
	
	//can use profit to reinvest - produce new commodities
	//switching cost to produce new commodities - zero for now

	//from the paper (base implementation)
	float priceBound = 10; //price belief [median-priceBound, median+priceBound]
	float selfPrice;
	// Use this for initialization
	Dictionary<string, Commodity> com {get; set;}
	void Start () {
	}
	void AddToStockPile(string name, float num, float max, float price, float production)
	{
		if (stockPile.ContainsKey(name))
			return;

        stockPile.Add(name, new CommodityStock(num, max, price, production));

		//book keeping
		stockPileCost[name] = com[name].price * num;
	}
	public void Init(float initCash, List<string> b, float initNum=5, float maxstock=10) {
        if (com == null)
			com = Commodities.Instance.com;
		//list of commodities self can produce
		//get initial stockpiles
		buildables = b;
		cash = initCash;
		maxStock = maxstock;
		foreach (var buildable in buildables)
		{
            Debug.Log(gameObject.name + " builds: " + buildable);

			if (!com.ContainsKey(buildable))
				Debug.Log("commodity not recognized: " + buildable);

            if (com[buildable].dep == null)
				Debug.Log(buildable + ": null dep!");

			foreach (var dep in com[buildable].dep)
			{
				var commodity = dep.Key;
                Debug.Log("::" + commodity);
				AddToStockPile(commodity, initNum, maxStock, com[commodity].price, com[commodity].production);
			}
			AddToStockPile(buildable, 0, maxStock, com[buildable].price, com[buildable].production);
		}
	}

	/*********** Trading ************/
	public void Buy(string commodity, float quantity, float price)
	{
		stockPile[commodity].Buy(quantity, price);
		cash -= price * quantity;
	}
	public void Sell(string commodity, float quantity, float price)
	{
		quantity = -quantity;
		stockPile[commodity].Sell(quantity, price);
		cash -= price * quantity;
	}
	public void RejectAsk(string commodity, float price)
	{
		stockPile[commodity].updatePriceBelief(true, price, false);
	}
	public void RejectBid(string commodity, float price)
	{
		stockPile[commodity].updatePriceBelief(false, price, false);
	}

	/*********** Produce and consume; enter asks and bids to auction house *****/
	public TradeSubmission Consume(Dictionary<string, Commodity> com) {
        var bids = new TradeSubmission();
        //replenish depended commodities
        foreach (var stock in stockPile)
		{
			if (buildables.Contains(stock.Key)) continue;

			//TODO add price beliefs
			var numBids = stock.Value.Deficit();
			if (numBids > 0)
			{
				//maybe buy less if expensive?
				bids.Add(stock.Key, new Trade(stock.Value.GetPrice(), numBids, this));
			}
        }
				//adjust price believes
		//if in debt > threshtold
			//remove least profitable commodity, add in most in demand commodity
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
			foreach (var dep in com[buildable].dep)
			{
				//get num commodities you can build
				var numNeeded = dep.Value;
				var numAvail = stockPile[dep.Key].quantity;
				numProduced = Mathf.Min(numProduced, numAvail/numNeeded);
				sStock += " " + numAvail + " " + dep.Key;
			}
			//can only build 1 at a time
			numProduced = Mathf.Min(numProduced, stockPile[buildable].productionRate);
			//build and add to stockpile
			foreach (var dep in com[buildable].dep)
			{
				bool sanity = (stockPile[dep.Key].quantity >= dep.Value * numProduced);
				if (!sanity)
                {
                    Assert.IsTrue(sanity);
					Debug.Log(dep.Key + ": " + stockPile[dep.Key].quantity + ">=" + dep.Value * numProduced);
                    stockPile[dep.Key].quantity = 0;
				}
                else
                {
                    stockPile[dep.Key].quantity -= dep.Value * numProduced;
				}
			}
			numProduced *= stockPile[buildable].production;
			stockPile[buildable].quantity += numProduced;
			if (float.IsNaN(numProduced))
				Debug.Log(buildable + " numproduced is nan!");

            Debug.Log(name + " has " + cash + " made " + numProduced + " " + buildable + sStock);
			if (numProduced > 0)
			{
				var buildStock = stockPile[buildable];
				asks.Add(buildable, new Trade(buildStock.GetPrice(), buildStock.quantity, this));
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
