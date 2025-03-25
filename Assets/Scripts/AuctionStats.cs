using System.Collections;
using System.Collections.Generic;
using UnityEngine.Assertions;
using UnityEngine;
using System.Linq;
using System;
using AYellowpaper.SerializedCollections;
using System.Security.Cryptography.X509Certificates;
using System.Net.WebSockets;
using Sirenix.OdinInspector;
using Sirenix.Serialization;

public class AuctionBook : Dictionary<string, ResourceController> { }

public class AuctionStats : MonoBehaviour
{
	public int historySize = 1;
	public bool changeToHighestBidPrice = false;
	public bool probabilisticHottestGood = true;

	public AuctionHouse auctionHouse { get; private set; }
	
	public AuctionBook book { get; private set; }
	public Bank bank;
	[Required]
	public SimulationConfig config;
	public Dictionary<string, List<GenericTransaction>> transactions = new();
	public int round { get; private set; }
	[DisableInEditorMode] public float inflation;
	[DisableInEditorMode] public float happiness;
	[DisableInEditorMode] public float approval;
	[DisableInEditorMode] public int numBankrupted;
	[DisableInEditorMode] public int numStarving;
	[DisableInEditorMode] public int numChangedProfession;
	[DisableInEditorMode] public int numNegProfit;
	[DisableInEditorMode] public int numNoInput;
	[DisableInEditorMode] public float gdp; // { get { return book.Values.Sum(x => x.gdp);}}
	[DisableInEditorMode] public float gini;

	void Awake()
	{
		// regulations = new BankRegulations(.1f, 30, .05f, 5, 500f, 3);
		// bank = new Bank(100, "Cash", regulations);
	}

	public void ClearStats()
	{
		happiness = 0;
		approval = 0;
		numBankrupted = 0;
		numStarving = 0;
		numChangedProfession = 0;
		numNegProfit = 0;
		numNoInput = 0;
		gdp = 0;
		gini = 0;

		foreach (var entry in book.Values)
		{
			entry.incomes.Add(0);
			entry.starving.Add(0);
			entry.bankrupted.Add(0);
			entry.changedProfession.Add(0);

			entry.numAgents = 0;
			entry.happiness = 0;
			entry.approval = 0;
			entry.numBankrupted = 0;
			entry.numStarving = 0;
			entry.numChangedProfession = 0;
			entry.numNegProfit = 0;
			entry.numNoInput = 0;
			entry.gdp = 0;
			entry.gini = 0;
		}

		foreach (var rscTransaction in transactions.Values)
		{
			rscTransaction.Clear();
		}
	}

	string log_msg = "";

	public string GetLog()
	{
		foreach (var (com, rscTransaction) in transactions)
		{
			string header = round + ", ";
			foreach (var trans in rscTransaction)
			{
				log_msg += trans.ToString(header);
			}
		}

		var ret = log_msg;
		log_msg = "";
		return ret;
	}

	public void nextRound()
	{
		round += 1;
	}

	public void Transfer(EconAgent from, EconAgent to, string kind, float amount)
	{
		if (amount == 0)
			return;
		transactions[kind].Add(new GenericTransaction(from, to, kind, amount)); //seller transfers
	}

	public string GetMostProfitableProfession(ref float mostProfit, String exclude_key = "invalid")
	{
		string mostProfitableProf = "invalid";
		mostProfit = 0;

		foreach (var entry in book)
		{
			var profession = entry.Key;
			if (exclude_key == profession)
			{
				continue;
			}

			var profitHistory = entry.Value.incomes;
			//WARNING this history refers to the last # agents' profits, not last # rounds... short history if popular profession...
			var profit = profitHistory.LastAverage(historySize);
			if (profit > mostProfit)
			{
				mostProfitableProf = profession;
				mostProfit = profit;
			}

			log_msg += round + ", auction, " + profession + ", none, profitability, " + profit + ", n/a\n";
		}

		log_msg += round + ", auction, " + mostProfitableProf + ", none, mostProfit, " + mostProfit + ", n/a\n";
		return mostProfitableProf;
	}
	
	//get price of good
	int gotHottestGoodRound = 0;
	
