using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
using AYellowpaper.SerializedCollections;
using UnityEngine.Rendering;
using Sirenix.OdinInspector;
using ChartAndGraph;
using EconSim;
using Sirenix.Serialization;
using Sirenix.OdinInspector.Editor.ValueResolvers;

public class AuctionHouse : MonoBehaviour {
	[Required]
	public InfoDisplay info;
	public SimulationConfig config;
	public AuctionStats auctionTracker;

	[ShowInInspector]
	FiscalPolicy fiscalPolicy;
	public ProgressivePolicy progressivePolicy;
	public FlatTaxPolicy FlatTaxPolicy;
	// [ValueDropdown("FiscalPolicies")]
	// public FiscalPolicy fiscalPolicy;


	protected List<EconAgent> agents = new();
	protected bool timeToQuit = false;
    protected OfferTable askTable, bidTable;
	protected float lastTick;
	ESStreamingGraph meanPriceGraph;
	public Government gov { get; protected set; }
	float moneyExchangedThisRound = 0;
	float goodsExchangedThisRound = 0;
	protected Logger logger;
	private TradeResolution tradeResolver;

	void Awake()
	{
		auctionTracker = GetComponent<AuctionStats>();
		config = GetComponent<SimulationConfig>();
		auctionTracker.config = config;
		auctionTracker.Init();
	}
	void Start()
	{
		Debug.unityLogger.logEnabled=config.EnableDebug;
		logger = new Logger(config);
		logger.OpenFileForWrite();
		string header_row = "round, agent, produces, inventory_items, type, amount, reason\n";
		logger.PrintToFile(header_row);

		UnityEngine.Random.InitState(config.seed);
		lastTick = 0;

		meanPriceGraph = GetComponent<ESStreamingGraph>();
		Assert.IsFalse(meanPriceGraph == null);

		InitGovernment();
		InitAgents();
		
		progressivePolicy.gov = gov;
		progressivePolicy.config = config;
		progressivePolicy.auctionStats = auctionTracker;
		fiscalPolicy = progressivePolicy;

		var com = auctionTracker.book;
		askTable = new OfferTable(com);
        bidTable = new OfferTable(com);

        switch (config.tradeResolution)
        {
	        case TradeResolutionType.XEven:
		        tradeResolver = new XEvenResolution(auctionTracker, fiscalPolicy, askTable, bidTable);
		        break;
	        case TradeResolutionType.OmiType:
		        tradeResolver = new OmisTradeResolution(auctionTracker, fiscalPolicy, askTable, bidTable);
		        break;
	        case TradeResolutionType.SimonType:
		        tradeResolver = new SimonTradeResolution(auctionTracker, fiscalPolicy, askTable, bidTable);
		        break;
	        default:
		        Assert.IsTrue(false, "Unknown trade resolution");
		        break;
        }
	}
	void InitGovernment()
	{
		if (config.EnableGovernment == false)
			return;
		GameObject go = new GameObject();
		go.transform.parent = transform;
		go.name = "gov";
		gov = go.AddComponent<Government>();
		Debug.Log(gov.name + " 1outputs: " + string.Join(", ", gov.outputName));
        string buildable = "Government";
		float initStock = 10f;

		var maxStock = Mathf.Max(initStock, 200);
        gov.Init(config, auctionTracker, buildable, initStock, maxStock);
        
		agents.Add(gov);
	}
	void InitAgents()
	{
		GameObject prefab;
		if (config.SimpleAgent == true)
		{
			prefab = (GameObject)Resources.Load("SimpleAgent");
		} else
		{
			prefab = (GameObject)Resources.Load("Agent");
		}
		var professions = config.numAgents.Keys;
		int agentId = 0;
		foreach (string profession in professions)
		{
			for (int i = 0; i < config.numAgents[profession]; ++i)
			{
				GameObject go = Instantiate(prefab) as GameObject;
				go.transform.parent = transform;
				go.name = "agent" + agentId.ToString();
			
				var agent = go.GetComponent<EconAgent>();
				InitAgent(agent, profession);
				agents.Add(agent);
				agentId++;
			}
		}
	}
	void InitAgent(EconAgent agent, string type)
	{
        string buildable = type;
		float initStock = config.initStock;
		if (config.randomInitStock)
		{
			initStock = UnityEngine.Random.Range(config.initStock/2, config.initStock*2);
			initStock = Mathf.Floor(initStock);
		}

		// This may cause uneven maxStock between agents
		var maxStock = Mathf.Max(initStock, config.maxStock);

        agent.Init(config, auctionTracker, buildable, initStock, maxStock);
	}

