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
using UnityEngine.Serialization;

public partial class AuctionHouse : MonoBehaviour {
	[Required]
	public InfoDisplay info;
	public SimulationConfig config;
	[FormerlySerializedAs("auctionTracker")] public AuctionStats district;

	[ShowInInspector] public int FrameRate = 60;
	[ShowInInspector]
	FiscalPolicy fiscalPolicy;
	public ProgressivePolicy progressivePolicy;
	public FlatTaxPolicy FlatTaxPolicy;
	// [ValueDropdown("FiscalPolicies")]
	// public FiscalPolicy fiscalPolicy;


	[HideInInspector]
	public List<EconAgent> agents = new();
	protected bool timeToQuit = false;
    protected OfferTable askTable, bidTable;
	protected float lastTick;
	ESStreamingGraph streamingGraphs;
	public Government gov { get; protected set; }
	protected Logger logger;
	private TradeResolution tradeResolver;

	void Awake()
	{
#if UNITY_EDITOR
		QualitySettings.vSyncCount = 2;  // VSync must be disabled
		Application.targetFrameRate = FrameRate;
#endif
		district = GetComponent<AuctionStats>();
		config = GetComponent<SimulationConfig>();
		district.config = config;
		district.Init();
		InitBank();
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

		streamingGraphs = GetComponent<ESStreamingGraph>();
		Assert.IsFalse(streamingGraphs == null);

		InitGovernment();
		InitAgents();
		
		progressivePolicy.gov = gov;
		progressivePolicy.config = config;
		progressivePolicy.auctionStats = district;
		fiscalPolicy = progressivePolicy;

		var book = district.book;
		askTable = new OfferTable(book);
        bidTable = new OfferTable(book);

        switch (config.tradeResolution)
        {
	        case TradeResolutionType.XEven:
		        tradeResolver = new XEvenResolution(district, fiscalPolicy, askTable, bidTable);
		        break;
	        case TradeResolutionType.OmiType:
		        tradeResolver = new OmisTradeResolution(district, fiscalPolicy, askTable, bidTable);
		        break;
	        case TradeResolutionType.SimonType:
		        tradeResolver = new SimonTradeResolution(district, fiscalPolicy, askTable, bidTable);
		        break;
	        default:
		        Assert.IsTrue(false, "Unknown trade resolution");
		        break;
        }
		UpdateAgentTable();
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
        gov.Init(config, district, buildable, initStock, maxStock);
        
		agents.Add(gov);
	}
	
	void InitBank()
	{
		var go = transform.Find("Bank").gameObject;
		var bank = go.GetComponent<Bank>();
		bank.name = "Bank";
		bank.BankRegulations(config.fractionalReserveRatio, 
							 config.termInRounds, 
						 	 config.interestRate, 
						  	 config.maxMissedPayments, 
						  	 config.maxPrinciple,
						  	 config.maxNumDefaults);
        string buildable = "Bank";
        bank.Init(config, district, buildable, 0, 1200000);
		// var builtinregulations = go.GetComponent<BankRegulations>();
		// builtinregulations = regulations;
		bank.BankInit(100, "Cash");
		Debug.Log(bank.name + " 1outputs: " + string.Join(", ", bank.outputName));
        
		district.bank = bank;
		agents.Add(bank);
	}

	GameObject GetAgentPrefab()
	{
		GameObject prefab;
		if (config.agentType == AgentType.Simple)
		{
			prefab = (GameObject)Resources.Load("SimpleAgent");
		} else if (config.agentType == AgentType.Medium)
		{
			prefab = (GameObject)Resources.Load("MediumAgent");
		} else if (config.agentType == AgentType.User)
		{
			prefab = (GameObject)Resources.Load("UserAgent");
		} else
		{
			prefab = (GameObject)Resources.Load("Agent");
		}

		return prefab;
	}
	void InitAgents()
	{
		GameObject prefab = GetAgentPrefab();
		var professions = config.numAgents.Keys;
		foreach (string profession in professions)
		{
			for (int i = 0; i < config.numAgents[profession]; ++i)
			{
				var agent = NewAgent(prefab, profession);
				agents.Add(agent);
			}
		}
	}

