using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UIElements;


public static class IListExtensions {
    /// <summary>
    /// Shuffles the element order of the specified list.
    /// </summary>
    public static void Shuffle<T>(this IList<T> ts) {
        var count = ts.Count;
        var last = count - 1;
        for (var i = 0; i < last; ++i) {
            var r = UnityEngine.Random.Range(i, count);
            var tmp = ts[i];
            ts[i] = ts[r];
            ts[r] = tmp;
        }
    }
}

public class MarketStats {
	public float totalSupply;
	public float totalDemand;
	public float agentDemandRatio;
	public float meanPrice;
}
public class Trade
{
	public Trade(string c, float p, float q, EconAgent a)
	{
		commodity = c;
		price = p;
		clearingPrice = p;
		remainingQuantity = q;
		offerQuantity = q;
		agent = a;
	}
	public float Reduce(float q)
	{
		remainingQuantity -= q;
		return remainingQuantity;
	}
	public void Print()
	{
		Debug.Log(agent.gameObject.name + ": " + commodity + " trade: " + price + ", " + remainingQuantity);
	}
	public string commodity { get; private set; }
	public float price { get; private set; }
	public float clearingPrice;
	public float remainingQuantity { get; private set; }
	public float offerQuantity { get; private set; }
	public EconAgent agent{ get; private set; }
}
public class TradeSubmission : Dictionary<string, Trade> { }
public class Trades : List<Trade> { 
	public new void RemoveAt(int index)
	{
		int before = base.Count;
		base.RemoveAt(index);
        if (before != base.Count + 1) 
			Debug.Log("did not remove trade correctly! before: " 
			+ before + " after: " + base.Count);
    }
    public void Shuffle()
    {
        var count = base.Count;
        var last = count - 1;
        for (var i = 0; i < last; ++i) {
            var r = UnityEngine.Random.Range(i, count);
            var tmp = base[i];
            base[i] = base[r];
            base[r] = tmp;
        }
    }
	public void Print()
	{
		var enumerator = base.GetEnumerator();
        while (enumerator.MoveNext())
        {
            var item = enumerator.Current;
			item.Print();
		}
	}


}
//commodities["commodity"] = ordered list<price, quantity, seller>
public class TradeTable : Dictionary<string, Trades>
{
	public TradeTable() 
	{
        var com = Commodities.Instance.com;
        foreach (var c in com)
        {
            base.Add(c.Key, new Trades());
        }
    }
	public void Add(TradeSubmission ts)
	{
		foreach (var entry in ts)
		{
			var commodity = entry.Key;
			var trade = entry.Value;
			base[commodity].Add(trade);
		}
	}
}
public class AuctionHouse : MonoBehaviour {
    public float tickInterval = .01f;
    public int numAgents = 100;
	public float initCash = 100;
	public float initStock = 10;
	public float maxStock = 20;
	List<EconAgent> agents = new List<EconAgent>();
	float irs;
    TradeTable askTable, bidTable;
	StreamWriter sw;

