using System.Collections;
using System.Collections.Generic;
using UnityEngine.Assertions;
using UnityEngine;
using System.Linq;
using System;

using AYellowpaper.SerializedCollections;
using System.Security.Cryptography.X509Certificates;
using System.Net.WebSockets;
using Sirenix.Serialization;

public class AuctionBook : Dictionary<string, ResourceController>{}
public class AuctionStats : MonoBehaviour
{
	public int historySize = 10;
	public bool changeToHighestBidPrice = false;
	public bool probabilisticHottestGood = true;
    //public static AuctionStats Instance { get; private set; }

	public AuctionBook book { get; private set; }
	public int round { get; private set; }
	public float happiness;
	public float approval;
	public int numBankrupted;
	public int numStarving;
	public int numChangedProfession;
	public int numNegProfit;
	public int numNoInput;
	public float gdp;// { get { return book.Values.Sum(x => x.gdp);}}
	public float gini;

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
	}
	[SerializedDictionary("ID", "Recipe")]
	//[OdinSerialize]
	public SerializedDictionary<string, SerializedDictionary<string, float>> initialization = new();
	string log_msg = "";
	public string GetLog()
	{
		var ret = log_msg;
		log_msg = "";
		return ret;
	}
	
	public void nextRound()
	{
		round += 1;
	}
    private void Awake()
    {
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
			var asks = c.Value.asks.LastAverage(historySize);
			var bids = c.Value.bids.LastAverage(historySize);
            asks = Mathf.Max(.5f, asks);
			var ratio = bids / asks;

			if (best_ratio < ratio)
			{
				best_ratio = ratio;
				hottestGood = c.Key;
				picker.AddItem(c.Key, 1);//Mathf.Sqrt(ratio)); //less likely a profession dies out
			}
			Debug.Log(round + " demand: " + c.Key + ": " + Mathf.Sqrt(best_ratio));
			log_msg += round + ", auction, " + c.Key + ", none, demandsupplyratio, " + Mathf.Sqrt(ratio) + ", n/a\n";
		}
		return hottestGood;
	}
	bool AddToBook(string name, float production, float productionMultiplier, float setPrice, Recipe dep)
	{
		if (book.ContainsKey(name)) { return false; }
		Assert.IsNotNull(dep);

		book.Add(name, new ResourceController(name, production, productionMultiplier, setPrice, dep));
		return true;
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
	void InitInitialization() 
	{
		initialization["Food"] = new() { 
			{ "Wood", .1f}, 
			{ "Prod_multiplier", 1f}, 
			{ "Tool", .1f}, 
			{ "Prod_rate", 5f}, 
			};
		initialization["Wood"] = new() { 
			{ "Food", 1f}, 
			{ "Prod_multiplier", 1f}, 
			{ "Prod_rate", 1f}, 
			};
		initialization["Ore"] = new() { 
			{ "Food", 1f}, 
			{ "Prod_multiplier", 1f}, 
			{ "Prod_rate", 5f}, 
			};
		initialization["Metal"] = new() { 
			{ "Food", 1f}, 
			{ "Ore", 2f}, 
			{ "Prod_multiplier", 1f}, 
			{ "Prod_rate", 3f}, 
			};
		initialization["Tool"] = new() { 
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
		//InitInitialization();
		foreach( var item in initialization)
		{
			Recipe dep = new Recipe();
			float prod_rate = 0;
			float prod_multiplier = 0;
			float set_price = 0;
			foreach (var field in item.Value)
			{
				if (field.Key == "Prod_rate")
				{
					prod_rate = field.Value;
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
				dep.Add(field.Key, field.Value);
			}
			if (!AddToBook(item.Key, prod_rate, prod_multiplier, set_price, dep))
			{
				Debug.Log("Failed to add commodity; duplicate?");
			}
		}
		//PrintStat();
		return;
    }
	
	// Update is called once per frame
	void Update () {
		
	}
}
