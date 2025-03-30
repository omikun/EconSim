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
using UnityEngine.Serialization;

public partial class AuctionHouse : MonoBehaviour {
	[Required]
	public InfoDisplay info;
	[SerializeField] public SimulationConfig config;
	[SerializeField] public AuctionStats district;

	[ShowInInspector] public int FrameRate = 60;
	[ShowInInspector]
	public FiscalPolicy fiscalPolicy { get; private set; }
	[ShowInInspector]
	public ProgressivePolicy progressivePolicy = new();
	[HideInInspector]
	public FlatTaxPolicy FlatTaxPolicy;
	// [ValueDropdown("FiscalPolicies")]
	// public FiscalPolicy fiscalPolicy;


	protected bool timeToQuit = false;
    public OfferTable askTable { get; protected set; }
    public OfferTable bidTable { get; protected set; }
	protected float lastTick;
	ESStreamingGraph streamingGraphs;
	public Government gov { get; protected set; }
	public Bank bank { get; protected set; }

	public AuctionStats AuctionStats
	{
		get { return district; }
	}

	private TradeResolution tradeResolver;
	public Logger logger;

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
		agentManager = new AgentManager(this);
		InitBank();
	}
	void Start()
	{
		Debug.unityLogger.logEnabled=config.EnableDebug;
		logger = new Logger(config);

		UnityEngine.Random.InitState(config.seed);
		lastTick = 0;

		streamingGraphs = GetComponent<ESStreamingGraph>();
		Assert.IsFalse(streamingGraphs == null);

		InitGovernment();
		AgentManager.InitAgents();
		
		progressivePolicy.Init(config, district, gov);
		progressivePolicy.gov = gov;
		progressivePolicy.config = config;
		progressivePolicy.auctionStats = district;
		fiscalPolicy = progressivePolicy;

		var book = district.book;
		askTable = new OfferTable(book);
        bidTable = new OfferTable(book);

        CreateTradeResolver();
		UpdateAgentTable();
	}

	private void CreateTradeResolver()
	{
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

        district.gov = gov;
        AgentManager.agents.Add(gov);
	}
	
	void InitBank()
	{
		var go = transform.Find("Bank").gameObject;
		bank = go.GetComponent<Bank>();
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
		AgentManager.agents.Add(bank);
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
		var totalCash = AgentManager.agents.Sum(x => x.Cash) + district.bank.Monies();
		var totalDebt = AgentManager.agents.Sum(x => district.bank.QueryLoans(x));

		Debug.Log("Auction House tick: Total cash: " + totalCash + " Total debt: " + totalDebt + " net: " + (totalCash - totalDebt));

		district.bank.CollectPayments();
		
		var book = district.book;
		foreach (var agent in AgentManager.agents)
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
			district.RecordStats(entry.Value, stats);
			Debug.Log(entry.Key + ": have " + entry.Value.trades.Last()
				+ " at price: " + entry.Value.marketPrice.ToString("c2"));
		}

		district.PrintAuctionStats();

		foreach (var agent in AgentManager.agents) //including gov
			agent.CalculateProfit();
		
		progressivePolicy.Tax(book, AgentManager.agents);
		
		foreach (var agent in AgentManager.agents) //including gov
			agent.UpdatePrevCash();
		
		logAgentsStats();
		district.ClearStats();
		AgentManager.TickAgent();
		QuitIf();
	}

	// TODO decouple transfer of commodity with transfer of money
	// TODO convert cash into another commodity

	protected bool Transfer(EconAgent source, EconAgent destination, string commodity, float quant)
	{
		if (source.inventory[commodity].Quantity >= quant)
		{
			Assert.IsFalse(commodity == "Labor");
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
		foreach (var agent in AgentManager.agents)
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

	
}