	string hottestGood = "invalid";
	
	// string mostProfitable = "invalid";
	WeightedRandomPicker<string> picker = new();

	public string GetHottestGood()
	{
		float best_ratio = 1.5f;
		string ret = "invalid";
		if (round != gotHottestGoodRound)
		{
			hottestGood = _GetHottestGood(ref best_ratio);
		}

		if (probabilisticHottestGood && !changeToHighestBidPrice)
		{
			if (picker.IsEmpty() == false)
			{
				ret = picker.PickRandom();
			}
		}
		else
		{
			ret = hottestGood;
		}

		Debug.Log(round + " picked demand: " + ret + ": " + best_ratio);
		log_msg += round + ", auction, " + ret + ", none, hottestGood, " + best_ratio + ", n/a\n";
		return ret;
	}
	
	string _GetHottestGood(ref float best_ratio)
	{
		hottestGood = "invalid";
		gotHottestGoodRound = round;

		if (changeToHighestBidPrice)
		{
			float mostBid = 0;
			foreach (var c in book)
			{
				var bid = c.Value.avgBidPrice.LastAverage(historySize);
				if (bid > mostBid)
				{
					mostBid = bid;
					hottestGood = c.Key;
				}
			}

			return hottestGood;
		}

		picker.Clear();
		foreach (var c in book)
		{
			var asks = c.Value.asks.ExpAverage();
			asks = Mathf.Max(asks, 0.1f);
			var bids = c.Value.bids.ExpAverage();
			var ratio = bids / asks;

			if (best_ratio < ratio)
			{
				best_ratio = ratio;
				hottestGood = c.Key;
				picker.AddItem(c.Key, 1); //Mathf.Sqrt(ratio)); //less likely a profession dies out
			}

			Debug.Log(round + " num bids: " + bids.ToString("n2")
			          + " num asks: " + asks.ToString("n2") + " demand: " + c.Key + ": " + (ratio));
			log_msg += round + ", auction, " + c.Key + ", none, demandsupplyratio, " + (ratio) + ", n/a\n";
		}

		return hottestGood;
	}
	
	void PrintStat()
	{
		foreach (var item in book)
		{
			Debug.Log(item.Key + ": " + item.Value.marketPrice);
			if (item.Value.recipe != null)
			{
				Debug.Log("Dependencies: ");
				foreach (var depItem in item.Value.recipe)
				{
					Debug.Log(" -> " + depItem.Key + ": " + depItem.Value);
				}
			}
		}
	}
	
	void InitCommodities()
	{
		config.initialization["Food"] = new()
		{
			{ "Wood", .1f },
			{ "Prod_multiplier", 1f },
			{ "Tool", .1f },
			{ "Prod_rate", 5f },
		};
		config.initialization["Wood"] = new()
		{
			{ "Food", 1f },
			{ "Prod_multiplier", 1f },
			{ "Prod_rate", 1f },
		};
		config.initialization["Ore"] = new()
		{
			{ "Food", 1f },
			{ "Prod_multiplier", 1f },
			{ "Prod_rate", 5f },
		};
		config.initialization["Metal"] = new()
		{
			{ "Food", 1f },
			{ "Ore", 2f },
			{ "Prod_multiplier", 1f },
			{ "Prod_rate", 3f },
		};
		config.initialization["Tool"] = new()
		{
			{ "Food", 1f },
			{ "Metal", 2f },
			{ "Prod_multiplier", 1f },
			{ "Prod_rate", 1f },
		};
	}
	