    //gmp = Graph Mean Price
    List<GraphMe> gMeanPrice, gUnitsExchanged, gProfessions, gStocks, gCash, gCapital;
	//trackBids[selling_commodity][buyer's producing commodity] = price buyer paid for seller * number of selling_commodity bought
	Dictionary<string, Dictionary<string, float>> trackBids = new Dictionary<string, Dictionary<string, float>>();
	float lastTick;
	public bool EnableDebug = false;
	void AddLine(int i, List<GraphMe> gmList, GameObject graph)
	{
		var line = graph.transform.Find("line"+i);
		if (line != null)
		{
			gmList.Add(line.GetComponent<GraphMe>());
		}
	}
	// Use this for initialization
	void Start () {
		//sampler = new CustomSampler("AuctionHouseTick");
		Debug.unityLogger.logEnabled=EnableDebug;
		OpenFileForWrite();

		lastTick = 0;
		int count = 0;
		var com = Commodities.Instance.com;
        gMeanPrice = new List<GraphMe>(com.Count);
        gUnitsExchanged = new List<GraphMe>(com.Count);
        gProfessions = new List<GraphMe>(com.Count);
        gStocks = new List<GraphMe>(com.Count);
        gCash = new List<GraphMe>(com.Count);
        gCapital = new List<GraphMe>(com.Count);

		/* initialize graphs */
		var gmp = GameObject.Find("AvgPriceGraph");
		var gue = GameObject.Find("UnitsExchangedGraph");
		var gp = GameObject.Find("ProfessionsGraph");
		var gs = GameObject.Find("StockPileGraph");
		var gc = GameObject.Find("CashGraph");
		var gtc = GameObject.Find("TotalCapitalGraph");
		for (int i = 0; i < com.Count+3; i++) 
		{
			AddLine(i, gMeanPrice, gmp);
			AddLine(i, gUnitsExchanged, gue);
			AddLine(i, gProfessions, gp);
			AddLine(i, gStocks,  gs);
			AddLine(i, gCash, gc);
			AddLine(i, gCapital, gtc);
		}
		irs = 0; //GetComponent<EconAgent>();
		#if false
			gMeanPrice.Add(gmp.transform.Find("line"+i).GetComponent<GraphMe>());
			gUnitsExchanged.Add(gue.transform.Find("line"+i).GetComponent<GraphMe>());
			gProfessions.Add(gp.transform.Find("line"+i).GetComponent<GraphMe>());
			gStocks.Add(gs.transform.Find("line"+i).GetComponent<GraphMe>());
			gCash.Add(gc.transform.Find("line"+i).GetComponent<GraphMe>());
			gCapital.Add(gtc.transform.Find("line"+i).GetComponent<GraphMe>());
		}
		#endif

