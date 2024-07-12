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
public class Trade
{
	public Trade(string c, float p, float q, EconAgent a)
	{
		commodity = c;
		price = p;
		quantity = q;
		agent = a;
	}
	public float Reduce(float q)
	{
		quantity -= q;
		return quantity;
	}
	public void Print()
	{
		Debug.Log(agent.gameObject.name + ": " + commodity + " trade: " + price + ", " + quantity);
	}
	public string commodity { get; private set; }
	public float price { get; private set; }
	public float quantity { get; private set; }
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
    public float tickInterval = .02f;
    public int numAgents = 100;
	public float initCash = 100;
	public float initStock = 15;
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
			int numPerType = transform.childCount / 5;
			int typeNum = 1;
			if (count < numPerType*typeNum++) 		type = "Food";	//farmer
			else if (count < numPerType*typeNum++) 	type = "Wood";	//woodcutter
			else if (count < numPerType*typeNum++) 	type = "Ore";	//miner
			else if (count < numPerType*typeNum++) 	type = "Metal";	//refiner
			else if (count < numPerType*typeNum) 	type = "Tool";	//blacksmith
			else Debug.Log(count + " too many agents, not supported: " + typeNum);
			InitAgent(agent, type);
			agents.Add(agent);
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
		var _maxStock = Mathf.Max(initStock, maxStock);

        agent.Init(initCash, buildables, _initStock, _maxStock);
	}
	
	// Update is called once per frame
	int roundNumber = 0;
	void FixedUpdate () {
		if (roundNumber > 20)
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
			Debug.Log("v1.2 Round: " + roundNumber++);
			//sampler.BeginSample("AuctionHouseTick");
            Tick();
			//sampler.EndSample();
			lastTick = Time.time;
		}
	}
	void Tick()
	{
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
			float moneyExchanged = 0;
			float goodsExchanged = 0;
			var commodity = entry.Key;
			var asks = askTable[commodity];
            var bids = bidTable[commodity];
			var demand = bids.Count / Mathf.Max(.01f, (float)asks.Count);
            
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
                    Debug.Log(hTrade.agent.name + " bid " + commodity + " more than $1000: " + hTrade.price);
            }
            /******* end debug */

			bool switchBasedOnNumBids = true;
			if (switchBasedOnNumBids)
            {
                var numBids = bids.Sum(item => item.quantity);
                var numAsks = asks.Sum(item => item.quantity);
                entry.Value.bids.Add(numBids);
                entry.Value.asks.Add(numAsks);
			}
            else
            {
                entry.Value.bids.Add(bids.Count);
                entry.Value.asks.Add(asks.Count);
            }

            asks.Shuffle();
			bids.Shuffle();
			
			asks.Sort((x, y) => x.price.CompareTo(y.price)); //inc
			bids.Sort((x, y) => y.price.CompareTo(x.price)); //dec

			//Debug.Log(commodity + " asks sorted: "); asks.Print();
			//Debug.Log(commodity + " bids sorted: "); bids.Print();
			float failsafe = 0;
            while (asks.Count > 0 && bids.Count > 0)
            {
                //get highest bid and lowest ask
				int askIndex = 0;
				var ask = asks[askIndex];
				int bidIndex = 0;
				var bid = bids[bidIndex];
				//set price
				var clearingPrice = (bid.price + ask.price) / 2;
				if (clearingPrice < 0 || clearingPrice > 1000)
				{
					Debug.Log(commodity + " clearingPrice: " + clearingPrice + " ask: " + ask.price + " bid: " + bid.price);
				}
				//trade
				var tradeQuantity = Mathf.Min(bid.quantity, ask.quantity);
#if false
				Trade(commodity, clearingPrice, bid.agent, ask.agent);
#else
				var boughtQuantity = bid.agent.Buy(commodity, tradeQuantity, clearingPrice);
				ask.agent.Sell(commodity, boughtQuantity, clearingPrice);
#endif
				//track who bought what
				var buyers = trackBids[commodity];
				buyers[bid.agent.buildables[0]] += clearingPrice * boughtQuantity;
                //remove ask/bid if fullfilled
                if (ask.Reduce(boughtQuantity) == 0) { 
					asks.RemoveAt(askIndex);
					failsafe = 0;
				}
				if (bid.Reduce(boughtQuantity) == 0) { 
					bids.RemoveAt(bidIndex);
					failsafe = 0;
				}
				failsafe++;
				if (failsafe > 1000)
				{
					Debug.Log("Can't seem to sell: " + commodity + " bought: " + boughtQuantity + " for " + clearingPrice.ToString("c2"));
					asks.RemoveAt(askIndex);
					//break;
				}
				moneyExchanged += clearingPrice * boughtQuantity;
				goodsExchanged += boughtQuantity;
            }
			if (goodsExchanged == 0)
			{
				goodsExchanged = 1;
			} else 
			{
				Debug.Log("ERROR " + commodity + " had negative exchanges!?!?!");
				Assert.IsFalse(goodsExchanged < 0);
			}
			
			var averagePrice = moneyExchanged/goodsExchanged;
			if (float.IsNaN(averagePrice))
			{
				Debug.Log(commodity + ": average price is nan");
				Assert.IsFalse(float.IsNaN(averagePrice));
			}
			SetGraph(gMeanPrice, commodity, averagePrice);
			SetGraph(gUnitsExchanged, commodity, goodsExchanged);
			entry.Value.trades.Add(goodsExchanged);
			entry.Value.prices.Add(averagePrice);
            //reject the rest
            foreach (var ask in asks)
			{
				ask.agent.RejectAsk(commodity, averagePrice);
			}
			asks.Clear();
			foreach (var bid in bids)
			{
				bid.agent.RejectBid(commodity, averagePrice);
			}
			bids.Clear();

			//calculate supply/demand
			//var excessDemand = asks.Sum(ask => ask.quantity);
			//var excessSupply = bids.Sum(bid => bid.quantity);
			//var demand = (goodsExchanged + excessDemand) 
			//					 / (goodsExchanged + excessSupply);

            entry.Value.Update(averagePrice, demand);
		}

		//PrintToFile("round, " + roundNumber + ", commodity, " + commodity + ", price, " + averagePrice);
		AgentsStats();
		CountStockPileAndCash();
		CountProfits();
		EnactBankruptcy();
		
		//PrintTrackBids();
		//ClearTrackBids();
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
		String header_row = "round, agent, produces, commodity_stock, type, cs_amount, \n";
		PrintToFile(header_row);
	}
	void PrintToFile(String msg) {
		sw.Write(msg + "\n");
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
            SetGraph(gStocks, stock.Key, avg);

            if (avg > 20)
            {
                //Debug.Log(stock.Key + " HIGHSTOCK: " + avg.ToString("n2") + " max: " + stockList[stock.Key].Max().ToString("n2") + " num: " + stockList[stock.Key].Count);
            }

            //avg = GetQuantile(cashList[stock.Key], bucket, index);
            SetGraph(gCapital, stock.Key, cashList[stock.Key].Sum());
        }
        SetGraph(gCapital, "Total", totalCash);
		SetGraph(gCapital, "IRS", irs+totalCash);
		
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
            SetGraph(gCash, commodity, profit);
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
            irs -= agent.Tick();
        }
		SetGraph(gCapital, "Debt", defaulted);
		CountProfessions();
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
			SetGraph(gProfessions, entry.Key, entry.Value);
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
