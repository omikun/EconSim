using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EconAgent : MonoBehaviour {
	public int debug = 0;
	float cash = 0;
	float maxStock = 1;
	//has a set of commodities in stock
	Dictionary<string, float> stockPile = new Dictionary<string, float>(); //commodities stockpiled
	Dictionary<string, float> stockPileCost = new Dictionary<string, float>(); //commodities stockpiled

	//can produce a set of commodities
	List<string> buildables;
	//production has dependencies on commodities->populates stock
	//production rate is limited by assembly lines (queues/event lists)
	
	//can use profit to reinvest - produce new commodities
	//switching cost to produce new commodities - zero for now

	//from the paper (base implementation)
	float priceBound = 10; //price belief [median-priceBound, median+priceBound]
	// Use this for initialization
	Dictionary<string, Commodity> com;
	void Start () {
		com = Commodities.Instance.com;
	}
	void AddToStockPile(string name, float num)
	{
		if (stockPile.ContainsKey(name))
			return;

        stockPile.Add(name, num);

		//book keeping
		stockPileCost[name] = com[name].price * num;
	}
	public void Init(float initCash, List<string> b, float initNum=5, float maxstock=10) {
		//list of commodities self can produce
		//get initial stockpiles
		Debug.Log(gameObject.name + " builds: " + b[0]);
		buildables = b;
		cash = initCash;
		maxStock = maxstock;
		foreach (var buildable in buildables)
		{
			if (!com.ContainsKey(buildable))
				Debug.Log("commodity not recognized: " + buildable);

            if (com[buildable].dep == null)
				Debug.Log(buildable + ": null dep!");

			foreach (var dep in com[buildable].dep)
			{
				AddToStockPile(dep.Key, initNum);
			}
			AddToStockPile(buildable, 0);
		}
	}

	/*********** Trading ************/
	public void Buy(string commodity, float quantity, float price)
	{
		Trade(commodity, quantity, price);
	}
	public void Sell(string commodity, float quantity, float price)
	{
		Trade(commodity, -quantity, price);
	}
	public void Trade(string commodity, float quantity, float price)
	{
		float sign = Mathf.Sign(quantity);
		var beforeStock = stockPile[commodity];
		stockPile[commodity] += sign * quantity;
		cash -= sign * price * quantity;

		//book keeping
		if (sign > 1)
		{
			var averagePrice = stockPileCost[commodity];
            var totalPrice = averagePrice * beforeStock + quantity * sign;
			stockPileCost[commodity] = totalPrice / stockPile[commodity];
		}
	}


	/*********** Produce and consume; enter asks and bids to auction house *****/
	public TradeSubmission Consume(Dictionary<string, Commodity> com) {
        var bids = new TradeSubmission();
        //replenish depended commodities
        foreach (var stock in stockPile)
		{
			if (buildables.Contains(stock.Key)) continue;

            var numInStock = stock.Value;
			//TODO add price beliefs
			float price = 4;
			if (numInStock < maxStock)
			{
				//maybe buy less if expensive?
				bids.Add(stock.Key, new Trade(price, maxStock-numInStock, this));
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
			float numProduced = 0; //amt agent can produce for commodity buildable
			foreach (var dep in com[buildable].dep)
			{
				//get num commodities you can build
				var numNeeded = dep.Value;
				var numAvail = stockPile[dep.Key];
				numProduced = Mathf.Max(numProduced, numAvail/numNeeded);
			}
			//build and add to stockpile
			foreach (var dep in com[buildable].dep)
			{
				stockPile[dep.Key] -= dep.Value * numProduced;
			}
			stockPile[buildable] += numProduced;
			float price = 5; //TODO implement price beliefs
			if (numProduced > 0)
			{
				asks.Add(buildable, new Trade(price, stockPile[buildable], this));
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