		var prefab = Resources.Load("Agent");
		for (int i = transform.childCount; i < numAgents; i++)
		{
            GameObject go = Instantiate(prefab) as GameObject; 
			go.transform.parent = transform;
			go.name = "agent" + i.ToString();
		}
		/* initialize agents */
		foreach (Transform tChild in transform)
		{
			GameObject child = tChild.gameObject;
			var agent = child.GetComponent<EconAgent>();

			string type = "invalid";
			int numPerType = 2; //transform.childCount / 5;
			int typeNum = 1;

			if (count < 3) 		type = "Food";
			else if (count < 4) 	type = "Wood";  //woodcutter
#if false
			else if (count < numPerType*3) 	type = "Ore";	//miner
			else if (count < numPerType*4) 	type = "Metal";	//refiner
			else if (count < numPerType*5) 	type = "Tool";	//blacksmith
#endif

			Debug.Log("agent type: " + type);
			if (type == "invalid")
				Debug.Log("2agent type: " + type);
			else {
				InitAgent(agent, type);
				agents.Add(agent);
			}
			count++;
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

	void PrintTrackBids()
	{
        string print = "Track Wood: ";
		foreach (var entry in trackBids["Wood"])
		{
			print += entry.Key + "," + entry.Value.ToString("c2") + " ";
		}
		Debug.Log(print);
	}

	void ClearTrackBids()
	{
		foreach (var item in trackBids)
		{
			foreach (var item2 in item.Value)
			{
				item.Value[item2.Key] = 0;
			}
		}
	}
	
	void InitAgent(EconAgent agent, string type)
	{
        agent.debug++;
        List<string> buildables = new List<string>();
		buildables.Add(type);
		var _initStock = UnityEngine.Random.Range(initStock/2, initStock*2);
		_initStock = Mathf.Floor(_initStock);
		var _maxStock = Mathf.Max(initStock, maxStock);

        agent.Init(initCash, buildables, _initStock, _maxStock);
	}
	
	// Update is called once per frame
	int roundNumber = 0;
	void FixedUpdate () {
		if (roundNumber > 100)
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
		//wait 1s before update
		if (Time.time - lastTick > tickInterval)
		{
			Debug.Log("v1.3 Round: " + roundNumber++);
			//sampler.BeginSample("AuctionHouseTick");
            Tick();
			//sampler.EndSample();
			lastTick = Time.time;
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

		//resolve prices
		foreach (var entry in com)
		{
			ResolveOffers(entry.Value);
			Debug.Log(entry.Key + ": goods: " + entry.Value.trades[^1] + " at price: " + entry.Value.prices[^1]);
		}
		
		Debug.Log("post resolve prices");

		//PrintToFile("round, " + roundNumber + ", commodity, " + commodity + ", price, " + averagePrice);
		AgentsStats();
		Debug.Log("post agent stats");
		CountProfits();
		Debug.Log("post count profits");
		CountProfessions();
		Debug.Log("post count professions");
		CountStockPileAndCash();
		Debug.Log("post count stock pile and cash");
		EnactBankruptcy();
		Debug.Log("post enact bankruptcy");
		//SetGraph(gCapital, "Debt", defaulted);
		
		//PrintTrackBids();
		//ClearTrackBids();
	}

	void PrintAuctionStats(String c, float buy, float sell)
	{
		String header = roundNumber + ", auction, none, " + c + ", ";
		String msg = header + "bid, " + buy + "\n";
		msg += header + "ask, " + sell + "\n";
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
			Assert.IsTrue(clearingPrice > 0);
			if (clearingPrice < 0 || clearingPrice > 1000)
			{
				Debug.Log(commodity.name + " clearingPrice: " + clearingPrice + " ask: " + ask.price + " bid: " + bid.price);
			}
			//trade
			var tradeQuantity = Mathf.Min(bid.remainingQuantity, ask.remainingQuantity);
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
			buyers[bid.agent.buildables[0]] += clearingPrice * boughtQuantity;

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
			moneyExchanged += clearingPrice * boughtQuantity;
			goodsExchanged += boughtQuantity;
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
		Debug.Log(roundNumber + ": " + commodity.name + ": " + goodsExchanged + " traded at average price of " + averagePrice);
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
		sw = new StreamWriter("log2.csv");
		String header_row = "round, agent, produces, commodity_stock, type, cs_amount\n";
		PrintToFile(header_row);
	}
	void PrintToFile(String msg) {
		sw.Write(msg);
	}

	void CloseWriteFile() {
		sw.Close();
	}

	void AgentsStats() {
		String header = roundNumber + ", ";
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
			foreach(var c in agent.stockPile)
			{
				stockPile[c.Key] += c.Value.Surplus();
				var surplus = c.Value.Surplus();
				if (surplus > 20)
				{
					//Debug.Log(agent.name + " has " + surplus + " " + c.Key);
				}
				stockList[c.Key].Add(surplus);
			}
            cashList[agent.buildables[0]].Add(agent.cash);
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
            var commodity = agent.buildables[0];
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
		Debug.Log("enacting bankruptcy!");
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
			professions[agent.buildables[0]] += 1;
        }

		foreach (var entry in professions)
		{
			//Debug.Log("Profession: " + entry.Key + ": " + entry.Value);
			//SetGraph(gProfessions, entry.Key, entry.Value);
		}
	}
	void SetGraph(List<GraphMe> graphs, string commodity, float input)
	{
        if (commodity == "Food") graphs[0].Tick(input);
        if (commodity == "Wood") graphs[1].Tick(input);
        if (commodity == "Ore") graphs[2].Tick(input);
        if (commodity == "Metal") graphs[3].Tick(input);
        if (commodity == "Tool") graphs[4].Tick(input);
        if (commodity == "Total") graphs[5].Tick(input);
        if (commodity == "Debt") graphs[6].Tick(input);
        if (commodity == "IRS") graphs[7].Tick(input);
    }
}