	void OnApplicationQuit() 
	{
		//CloseWriteFile();
	}

	[Title("Player Actions")]
	[Button(ButtonSizes.Large), GUIColor(0.4f, 0.8f,1)]
	public void DoNextRound()
	{
		Tick();
		auctionTracker.nextRound();
		meanPriceGraph.UpdateGraph();
	}
	[HideIf("forestFire")]
	[Button(ButtonSizes.Large), GUIColor(0.4f, 0.8f,1)]
	public void ForestFire()
	{
		//TODO rapid decline (*.6 every round) for 2-5 rounds, then regrow at 1.1 until reaches back to 1 multiplier
		//do this in a new class
		var wood = auctionTracker.book["Wood"];
		var weight = wood.productionMultiplier;
		weight = .2f;

		wood.ChangeProductionMultiplier(weight);

		forestFire = true;
	}
	[ShowIf("forestFire")]
	[Button(ButtonSizes.Large), GUIColor(1, 0.4f, 0.4f)]
	public void StopForestFire()
	{
		var wood = auctionTracker.book["Wood"];
		var weight = wood.productionMultiplier;
		weight = 1f;
		wood.ChangeProductionMultiplier(weight);

		forestFire = false;
	}
	private static string[] comOptions = new string[] { "Food","Wood","Ore","Metal","Tool" };
	[PropertyOrder(4)]
	[HorizontalGroup("InsertBid")]
	[ValueDropdown("comOptions")]
	public string bidCom = "Food";

	[PropertyOrder(4)]
	[HorizontalGroup("InsertBid")]
	public float bidQuant = 0;
	[PropertyOrder(4)]
	[HorizontalGroup("InsertBid")]
	[Button]
	public void InsertBid() 
	{
		((Government)gov).InsertBid(bidCom, bidQuant, 0f);
	}
	bool forestFire = false;
	