	public void Init()
	{
		Debug.Log("Initializing commodities");
		auctionHouse = GetComponent<AuctionHouse>();
		book = new AuctionBook();
		round = 0;
		//InitCommodities();
		foreach (var item in config.initialization)
		{
			if (book.ContainsKey(item.Key))
			{
				Debug.Log("Failed to add commodity; duplicate?");
				continue;
			}

			Recipe dep = new Recipe();
			float batch_rate = 0;
			float prod_rate = 0;
			float base_rate = 0;
			float prod_multiplier = 0;
			float set_price = 0;
			float breakdown_chance = 1;
			foreach (var field in item.Value)
			{
				if (field.Key == "Breakdown_chance")
				{
					breakdown_chance = field.Value;
					continue;
				}

				if (field.Key == "Prod_rate")
				{
					prod_rate = field.Value;
					continue;
				}

				if (field.Key == "Base_rate")
				{
					base_rate = field.Value;
					continue;
				}

				if (field.Key == "Prod_multiplier")
				{
					prod_multiplier = field.Value;
					continue;
				}

				if (field.Key == "Set_price")
				{
					set_price = field.Value;
					continue;
				}

				if (field.Key == "Batch_rate")
				{
					batch_rate = field.Value;
					continue;
				}

				dep.Add(field.Key, field.Value);
			}

			Assert.IsNotNull(dep);
			book.Add(item.Key,
				new ResourceController(item.Key, prod_rate, base_rate, batch_rate, prod_multiplier, set_price,
					breakdown_chance, dep));
		}

		foreach (var com in book.Keys)
		{
			transactions.Add(com, new());
		}

		transactions.Add("Cash", new());
	}
	
	// Update is called once per frame
	void Update()
	{
	}
	public void RecordStats(ResourceController rsc, TradeStats stats)
	{
		var asks = auctionHouse.askTable[rsc.name];
		var bids = auctionHouse.bidTable[rsc.name];
		
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

		Debug.Log(round + " " + rsc.name + " avgprice: " + averagePrice.ToString("c2") + " goods exchanged: " + stats.goodsExchangedThisRound.ToString("n2") + " money exchanged: " + stats.moneyExchangedThisRound.ToString("c2"));
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

		var totalInventory = auctionHouse.AgentManager.agents.Sum(agent => (agent.inventory.Keys.Contains(rsc.name)) ? agent.inventory[rsc.name].Quantity : 0f);
		rsc.inventory.Add(totalInventory);

		var totalCash = auctionHouse.AgentManager.agents.Sum(agent => (agent.outputName == rsc.name) ? agent.Cash : 0f)
		                + auctionHouse.bank.Deposits.Sum(entry => (entry.Key.outputName == rsc.name) ? entry.Value : 0f);
		string msg = "";
		foreach (var agent in auctionHouse.AgentManager.agents)
		{
			if (agent.outputName == rsc.name)
				msg += agent.name + " makes " + agent.outputName + " has cash " + agent.CashString + "\n";
		}

		Debug.Log(round + ": " + rsc.name + " cash list:\n " + msg);
		rsc.cash.Add(totalCash);
		
		foreach (var ask in asks)
			ask.agent.UpdateSellerPriceBelief(ask, rsc);
		foreach (var bid in bids)
			bid.agent.UpdateBuyerPriceBelief(bid, rsc);
		
		//update price beliefs if still a thing
		asks.Clear();
		bids.Clear();

		PrintAuctionStats(rsc.name, quantityToBuy, quantityToSell);
		Debug.Log(round + ": " + rsc.name + ": " + stats.goodsExchangedThisRound + " traded at average price of " + averagePrice.ToString("c2"));
	}
	public void PrintAuctionStats()
	{
		if (!config.EnableLog)
			return;
		var header = round + ", auction, none, none, ";
		var msg = header + "irs, " + auctionHouse.gov.Cash + ", n/a\n";
		msg += header + "taxed, " + auctionHouse.fiscalPolicy.taxed + ", n/a\n";
		msg += GetLog();
		msg += auctionHouse.info.GetLog(header);

		auctionHouse.logger.PrintToFile(msg);
	}
	
	protected void PrintAuctionStats(string c, float buy, float sell)
	{
		if (!config.EnableLog)
			return;
		string header = round + ", auction, none, " + c + ", ";
		string msg = header + "bid, " + buy + ", n/a\n";
		msg += header + "ask, " + sell + ", n/a\n";
		msg += header + "avgAskPrice, " + book[c].avgAskPrice[^1] + ", n/a\n";
		msg += header + "avgBidPrice, " + book[c].avgBidPrice[^1] + ", n/a\n";

		auctionHouse.logger.PrintToFile(msg);
	}
}
