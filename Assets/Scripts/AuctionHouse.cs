using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
using AYellowpaper.SerializedCollections;

public class Offer : Dictionary<string, Trade> { }
public class AuctionHouse : MonoBehaviour {
    public float tickInterval = .001f;
	public int maxRounds = 10;
	
	[SerializedDictionary("Comm", "numAgents")]
	public SerializedDictionary<string, int> numAgents = new()
	{
		{ "Food", 3 },
		{ "Wood", 3 },
		{ "Ore", 3 },
		{ "Metal", 4 },
		{ "Tool", 4 }
	};

	public float initCash = 100;
	public bool simpleInitStock = false;
	public float initStock = 10;
	public float maxStock = 20;
	List<EconAgent> agents = new List<EconAgent>();
	float irs;
    TradeTable askTable, bidTable;
	StreamWriter sw;

	Dictionary<string, Dictionary<string, float>> trackBids = new();
	float lastTick;
	public bool EnableDebug = false;
	void Start () {
		Debug.unityLogger.logEnabled=EnableDebug;
		OpenFileForWrite();

		UnityEngine.Random.InitState(42);
		lastTick = 0;
		var com = Commodities.Instance.com;

		irs = 0; //GetComponent<EconAgent>();
		var prefab = Resources.Load("Agent");

		for (int i = transform.childCount; i < numAgents.Values.Sum(); i++)
		{
		    GameObject go = Instantiate(prefab) as GameObject;
			go.transform.parent = transform;
			go.name = "agent" + i.ToString();
		}
		
		int agentIndex = 0;
		var professions = numAgents.Keys;
		foreach (string profession in professions)
		{
			for (int i = 0; i < numAgents[profession]; ++i)
			{
				GameObject child = transform.GetChild(agentIndex).gameObject;
				var agent = child.GetComponent<EconAgent>();
				InitAgent(agent, profession);
				agents.Add(agent);
				++agentIndex;
			}
		}
		
		askTable = new TradeTable();
        bidTable = new TradeTable();

		foreach (var entry in com)
		{
			trackBids.Add(entry.Key, new Dictionary<string, float>());
            foreach (var item in com)
			{
				//allow tracking farmers buying food...
				trackBids[entry.Key].Add(item.Key, 0);
			}
		}
	}
	void OnApplicationQuit() 
	{
		//CloseWriteFile();
	}
	void InitAgent(EconAgent agent, string type)
	{
        List<string> buildables = new List<string>();
		buildables.Add(type);
		float _initStock = 0;
		if (simpleInitStock)
		{
			_initStock = initStock;
		} else {
			_initStock = UnityEngine.Random.Range(initStock/2, initStock*2);
			_initStock = Mathf.Floor(_initStock);
		}

		// TODO: This may cause uneven maxStock between agents
		var _maxStock = Mathf.Max(initStock, maxStock);

        agent.Init(initCash, buildables, _initStock, _maxStock);
	}
	
	// Update is called once per frame
	void FixedUpdate () {
		if (Commodities.Instance.round > maxRounds)
		{
			CloseWriteFile();
#if UNITY_EDITOR
			UnityEditor.EditorApplication.isPlaying = false;
#elif UNITY_WEBPLAYER
        Application.OpenURL("127.0.0.1");
#else
        Application.Quit();
#endif
			return;
		}
		//wait before update
		if (Time.time - lastTick > tickInterval)
		{
			Debug.Log("v1.4 Round: " + Commodities.Instance.round);
			//sampler.BeginSample("AuctionHouseTick");
            Tick();
			//sampler.EndSample();
			lastTick = Time.time;
			Commodities.Instance.nextRound();
		}
	}
	void Tick()
	{
		Debug.Log("auction house ticking");

		var com = Commodities.Instance.com;
		//get all agents asks
		//get all agents bids
		foreach (var agent in agents)
		{
			float idleTax = 0;
			askTable.Add(agent.Produce(com, ref idleTax));
			//Utilities.TransferQuantity(idleTax, agent, irs);
			bidTable.Add(agent.Consume(com));
		}

		//was at bottom of tick
		EnactBankruptcy();

		//resolve prices
		foreach (var entry in com)
		{
			ResolveOffers(entry.Value);
			Debug.Log(entry.Key + ": goods: " + entry.Value.trades[^1] + " at price: " + entry.Value.prices[^1]);
		}
		

		//PrintToFile("round, " + Commodities.Instance.round + ", commodity, " + commodity + ", price, " + averagePrice);
		AgentsStats();
		CountProfits();
		CountProfessions();
		CountStockPileAndCash();

		foreach (var agent in agents)
		{
			agent.ClearRoundStats();
		}
	}

