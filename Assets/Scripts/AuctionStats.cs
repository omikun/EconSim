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

public class AuctionBook : Dictionary<string, ResourceController>{}
public class AuctionStats : MonoBehaviour
{
	public int historySize = 1;
	public bool changeToHighestBidPrice = false;
	public bool probabilisticHottestGood = true;
    //public static AuctionStats Instance { get; private set; }

	public AuctionBook book { get; private set; }
	public Bank bank;
	public BankRegulations regulations { get; private set; }
	public SimulationConfig config;
	public Dictionary<string, List<GenericTransaction>> transactions = new();
	public int round { get; private set; }
	[DisableInEditorMode]
	public float inflation;
	[DisableInEditorMode]
	public float happiness;
	[DisableInEditorMode]
	public float approval;
	[DisableInEditorMode]
	public int numBankrupted;
	[DisableInEditorMode]
	public int numStarving;
	[DisableInEditorMode]
	public int numChangedProfession;
	[DisableInEditorMode]
	public int numNegProfit;
	[DisableInEditorMode]
	public int numNoInput;
	[DisableInEditorMode]
	public float gdp;// { get { return book.Values.Sum(x => x.gdp);}}
	[DisableInEditorMode]
	public float gini;

	void Awake()
	{
		regulations = new BankRegulations(.1f, 30, .01f);
		bank = new Bank(100, "cash", regulations);
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
			entry.profits.Add(0);
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
    public string GetMostProfitableProfession(ref float mostProfit, String exclude_key="invalid")
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
			var profitHistory = entry.Value.profits;
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
	string mostProfitable = "invalid";
	WeightedRandomPicker<string> picker = new ();
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
		} else {
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
				picker.AddItem(c.Key, 1);//Mathf.Sqrt(ratio)); //less likely a profession dies out
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
				Debug.Log("Dependencies: " );
				foreach (var depItem in item.Value.recipe)
				{
                    Debug.Log(" -> " + depItem.Key + ": " + depItem.Value);
				}
			}
		}
	}
	void InitCommodities() 
	{
		config.initialization["Food"] = new() { 
			{ "Wood", .1f}, 
			{ "Prod_multiplier", 1f}, 
			{ "Tool", .1f}, 
			{ "Prod_rate", 5f}, 
			};
		config.initialization["Wood"] = new() { 
			{ "Food", 1f}, 
			{ "Prod_multiplier", 1f}, 
			{ "Prod_rate", 1f}, 
			};
		config.initialization["Ore"] = new() { 
			{ "Food", 1f}, 
			{ "Prod_multiplier", 1f}, 
			{ "Prod_rate", 5f}, 
			};
		config.initialization["Metal"] = new() { 
			{ "Food", 1f}, 
			{ "Ore", 2f}, 
			{ "Prod_multiplier", 1f}, 
			{ "Prod_rate", 3f}, 
			};
		config.initialization["Tool"] = new() { 
			{ "Food", 1f}, 
			{ "Metal", 2f}, 
			{ "Prod_multiplier", 1f}, 
			{ "Prod_rate", 1f}, 
			};
	}
    // Use this for initialization
    public void Init () {
		Debug.Log("Initializing commodities");
		book = new AuctionBook();
		round = 0;
		//InitCommodities();
		foreach( var item in config.initialization)
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
			foreach (var field in item.Value)
			{
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
			book.Add(item.Key, new ResourceController(item.Key, prod_rate, base_rate, batch_rate, prod_multiplier, set_price, dep));
		}
	    foreach (var com in book.Keys)
	    {
		    transactions.Add(com, new());
	    }
	    transactions.Add("Cash", new());
		//PrintStat();
		return;
    }
	
	// Update is called once per frame
	void Update () {
		
	}
}
