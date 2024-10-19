using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
using AYellowpaper.SerializedCollections;
using UnityEngine.Rendering;
using Sirenix.OdinInspector;
using ChartAndGraph;
using Sirenix.Serialization;
using Sirenix.OdinInspector.Editor.ValueResolvers;

public class AuctionHouse : MonoBehaviour {
	public bool EnableDebug = false;
	public bool EnableLog = false;
	[Required]
	public InfoDisplay info;
	protected AgentConfig config;
	public int seed = 42;
	public bool appendTimeToLog = false;
	public int maxRounds = 10;
	public bool exitAfterNoTrade = true;
	public int numRoundsNoTrade = 100;
	
	[SerializedDictionary("Comm", "numAgents")]
	public SerializedDictionary<string, int> numAgents = new()
	{
		{ "Food", 3 },
		{ "Wood", 3 },
		{ "Ore", 3 },
		{ "Metal", 4 },
		{ "Tool", 4 }
	};

	[ShowInInspector]
	public ProgressivePolicy progressivePolicy;
	public FlatTaxPolicy FlatTaxPolicy;
	// [ValueDropdown("FiscalPolicies")]
	// public FiscalPolicy fiscalPolicy;

	public bool autoNextRound = false;
	//[CustomValueDrawer("TickIntervalDrawer")]
	[Range(.001f, 0.5f)]
    public float tickInterval = .001f;

	protected List<EconAgent> agents = new();
	protected float taxed;
	protected bool timeToQuit = false;
    protected OfferTable askTable, bidTable;
	protected StreamWriter sw;
	protected AuctionStats auctionTracker;
	protected Dictionary<string, Dictionary<string, float>> trackBids = new();
	protected float lastTick;
	ESStreamingGraph meanPriceGraph;
	public Government gov { get; private set; }