	void Update () {
		if (auctionTracker.round > config.maxRounds || timeToQuit)
		{
			logger.CloseWriteFile();
#if UNITY_EDITOR
			return;
			//UnityEditor.EditorApplication.isPlaying = false;
#elif UNITY_WEBPLAYER
        Application.OpenURL("127.0.0.1");
#else
        Application.Quit();
#endif
			return;
		}

		if (config.autoNextRound && Time.time - lastTick > config.tickInterval)
		{
			Debug.Log("v1.4 Round: " + auctionTracker.round);
			// if (auctionTracker.round == 100)
			// 	ForestFire();
			// if (auctionTracker.round == 200)
			// 	StopForestFire();
			DoNextRound();
			lastTick = Time.time;
		}
	}
	void Tick()
	{
		//check total cash held by agents and government
		var totalCash = agents.Sum(x => x.Cash);
		Debug.Log("Auction House tick: Total cash: " + totalCash);

		var book = auctionTracker.book;
		foreach (var agent in agents)
		{
			if (agent.Alive == false)
				continue;
			agent.ConsumeGoods();
			agent.Produce();
			//var numProduced = agent.Produce(book);
			//PayIdleTax(agent, numProduced);

			agent.Decide();
			askTable.Add(agent.CreateAsks());
			bidTable.Add(agent.Consume(book));
		}

		//resolve prices
		foreach (var entry in book)
		{
			moneyExchangedThisRound = 0;
			goodsExchangedThisRound = 0;
			tradeResolver.ResolveOffers(entry.Value, ref moneyExchangedThisRound, ref goodsExchangedThisRound);
			RecordStats(entry.Value);
			Debug.Log(entry.Key + ": have " + entry.Value.trades[^1] 
				+ " at price: " + entry.Value.marketPrice.ToString("c2"));
		}

		PrintAuctionStats();

		progressivePolicy.Tax(book, agents);
		foreach (var agent in agents) //including gov
		{
			agent.CalculateProfit();
		}
		logAgentsStats();
		auctionTracker.ClearStats();
		TickAgent();
		QuitIf();
	}
	protected void TickAgent()
	{
		var book = auctionTracker.book;
		var approval = 0f;
		
		Debug.Log(auctionTracker.round + " gov outputs: " + gov.outputName);
        foreach (var agent in agents)
        {
			if (agent.Alive == false)
				continue;
	        Debug.Log("TickAgent() " + agent.name);
			if (agent is Government)
				continue;
			bool changedProfession = false;
			bool bankrupted = false;
			bool starving = false;
			string profession = agent.Profession;


			book[profession].numAgents++;

			approval += agent.EvaluateHappiness();

			if (agent.Cash < 0.0f)
				book[profession].numBankrupted++;

			if (agent.CalcMinProduction() < 1)
				book[profession].numNoInput++;
			
			if (agent.Profit < 0)
				book[profession].numNegProfit++;

			book[profession].profits[^1] += agent.Profit;
			
			var amount = agent.Tick(gov, ref changedProfession, ref bankrupted, ref starving);
			gov.Pay(amount); //welfare?

			if (starving)
			{
				book[profession].starving[^1]++;
				book[profession].numStarving++;
			}
			if (bankrupted)
				book[profession].bankrupted[^1]++;
			if (changedProfession)
				book[profession].changedProfession[^1]++;
			// Debug.Log(agent.name + " total cash line: " + agents.Sum(x => x.cash).ToString("c2") + amount.ToString("c2"));

			agent.ClearRoundStats();
		}

		float inflation = 0;
		foreach (var rsc in book.Values)
		{
			rsc.happiness /= rsc.numAgents;
			rsc.gdp = rsc.trades[^1] * rsc.marketPrice;
			auctionTracker.gdp += rsc.gdp;

			auctionTracker.numBankrupted += rsc.numBankrupted;
			auctionTracker.numStarving += rsc.numStarving;
			auctionTracker.numNoInput += rsc.numNoInput;
			auctionTracker.numNegProfit += rsc.numNegProfit;
			rsc.numChangedProfession = (int)rsc.changedProfession[^1];
			auctionTracker.numChangedProfession += rsc.numChangedProfession;

			var prevPrice = rsc.avgClearingPrice[^2];
			var currPrice = rsc.avgClearingPrice[^1];
			if (prevPrice != 0)
				inflation += (currPrice - prevPrice) / prevPrice;
			Debug.Log("inflation current is " + inflation.ToString("p2"));
		}

		inflation /= 3f;//(float)book.Count;
		auctionTracker.inflation = (!float.IsNaN(inflation) && !float.IsInfinity(inflation)) ? inflation : 0;
		auctionTracker.happiness = approval / agents.Count;
		auctionTracker.approval = approval / agents.Count;
		auctionTracker.gini = GetGini(GetWealthOfAgents());
	}
	void PrintAuctionStats()
	{
		if (!config.EnableLog)
			return;
		var header = auctionTracker.round + ", auction, none, none, ";
		var msg = header + "irs, " + gov.Cash + ", n/a\n";
		msg += header + "taxed, " + fiscalPolicy.taxed + ", n/a\n";
		msg += auctionTracker.GetLog();
		msg += info.GetLog(header);

		logger.PrintToFile(msg);
	}