	void PrintAuctionStats(String c, float buy, float sell)
	{
		String header = Commodities.Instance.round + ", auction, none, " + c + ", ";
		String msg = header + "bid, " + buy + ", n/a\n";
		msg += header + "ask, " + sell + ", n/a\n";
		PrintToFile(msg);
	}
	void ResolveOffers(Commodity commodity)
	{
		float moneyExchanged = 0;
		float goodsExchanged = 0;
		var asks = askTable[commodity.name];
		var bids = bidTable[commodity.name];
		var acceptedAsks = new Trades();
		var acceptedBids = new Trades();
		var agentDemandRatio = bids.Count / Mathf.Max(.01f, (float)asks.Count); //demand by num agents bid/

		/******* debug */
		if (bids.Count > 0)
		{
			Trade hTrade = bids[0];
			foreach (var bid in bids)
			{
				if (bid.price > hTrade.price)
				{
					hTrade = bid;
				}
			}
			if (hTrade.price > 1000)
				Debug.Log(hTrade.agent.name + " bid " + commodity.name + " more than $1000: " + hTrade.price);
		}
		/******* end debug */

		var quantityToBuy = bids.Sum(item => item.remainingQuantity);
		var quantityToSell = asks.Sum(item => item.remainingQuantity);
		commodity.bids.Add(quantityToBuy);
		commodity.asks.Add(quantityToSell);
		commodity.buyers.Add(bids.Count);
		commodity.sellers.Add(asks.Count);
		PrintAuctionStats(commodity.name, quantityToBuy, quantityToSell);

		asks.Shuffle();
		bids.Shuffle();

		asks.Sort((x, y) => x.price.CompareTo(y.price)); //inc
		bids.Sort((x, y) => y.price.CompareTo(x.price)); //dec

		float watchdog_timer = 0;

////////////////////////////////////////////
// BIG LOOP to resolve all offers
////////////////////////////////////////////
		bool run = true;
		var askIt = asks.GetEnumerator();
		var bidIt = bids.GetEnumerator();
		run &= askIt.MoveNext();
		run &= bidIt.MoveNext();

		while (true && run)
		{
			var ask = askIt.Current;
			var bid = bidIt.Current;

			//set price
			var clearingPrice = (bid.price + ask.price) / 2;
			bid.clearingPrice = clearingPrice;
			ask.clearingPrice = clearingPrice;
			if (clearingPrice <= 0)
			{
				Debug.Log(Commodities.Instance.round + " " + commodity.name + " asker: " + ask.agent.name + " asking price: " + ask.price + "bidder: " + bid.agent.name + " bidding price: " + bid.price + " clearingPrice: " + clearingPrice);
			}
			Assert.IsTrue(clearingPrice > 0);
			//go to next ask/bid if fullfilled
			// DEBUG this should not be necessary!?
			if (ask.remainingQuantity == 0)
			{
				if (askIt.MoveNext() == false)
					break;
				watchdog_timer = 0;
				continue;
			}
			if (bid.remainingQuantity == 0)
			{
				if (bidIt.MoveNext() == false)
					break;
				watchdog_timer = 0;
				continue;
			}
			// =========== trade ============== 
			var tradeQuantity = Mathf.Min(bid.remainingQuantity, ask.remainingQuantity);
			Debug.Log(commodity.name + " asked: " + ask.remainingQuantity + " bided: " + bid.remainingQuantity);
			Assert.IsTrue(tradeQuantity > 0);
			Assert.IsTrue(clearingPrice > 0);
#if false
				Trade(commodity, clearingPrice, bid.agent, ask.agent);
#else
			var boughtQuantity = bid.agent.Buy(commodity.name, tradeQuantity, clearingPrice);
			ask.agent.Sell(commodity.name, boughtQuantity, clearingPrice);
			Debug.Log(ask.agent.name + " ask " + ask.remainingQuantity + "x" + ask.price 
					+ " | " + bid.agent.name + " bid: " + bid.remainingQuantity + "x" + bid.price 
					+ " -- " + commodity.name + " offer quantity: " + tradeQuantity + " bought quantity: " + boughtQuantity);
#endif
			//track who bought what
			var buyers = trackBids[commodity.name];
			buyers[bid.agent.outputs[0]] += clearingPrice * boughtQuantity;

			moneyExchanged += clearingPrice * boughtQuantity;
			goodsExchanged += boughtQuantity;

			//go to next ask/bid if fullfilled
			if (ask.Reduce(boughtQuantity) == 0)
			{
				if (askIt.MoveNext() == false)
					break;
				watchdog_timer = 0;
			}
			if (bid.Reduce(boughtQuantity) == 0)
			{
				if (bidIt.MoveNext() == false)
					break;
				watchdog_timer = 0;
			}
			watchdog_timer++;
			if (watchdog_timer > 1000)
			{
				Debug.Log("Can't seem to sell: " + commodity.name + " bought: " + boughtQuantity + " for " + clearingPrice.ToString("c2"));
				if (bidIt.MoveNext() == false)
					break;
			}
		}
		Assert.IsFalse(goodsExchanged < 0);
////////////////////////////////////////////
// END OF BIG LOOP
////////////////////////////////////////////

		var denom = (goodsExchanged == 0) ? 1 : goodsExchanged;
		var averagePrice = moneyExchanged / denom;
		if (float.IsNaN(averagePrice))
		{
			Debug.Log(commodity.name + ": average price is nan");
			Assert.IsFalse(float.IsNaN(averagePrice));
		}
		Debug.Log(Commodities.Instance.round + ": " + commodity.name + ": " + goodsExchanged + " traded at average price of " + averagePrice);
		commodity.trades.Add(goodsExchanged);
		commodity.prices.Add(averagePrice);
		commodity.Update(averagePrice, agentDemandRatio);

		//calculate supply/demand
		//var excessDemand = asks.Sum(ask => ask.quantity);
		//var excessSupply = bids.Sum(bid => bid.quantity);
		//var demand = (goodsExchanged + excessDemand) 
		//					 / (goodsExchanged + excessSupply);

		//update price beliefs
		//reject the rest
		foreach (var ask in asks)
		{
			ask.agent.UpdateSellerPriceBelief(in ask, in commodity);
		}
		asks.Clear();
		foreach (var bid in bids)
		{
			bid.agent.UpdateBuyerPriceBelief(in bid, in commodity);
		}
		bids.Clear();

		//SetGraph(gMeanPrice, commodity.name, averagePrice);
		//SetGraph(gUnitsExchanged, commodity.name, goodsExchanged);
	}