	void Awake()
	{
		auctionTracker = GetComponent<AuctionStats>();
		auctionTracker.Init();
	}
	void Start()
	{
		Debug.unityLogger.logEnabled=EnableDebug;
		OpenFileForWrite();

		UnityEngine.Random.InitState(seed);
		lastTick = 0;

		config = GetComponent<AgentConfig>();
		meanPriceGraph = GetComponent<ESStreamingGraph>();
		Assert.IsFalse(meanPriceGraph == null);

		var com = auctionTracker.book;
		taxed = 0;
		var prefab = Resources.Load("Agent");

		for (int i = transform.childCount; i < numAgents.Values.Sum(); i++)
		{
		    GameObject go = Instantiate(prefab) as GameObject;
			go.transform.parent = transform;
			go.name = "agent" + i.ToString();
		}

		//instiantiate gov
		{
			GameObject go = new GameObject();
			go.transform.parent = transform;
			go.name = "gov";
			gov = go.AddComponent<Government>();
			InitGovernment((Government)gov);
			agents.Add(gov);
		}
		progressivePolicy = new ProgressivePolicy(config, auctionTracker, gov);
		
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
		askTable = new OfferTable(com);
        bidTable = new OfferTable(com);

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
	void InitGovernment(Government agent)
	{
        List<string> buildables = new ();
		float initStock = 10f;
		float initCash = 1000f;

		// TODO: This may cause uneven maxStock between agents
		var maxStock = Mathf.Max(initStock, 200);

		Assert.IsTrue(config != null);
		//Assert.IsTrue(auctionTracker != null);
        agent.Init(config, auctionTracker, initCash, buildables, initStock, maxStock);
	}
	void InitAgent(EconAgent agent, string type)
	{
        List<string> buildables = new List<string>();
		buildables.Add(type);
		float initStock = config.initStock;
		float initCash = config.initCash;
		if (config.randomInitStock)
		{
			initStock = UnityEngine.Random.Range(config.initStock/2, config.initStock*2);
			initStock = Mathf.Floor(initStock);
		}

		// TODO: This may cause uneven maxStock between agents
		var maxStock = Mathf.Max(initStock, config.maxStock);

        agent.Init(config, auctionTracker, initCash, buildables, initStock, maxStock);
	}
	
	void Update () {
		if (auctionTracker.round > maxRounds || timeToQuit)
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

		if (autoNextRound && Time.time - lastTick > tickInterval)
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
		var totalCash = agents.Sum(x => x.cash);
		Debug.Log("Total cash: " + totalCash);
		Debug.Log("auction house ticking");

		var book = auctionTracker.book;
		foreach (var agent in agents)
		{
			agent.Produce();
			//var numProduced = agent.Produce(book);
			//PayIdleTax(agent, numProduced);

			askTable.Add(agent.CreateAsks());
			bidTable.Add(agent.Consume(book));
		}

		//resolve prices
		foreach (var entry in book)
		{
			if (config.baselineAuction)
				ResolveOffersBaseline(entry.Value);
			else
				ResolveOffers(entry.Value);
			RecordStats(entry.Value);
			Debug.Log(entry.Key + ": have " + entry.Value.trades[^1] 
				+ " at price: " + entry.Value.avgClearingPrice[^1]);
		}

		PrintAuctionStats();
		AgentsStats();

		foreach (var agent in agents)
		{
			agent.ClearRoundStats();
		}
		TickAgent();
		progressivePolicy.Tax(book, agents);
		QuitIf();
	}
	void PrintAuctionStats()
	{
		if (!EnableLog)
			return;
		var header = auctionTracker.round + ", auction, none, none, ";
		var msg = header + "irs, " + gov.cash + ", n/a\n";
		msg += header + "taxed, " + taxed + ", n/a\n";
		msg += auctionTracker.GetLog();
		msg += info.GetLog(header);

		PrintToFile(msg);
	}

	protected void PrintAuctionStats(string c, float buy, float sell)
	{
		if (!EnableLog)
			return;
		string header = auctionTracker.round + ", auction, none, " + c + ", ";
		string msg = header + "bid, " + buy + ", n/a\n";
		msg += header + "ask, " + sell + ", n/a\n";
		msg += header + "avgAskPrice, " + auctionTracker.book[c].avgAskPrice[^1] + ", n/a\n";
		msg += header + "avgBidPrice, " + auctionTracker.book[c].avgBidPrice[^1] + ", n/a\n";

		PrintToFile(msg);
	}
	float moneyExchangedThisRound = 0;
	float goodsExchangedThisRound = 0;
	protected void ResolveOffersBaseline(ResourceController rsc)
	{
		var asks = askTable[rsc.name];
		var bids = bidTable[rsc.name];

		asks.Shuffle();
		bids.Shuffle();

		asks.Sort((x, y) => x.offerPrice.CompareTo(y.offerPrice)); //inc
		bids.Sort((x, y) => y.offerPrice.CompareTo(x.offerPrice)); //dec

		moneyExchangedThisRound = 0;
		goodsExchangedThisRound = 0;

		int askIdx = 0;
		int bidIdx = 0;

		while (askIdx < asks.Count && bidIdx < bids.Count)
		{
			var ask = asks[askIdx];
			var bid = bids[bidIdx];

			var clearingPrice = (ask.offerPrice + bid.offerPrice) / 2f;
			var tradeQuantity = Mathf.Min(bid.remainingQuantity, ask.remainingQuantity);
			Assert.IsTrue(tradeQuantity > 0);
			Assert.IsTrue(clearingPrice > 0);

			// =========== trade ============== 
			var boughtQuantity = Trade(rsc, clearingPrice, tradeQuantity, bid, ask);

			moneyExchangedThisRound += clearingPrice * boughtQuantity;
			goodsExchangedThisRound += boughtQuantity;

			//this is necessary for price belief updates after the big loop
			ask.Accepted(clearingPrice, boughtQuantity);
			bid.Accepted(clearingPrice, boughtQuantity);

			//go to next ask/bid if fullfilled
			if (ask.remainingQuantity == 0)
				askIdx++;
			if (bid.remainingQuantity == 0)
				bidIdx++;
		}
		Assert.IsFalse(goodsExchangedThisRound < 0);
	}
	protected void ResolveOffers(ResourceController rsc)
	{
		var asks = askTable[rsc.name];
		var bids = bidTable[rsc.name];

		asks.Shuffle();
		bids.Shuffle();

		asks.Sort((x, y) => x.offerPrice.CompareTo(y.offerPrice)); //inc
		bids.Sort((x, y) => x.offerPrice.CompareTo(y.offerPrice)); //inc
		//bids.Sort((x, y) => y.offerPrice.CompareTo(x.offerPrice)); //dec

		moneyExchangedThisRound = 0;
		goodsExchangedThisRound = 0;

		int askIdx = 0;
		int bidIdx = 0;

		while (askIdx < asks.Count && bidIdx < bids.Count)
		{
			var ask = asks[askIdx];
			var bid = bids[bidIdx];

			//buyer should not pay more than bid price
			if (ask.offerPrice > bid.offerPrice)
			{
				bidIdx++;
				continue;
			}

			var clearingPrice = (ask.offerPrice + bid.offerPrice)/ 2f;
			var tradeQuantity = Mathf.Min(bid.remainingQuantity, ask.remainingQuantity);
			Assert.IsTrue(tradeQuantity > 0);
			Assert.IsTrue(clearingPrice > 0);

			// =========== trade ============== 
			var boughtQuantity = Trade(rsc, clearingPrice, tradeQuantity, bid, ask);

			moneyExchangedThisRound += clearingPrice * boughtQuantity;
			goodsExchangedThisRound += boughtQuantity;

			//this is necessary for price belief updates after the big loop
			ask.Accepted(clearingPrice, boughtQuantity);
			bid.Accepted(clearingPrice, boughtQuantity);

			//go to next ask/bid if fullfilled
			if (ask.remainingQuantity == 0)
				askIdx++;
			if (bid.remainingQuantity == 0)
				bidIdx++;
		}
		Assert.IsFalse(goodsExchangedThisRound < 0);
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

		Debug.Log("avgprice: " + averagePrice.ToString("c2") + " goods exchanged: " + goodsExchangedThisRound.ToString("n2") + " money exchanged: " + moneyExchangedThisRound.ToString("c2"));
		Assert.IsTrue(averagePrice >= 0f);
		rsc.avgClearingPrice.Add(averagePrice);
		rsc.trades.Add(goodsExchangedThisRound);
		rsc.Update(averagePrice, agentDemandRatio);

		//update price beliefs if still a thing
		asks.Clear();
		bids.Clear();

		PrintAuctionStats(rsc.name, quantityToBuy, quantityToSell);
		Debug.Log(auctionTracker.round + ": " + rsc.name + ": " + goodsExchangedThisRound + " traded at average price of " + averagePrice);
	}

	// TODO decouple transfer of commodity with transfer of money
	// TODO convert cash into another commodity
	protected float Trade(ResourceController rsc, float clearingPrice, float tradeQuantity, Offer bid, Offer ask)
	{
		var boughtQuantity = bid.agent.Buy(rsc.name, tradeQuantity, clearingPrice);
		Assert.IsTrue(boughtQuantity == tradeQuantity);
		ask.agent.Sell(rsc.name, boughtQuantity, clearingPrice);

		Debug.Log(auctionTracker.round + ": " + ask.agent.name 
			+ " ask " + ask.remainingQuantity.ToString("n2") + "x" + ask.offerPrice.ToString("c2")
			+ " | " + bid.agent.name 
			+ " bid: " + bid.remainingQuantity.ToString("n2") + "x" + bid.offerPrice.ToString("c2")
			+ " -- " + rsc.name + " offer quantity: " + tradeQuantity.ToString("n2") 
			+ " bought quantity: " + boughtQuantity.ToString("n2"));
		return boughtQuantity;
	}

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

	protected void OpenFileForWrite() {
		if (!EnableLog)
			return;
		var datepostfix = System.DateTime.Now.ToString(@"yyyy-MM-dd-h_mm_tt");
		if (appendTimeToLog)
		{
			sw = new StreamWriter("log_" + datepostfix + ".csv");
		} else {
			sw = new StreamWriter("log.csv");
		}
		string header_row = "round, agent, produces, inventory_items, type, amount, reason\n";
		PrintToFile(header_row);
	}
	protected void PrintToFile(string msg) {
		if (!EnableLog)
			return;
		sw.Write(msg);
	}

	protected void CloseWriteFile() {
		if (!EnableLog)
			return;
		sw.Close();
	}

	protected void AgentsStats() {
		if (!EnableLog)
			return;
		string header = auctionTracker.round + ", ";
		string msg = "";
		foreach (var agent in agents)
		{
			msg += agent.Stats(header);
		}
		PrintToFile(msg);
	}
	protected void QuitIf()
	{
		if (!exitAfterNoTrade)
		{
			return;
		}
		foreach (var entry in auctionTracker.book)
		{
			var commodity = entry.Key;
			var tradeVolume = entry.Value.trades.LastSum(numRoundsNoTrade);
			if (auctionTracker.round > numRoundsNoTrade && tradeVolume == 0)
			{
				Debug.Log("quitting!! last " + numRoundsNoTrade + " round average " + commodity + " was : " + tradeVolume);
				timeToQuit = true;
				//TODO should be no trades in n rounds
			} else {
				Debug.Log("last " + numRoundsNoTrade + " round trade average for " + commodity + " was : " + tradeVolume);
			}
		}
	}

	protected void TickAgent()
	{
		auctionTracker.ClearStats();
		var book = auctionTracker.book;
		
        foreach (var agent in agents)
        {
			if (agent is Government)
				continue;
			bool changedProfession = false;
			bool bankrupted = false;
			bool starving = false;
			string profession = agent.Profession;

			book[agent.Profession].numAgents++;

			var h = agent.EvaluateHappiness();
			auctionTracker.approval += h;
			auctionTracker.happiness += h;
			book[profession].happiness += h;

			if (agent.cash < 0.0f)
				book[profession].numBankrupted++;

			if (agent.inventory["Food"].Quantity < 0.1f)
				book[profession].numStarving++;

			if (agent.CalcMinProduction() < 1)
				book[profession].numNoInput++;
			
			if (agent.GetProfit() < 0)
				book[profession].numNegProfit++;

			var amount = agent.Tick(gov, ref changedProfession, ref bankrupted, ref starving);

			if (starving)
				book[profession].starving[^1]++;
			if (bankrupted)
				book[profession].bankrupted[^1]++;
			if (changedProfession)
				book[profession].changedProfession[^1]++;
			gov.Pay(amount);
			Debug.Log(agent.name + " total cash line: " + agents.Sum(x => x.cash).ToString("c2") + amount.ToString("c2"));
		}
		foreach (var rsc in book.Values)
		{
			rsc.happiness /= rsc.numAgents;
			rsc.gdp = rsc.trades[^1] * rsc.avgClearingPrice[^1];
			auctionTracker.gdp += rsc.gdp;

			auctionTracker.numBankrupted += rsc.numBankrupted;
			auctionTracker.numStarving += rsc.numStarving;
			auctionTracker.numNoInput += rsc.numNoInput;
			auctionTracker.numNegProfit += rsc.numNegProfit;
			rsc.numChangedProfession = (int)rsc.changedProfession[^1];
			auctionTracker.numChangedProfession += rsc.numChangedProfession;
		}
		auctionTracker.happiness /= agents.Count;
		auctionTracker.approval /= agents.Count;
		auctionTracker.gini = GetGini(GetWealthOfAgents());
	}
	public List<float> GetWealthOfAgents()
	{
		return agents.Where(x => x is not Government).Select(x => x.cash).ToList();
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