	protected void PrintAuctionStats(string c, float buy, float sell)
	{
		if (!config.EnableLog)
			return;
		string header = auctionTracker.round + ", auction, none, " + c + ", ";
		string msg = header + "bid, " + buy + ", n/a\n";
		msg += header + "ask, " + sell + ", n/a\n";
		msg += header + "avgAskPrice, " + auctionTracker.book[c].avgAskPrice[^1] + ", n/a\n";
		msg += header + "avgBidPrice, " + auctionTracker.book[c].avgBidPrice[^1] + ", n/a\n";

		logger.PrintToFile(msg);
	}
	protected void RecordStats(ResourceController rsc)
	{
		var asks = askTable[rsc.name];
		var bids = bidTable[rsc.name];

		var agentDemandRatio = bids.Count / Mathf.Max(.01f, (float)asks.Count); //demand by num agents bid/
		var quantityToBuy = bids.Sum(item => item.offerQuantity);
		var quantityToSell = asks.Sum(item => item.offerQuantity);

		rsc.bids.Add(quantityToBuy);
		rsc.asks.Add(quantityToSell);
		rsc.buyers.Add(bids.Count);
		rsc.sellers.Add(asks.Count);

		var avgAskPrice = (quantityToSell == 0) ? 0 : asks.Sum((x) => x.offerPrice * x.offerQuantity) / quantityToSell;
		rsc.avgAskPrice.Add(avgAskPrice);

		var avgBidPrice = (quantityToBuy == 0) ? 0 : bids.Sum((x) => x.offerPrice * x.offerQuantity) / quantityToBuy;
		rsc.avgBidPrice.Add(avgBidPrice);

		var averagePrice = (goodsExchangedThisRound == 0) ? 0 : moneyExchangedThisRound / goodsExchangedThisRound;

		Debug.Log(auctionTracker.round + " " + rsc.name + " avgprice: " + averagePrice.ToString("c2") + " goods exchanged: " + goodsExchangedThisRound.ToString("n2") + " money exchanged: " + moneyExchangedThisRound.ToString("c2"));
		Assert.IsTrue(averagePrice >= 0f);
		rsc.avgClearingPrice.Add(averagePrice);
		rsc.trades.Add(goodsExchangedThisRound);
		var marketPrice = averagePrice;
		if (goodsExchangedThisRound == 0)
			marketPrice = rsc.marketPrice;
		rsc.Update(marketPrice, agentDemandRatio);

		//update price beliefs if still a thing
		asks.Clear();
		bids.Clear();

		PrintAuctionStats(rsc.name, quantityToBuy, quantityToSell);
		Debug.Log(auctionTracker.round + ": " + rsc.name + ": " + goodsExchangedThisRound + " traded at average price of " + averagePrice.ToString("c2"));
	}

	// TODO decouple transfer of commodity with transfer of money
	// TODO convert cash into another commodity

	protected bool Transfer(EconAgent source, EconAgent destination, string commodity, float quant)
	{
		if (source.inventory[commodity].Quantity >= quant)
		{
			source.inventory[commodity].Decrease(quant);
			destination.inventory[commodity].Increase(quant);
			return true;
		} else {
			return false;
		}
	}

	protected void logAgentsStats() {
		if (!config.EnableLog)
			return;
		string header = auctionTracker.round + ", ";
		string msg = "";
		foreach (var agent in agents)
		{
			msg += agent.Stats(header);
		}
		logger.PrintToFile(msg);
	}
	protected void QuitIf()
	{
		if (!config.exitAfterNoTrade)
		{
			return;
		}
		foreach (var entry in auctionTracker.book)
		{
			var commodity = entry.Key;
			var tradeVolume = entry.Value.trades.LastSum(config.numRoundsNoTrade);
			if (auctionTracker.round > config.numRoundsNoTrade && tradeVolume == 0)
			{
				Debug.Log("quitting!! last " + config.numRoundsNoTrade + " round average " + commodity + " was : " + tradeVolume);
				timeToQuit = true;
				//TODO should be no trades in n rounds
			} else {
				Debug.Log("last " + config.numRoundsNoTrade + " round trade average for " + commodity + " was : " + tradeVolume);
			}
		}
	}

	public List<float> GetWealthOfAgents()
	{
		return agents.Where(x => x is not Government).Select(x => x.Cash).ToList();
	}
    float GetGini(List<float> values)
    {
        values.Sort();
        // string msg = ListUtil.ListToString(cashList, "c2");
        // Debug.Log("cash: " + msg);
        int n = values.Count;
        if (n == 0) return auctionTracker.gini;

        float totalWealth = values.Sum();
        Assert.IsTrue(totalWealth != 0);
        float cumulativeWealth = 0;
        float weightedSum = 0;
        for (int i = 0; i < n; i++)
        {
            cumulativeWealth += values[i];
            weightedSum += (i + 1) * values[i];
        }

        // Gini coefficient formula
        float gini = (2.0f * weightedSum) / (n * totalWealth) - (n + 1.0f) / n;
        Assert.IsFalse(float.IsNaN(gini));
        return gini;
    }
}