	EconAgent NewAgent(GameObject prefab, string profession, float cash=-1f)
	{
		GameObject go = Instantiate(prefab) as GameObject;
		go.transform.parent = transform;
			
		var agent = go.GetComponent<EconAgent>();
		InitAgent(agent, profession, cash);
		go.name = "agent" + agent.uid; //uid only initialized after agent.Init
		return agent;

	}
	void InitAgent(EconAgent agent, string type, float cash=-1f)
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

        agent.Init(config, district, buildable, initStock, maxStock, cash);
	}

	void OnApplicationQuit() 
	{
		//CloseWriteFile();
	}

	//latch bid values in AgentTable to agent inventory offers
	public void LatchBids()
	{
		Debug.Log("Latching bids");
		// var entriesAgent = AgentTable.Zip(agents, (e, a) => new { entry = e, agent = a });
		// foreach(var ea in entriesAgent)
		for (int a = 0; a < AgentTable.Count && a < agents.Count; a++)
		{
			var agent = agents[a];
			var entry = AgentTable[a];
			
			string msg = agent.name + " offering ";
			foreach (var c in entry.Bids)
			{
				if (agent.inventory.ContainsKey(c.name) == false)
					Debug.Log("no key found");
				agent.inventory[c.name].offersThisRound = c.quantity;
				msg += c.name + ": " + c.quantity + " ";
			}
			Debug.Log(msg);
		}
	}
	

	void Update () {
		if (district.round > config.maxRounds || timeToQuit)
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
		}

		if (config.autoNextRound && Time.time - lastTick > config.tickInterval)
		{
			Debug.Log("v1.4 Round: " + district.round);
			// if (auctionTracker.round == 100)
			// 	ForestFire();
			// if (auctionTracker.round == 200)
			// 	StopForestFire();
			DoNextRound();
			lastTick = Time.time;
		}
	}
	public void Tick()
	{
		//check total cash held by agents and government
		var totalCash = agents.Sum(x => x.Cash) + district.bank.Monies();
		var totalDebt = agents.Sum(x => district.bank.QueryLoans(x));

		Debug.Log("Auction House tick: Total cash: " + totalCash + " Total debt: " + totalDebt + " net: " + (totalCash - totalDebt));

		district.bank.CollectPayments();
		
		var book = district.book;
		foreach (var agent in agents)
		{
			if (agent.Alive == false)
				continue;
			agent.Decide();
			//var numProduced = agent.Produce(book);
			//PayIdleTax(agent, numProduced);

			askTable.Add(agent.CreateAsks());
			bidTable.Add(agent.CreateBids(book));
		}

		//resolve prices
		foreach (var entry in book)
		{
			TradeStats stats = new();
			tradeResolver.ResolveOffers(entry.Value, ref stats);
			RecordStats(entry.Value, stats);
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
		district.ClearStats();
		TickAgent();
		QuitIf();
		//print agent to inspector
	}

	protected void TickAgent()
	{
		var book = district.book;
		var approval = 0f;
		
		Debug.Log(district.round + " gov outputs: " + gov.outputName);
		var newAgents = new List<EconAgent>();
		var deadAgents = new List<EconAgent>();
        foreach (var agent in agents)
        {
			if (agent.Alive == false)
				continue;
//	        Debug.Log("TickAgent() " + agent.name);
			if (agent is Government || agent is Bank)
				continue;
			bool changedProfession = false;
			bool bankrupted = false;
			bool starving = false;
			string profession = agent.Profession;


			if (profession != "Unemployed" && profession != "Labor")
			{
				book[profession].numAgents++;
				book[profession].numAgents += agent.NumEmployees;

				approval += agent.EvaluateHappiness();

				if (agent.Cash < 0.0f)
					book[profession].numBankrupted++;

				if (agent.CalcMinProduction() < 1)
					book[profession].numNoInput++;
			
				if (agent.Profit < 0)
					book[profession].numNegProfit++;

				book[profession].profits[^1] += agent.Profit;
			}
			
			var cash = agent.Tick(gov, ref changedProfession, ref bankrupted, ref starving);
			if (agent.Alive == false)
			{
				agent.gameObject.SetActive(false);
				deadAgents.Add(agent);
				if (district.bank.QueryLoans(agent) > 0)
					district.bank.LiquidateInventory(agent.inventory);
				else
					gov.LiquidateInventory(agent.inventory);
				continue;
			} else if (cash > 0)
			{
				if (cash == 1f)
					cash = 0;
				var prefab = GetAgentPrefab();
				var chance = UnityEngine.Random.Range(0f, 1f);
				var prof = (chance < .5f) ? "Wood" : "Ore";
				// spawn new agent! if 1 spawn as wood worker or ore miner
				// else spawn as most profitable profession
				// var newAgent = NewAgent(prefab, agent.Profession, amount);
				GameObject go = Instantiate(prefab) as GameObject;
				go.transform.parent = transform;
			
				var newAgent = go.GetComponent<EconAgent>();
				// InitAgent(newAgent, profession, cash);
				newAgent.Init(config, district, "Unemployed", 0, 50, cash);  //available for hire
				go.name = "agent" + newAgent.uid.ToString(); //uid only initialized after agent.Init
				newAgents.Add(newAgent);
				Debug.Log(district.round + " new agent: " + go.name + " uid: " + newAgent.uid.ToString());
			// Debug.Log(auctionStats.round + " New agent " + gameObject.name + " uid: " + uid + " cash: " + Cash.ToString("c2") + " has " + inventory[buildable].Quantity + " " + buildable);
			}
			// gov.Pay(amount); //welfare?

			if (profession != "Unemployed" && profession != "Labor")
			{
				if (starving)
				{
					book[profession].starving[^1]++;
					book[profession].numStarving++;
				}
				if (bankrupted)
					book[profession].bankrupted[^1]++;
				if (changedProfession)
					book[profession].changedProfession[^1]++;
			}
			// Debug.Log(agent.name + " total cash line: " + agents.Sum(x => x.cash).ToString("c2") + amount.ToString("c2"));

			agent.ClearRoundStats();
		}

        foreach (var agent in deadAgents)
        {
	        agents.Remove(agent);
        }
        foreach (var agent in newAgents)
	        agents.Add(agent);

		float inflation = 0;
		district.numStarving = agents.Sum(agent => (agent.DaysStarving > 0) ? 1 : 0);
		foreach (var rsc in book.Values)
		{
			rsc.happiness /= rsc.numAgents;
			rsc.gdp = rsc.trades[^1] * rsc.marketPrice;
			district.gdp += rsc.gdp;

			district.numBankrupted += rsc.numBankrupted;
			//district.numStarving += rsc.numStarving;
			district.numNoInput += rsc.numNoInput;
			district.numNegProfit += rsc.numNegProfit;
			rsc.numChangedProfession = (int)rsc.changedProfession[^1];
			district.numChangedProfession += rsc.numChangedProfession;

			var prevPrice = rsc.avgClearingPrice[^2];
			var currPrice = rsc.avgClearingPrice[^1];
			if (prevPrice != 0)
				inflation += (currPrice - prevPrice) / prevPrice;
			Debug.Log("inflation current for " + rsc.name + " is " + inflation.ToString("p2"));
		}

		inflation /= 3f;//(float)book.Count;
		district.inflation = (!float.IsNaN(inflation) && !float.IsInfinity(inflation)) ? inflation : 0;
		district.happiness = approval / agents.Count;
		district.approval = approval / agents.Count;
		district.gini = GetGini(GetWealthOfAgents());
	}
	void PrintAuctionStats()
	{
		if (!config.EnableLog)
			return;
		var header = district.round + ", auction, none, none, ";
		var msg = header + "irs, " + gov.Cash + ", n/a\n";
		msg += header + "taxed, " + fiscalPolicy.taxed + ", n/a\n";
		msg += district.GetLog();
		msg += info.GetLog(header);

		logger.PrintToFile(msg);
	}

	protected void PrintAuctionStats(string c, float buy, float sell)
	{
		if (!config.EnableLog)
			return;
		string header = district.round + ", auction, none, " + c + ", ";
		string msg = header + "bid, " + buy + ", n/a\n";
		msg += header + "ask, " + sell + ", n/a\n";
		msg += header + "avgAskPrice, " + district.book[c].avgAskPrice[^1] + ", n/a\n";
		msg += header + "avgBidPrice, " + district.book[c].avgBidPrice[^1] + ", n/a\n";

		logger.PrintToFile(msg);
	}
	protected void RecordStats(ResourceController rsc, TradeStats stats)
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

		var avgAskPrice = (quantityToSell == 0) ? rsc.avgAskPrice.Last() : asks.Sum((x) => x.offerPrice * x.offerQuantity) / quantityToSell;
		rsc.avgAskPrice.Add(avgAskPrice);
		var maxAskPrice = (stats.maxAskPrice == 0f) ? rsc.maxAskPrice.Last() : stats.maxAskPrice;
		rsc.maxAskPrice.Add(maxAskPrice);
		var minAskPrice = (stats.minAskPrice == float.MaxValue) ? rsc.minAskPrice.Last() : stats.minAskPrice;
		rsc.minAskPrice.Add(minAskPrice);

		var avgBidPrice = (quantityToBuy == 0) ? rsc.avgBidPrice.Last() : bids.Sum((x) => x.offerPrice * x.offerQuantity) / quantityToBuy;
		rsc.avgBidPrice.Add(avgBidPrice);
		var maxBidPrice = (stats.maxBidPrice == 0f) ? rsc.maxBidPrice.Last() : stats.maxBidPrice;
		rsc.maxBidPrice.Add(maxBidPrice);
		var minBidPrice = (stats.minBidPrice == float.MaxValue) ? rsc.minBidPrice.Last() : stats.minBidPrice;
		rsc.minBidPrice.Add(minBidPrice);

		var averagePrice = (stats.goodsExchangedThisRound == 0) ? rsc.avgClearingPrice.Last() : stats.moneyExchangedThisRound / stats.goodsExchangedThisRound;

		Debug.Log(district.round + " " + rsc.name + " avgprice: " + averagePrice.ToString("c2") + " goods exchanged: " + stats.goodsExchangedThisRound.ToString("n2") + " money exchanged: " + stats.moneyExchangedThisRound.ToString("c2"));
		Assert.IsTrue(averagePrice >= 0f);
		rsc.avgClearingPrice.Add(averagePrice);
		var maxClearingPrice = (stats.maxClearingPrice == 0f) ? rsc.maxClearingPrice.Last() : stats.maxClearingPrice;
		rsc.maxClearingPrice.Add(maxClearingPrice );
		var minClearingPrice = (stats.minClearingPrice == float.MaxValue) ? rsc.minClearingPrice.Last() : stats.minClearingPrice;
		rsc.minClearingPrice.Add(minClearingPrice);
		rsc.trades.Add(stats.goodsExchangedThisRound);
		var marketPrice = averagePrice;
		if (stats.goodsExchangedThisRound == 0)
			marketPrice = rsc.marketPrice;
		rsc.Update(marketPrice, agentDemandRatio);

		var totalInventory = agents.Sum(agent => (agent.inventory.Keys.Contains(rsc.name)) ? agent.inventory[rsc.name].Quantity : 0f);
		rsc.inventory.Add(totalInventory);

		var totalCash = agents.Sum(agent => (agent.outputName == rsc.name) ? agent.Cash : 0f)
			+ district.bank.Deposits.Sum(entry => (entry.Key.outputName == rsc.name) ? entry.Value : 0f);
		string msg = "";
		foreach (var agent in agents)
		{
			if (agent.outputName == rsc.name)
				msg += agent.name + " makes " + agent.outputName + " has cash " + agent.CashString + "\n";
		}

		Debug.Log(district.round + ": " + rsc.name + " cash list:\n " + msg);
		rsc.cash.Add(totalCash);
		
		foreach (var ask in asks)
			ask.agent.UpdateSellerPriceBelief(ask, rsc);
		foreach (var bid in bids)
			bid.agent.UpdateBuyerPriceBelief(bid, rsc);
		
		//update price beliefs if still a thing
		asks.Clear();
		bids.Clear();

		PrintAuctionStats(rsc.name, quantityToBuy, quantityToSell);
		Debug.Log(district.round + ": " + rsc.name + ": " + stats.goodsExchangedThisRound + " traded at average price of " + averagePrice.ToString("c2"));
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
		string header = district.round + ", ";
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
		foreach (var entry in district.book)
		{
			var commodity = entry.Key;
			var tradeVolume = entry.Value.trades.LastSum(config.numRoundsNoTrade);
			if (district.round > config.numRoundsNoTrade && tradeVolume == 0)
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
        if (n == 0) return district.gini;

        float totalWealth = values.Sum();
        if (totalWealth == 0)
	        return 0;
        
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