	// TODO decouple transfer of commodity with transfer of money
	// TODO convert cash into another commodity
	void Trade(String commodity, float clearingPrice, float quantity, EconAgent bidder, EconAgent seller) 
	{
		//transfer commodity
		//transfer cash
		var boughtQuantity = bidder.Buy(commodity, quantity, clearingPrice);
		seller.Sell(commodity, boughtQuantity, clearingPrice);
		var cashQuantity = quantity * clearingPrice;

	}

	void OpenFileForWrite() {
		sw = new StreamWriter("log.csv");
		String header_row = "round, agent, produces, inventory_items, type, amount, reason\n";
		PrintToFile(header_row);
	}
	void PrintToFile(String msg) {
		sw.Write(msg);
	}

	void CloseWriteFile() {
		sw.Close();
	}

	void AgentsStats() {
		String header = Commodities.Instance.round + ", ";
		String msg = "";
		foreach (var agent in agents)
		{
			msg += agent.Stats(header);
		}
		PrintToFile(msg);
	}
	void CountStockPileAndCash() 
	{
		Dictionary<string, float> stockPile = new Dictionary<string, float>();
		Dictionary<string, List<float>> stockList = new Dictionary<string, List<float>>();
		Dictionary<string, List<float>> cashList = new Dictionary<string, List<float>>();
		var com = Commodities.Instance.com;
		float totalCash = 0;
		foreach(var entry in com)
		{
			stockPile.Add(entry.Key, 100);
			stockList.Add(entry.Key, new List<float>());
			cashList.Add(entry.Key, new List<float>());
		}
		foreach(var agent in agents)
		{
			//count stocks in all stocks of agent
			foreach(var c in agent.inventory)
			{
				stockPile[c.Key] += c.Value.Surplus();
				var surplus = c.Value.Surplus();
				if (surplus > 20)
				{
					//Debug.Log(agent.name + " has " + surplus + " " + c.Key);
				}
				stockList[c.Key].Add(surplus);
			}
            cashList[agent.outputs[0]].Add(agent.cash);
			totalCash += agent.cash;
		}
		foreach(var stock in stockPile)
        {
			int bucket = 1, index = 0;
			var avg = GetQuantile(stockList[stock.Key], bucket, index);
            //SetGraph(gStocks, stock.Key, avg);

            if (avg > 20)
            {
                //Debug.Log(stock.Key + " HIGHSTOCK: " + avg.ToString("n2") + " max: " + stockList[stock.Key].Max().ToString("n2") + " num: " + stockList[stock.Key].Count);
            }

            //avg = GetQuantile(cashList[stock.Key], bucket, index);
            //SetGraph(gCapital, stock.Key, cashList[stock.Key].Sum());
        }
        //SetGraph(gCapital, "Total", totalCash);
		//SetGraph(gCapital, "IRS", irs+totalCash);
		
	}
	float GetQuantile(List<float> list, int buckets=4, int index=0) //default lowest quartile
	{
		float avg = 0;
		if (buckets == 1)
		{
			if (list.Count > 0)
                return list.Average();
            else
				return 0;
		}
		Assert.IsTrue(index < buckets);
		Assert.IsTrue(index >= 0);
		var numPerQuantile = list.Count / buckets;
		var numQuantiles = buckets;
		var begin = Mathf.Max(0, index * numPerQuantile);
		var end = Mathf.Min(list.Count-1, begin + numPerQuantile);
		if (list.Count != 0 && end > 0)
		{
            //Debug.Log("list.count: " + list.Count + " begin: " + begin + " end: " + end);
            //var skip = Mathf.Max(0, list.Count * (buckets - index -1) / buckets);
            list.Sort();
            var newList = list.GetRange(begin, end);
            avg = newList.Average();
        }
		return avg;
    }
	float taxRate = .15f;
	void CountProfits()
	{
		var com = Commodities.Instance.com;
		//count profit per profession/commodity
		//first get total profit earned this round
        Dictionary<string, float> totalProfits = new Dictionary<string, float>();
		//and number of agents per commodity
        Dictionary<string, int> numAgents = new Dictionary<string, int>();
		//initialize
        foreach (var entry in com)
        {
            var commodity = entry.Key;
			totalProfits.Add(commodity, 0);
			numAgents.Add(commodity, 0);
        }
		//accumulate
        foreach (var agent in agents)
        {
            var commodity = agent.outputs[0];
            //totalProfits[commodity] += agent.TaxProfit(taxRate);
            totalProfits[commodity] += agent.GetProfit();
			numAgents[commodity] ++;
        }
		//average
		foreach (var entry in com)
		{
			var commodity = entry.Key;
			var profit = totalProfits[commodity];// / numAgents[commodity];
			if (profit == 0)
			{
				Debug.Log(commodity + " no profit earned this round");
			} else {
                entry.Value.profits.Add(profit);
			}
			if (float.IsNaN(profit) || profit > 10000)
			{
				profit = -42; //special case to use last value in graph
			}
            //SetGraph(gCash, commodity, profit);
		}
	}

    float defaulted = 0;
	void EnactBankruptcy()
	{
        foreach (var agent in agents)
        {
			if (agent.IsBankrupt())
			{
				defaulted += agent.cash;
			}
			agent.Tick();
            //irs -= agent.Tick();
        }
	}
	void CountProfessions()
	{
		var com = Commodities.Instance.com;
		//count number of agents per professions
		Dictionary<string, float> professions = new Dictionary<string, float>();
		//initialize professions
		foreach (var item in com)
		{
			var commodity = item.Key;
			professions.Add(commodity, 0);
		}
		//bin professions
        foreach (var agent in agents)
        {
			professions[agent.outputs[0]] += 1;
        }

		foreach (var entry in professions)
		{
			//Debug.Log("Profession: " + entry.Key + ": " + entry.Value);
			//SetGraph(gProfessions, entry.Key, entry.Value);
		}
	}
}
