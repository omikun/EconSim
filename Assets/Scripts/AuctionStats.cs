using System.Collections;
using System.Collections.Generic;
using UnityEngine.Assertions;
using UnityEngine;
using System.Linq;
using System;

using AYellowpaper.SerializedCollections;
using System.Security.Cryptography.X509Certificates;
using System.Net.WebSockets;

public class AuctionBook : Dictionary<string, ResourceController>{}
public class AuctionStats : MonoBehaviour
{
	public int historySize = 10;
	public bool changeToHighestBidPrice = false;
	public bool probabilisticHottestGood = true;
    public static AuctionStats Instance { get; private set; }

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
	public SerializedDictionary<string, SerializedDictionary<string, float>> initialization;
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
        Instance = this;
		book = new AuctionBook();
		round = 0;
		Init();
    }
    public string GetMostProfitableProfession(String exclude_key)
	{
		string prof = "invalid";
		float most = 0;

		foreach (var entry in book)
		{
			var commodity = entry.Key;
			if (exclude_key == commodity) 
			{
				continue;
			}
			var profitHistory = entry.Value.profits;
			//WARNING this history refers to the last # agents' profits, not last # rounds... short history if popular profession...
			var profit = profitHistory.LastAverage(historySize);
			if (profit > most)
			{
				prof = commodity;
				most = profit;
			}
		}
		return prof;
	}
	//get price of good
	int gotHottestGoodRound = 0;
	string mostDemand = "invalid";
	WeightedRandomPicker<string> picker = new WeightedRandomPicker<string>();
	public string GetHottestGood()
	{
		if (round != gotHottestGoodRound)
		{
			mostDemand = _GetHottestGood();
			return mostDemand;
		}
		if (probabilisticHottestGood && !changeToHighestBidPrice)
		{
			mostDemand = _GetHottestGood();
			if (picker.IsEmpty() == false)
			{
				mostDemand = picker.PickRandom();
			}
		}
		var best_ratio = picker.GetWeight(mostDemand);
		Debug.Log(round + " picked demand: " + mostDemand + ": " + best_ratio);
		log_msg += round + ", auction, " + mostDemand + ", none, hottestGood, " + best_ratio + ", n/a\n";
		return mostDemand;
	}
	string _GetHottestGood()
	{
		mostDemand = "invalid";
		float best_ratio = 1.5f;

		if (changeToHighestBidPrice)
		{
			float mostBid = 0;
			foreach (var c in book)
			{
				var bid = c.Value.avgBidPrice.LastAverage(historySize);
				if (bid > mostBid)
				{
					mostBid = bid;
					mostDemand = c.Key;
				}
			}
			return mostDemand;
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
				mostDemand = c.Key;
				picker.AddItem(c.Key, 1);//Mathf.Sqrt(ratio)); //less likely a profession dies out
			}
			Debug.Log(round + " demand: " + c.Key + ": " + Mathf.Sqrt(best_ratio));
			log_msg += round + ", auction, " + c.Key + ", none, demandsupplyratio, " + Mathf.Sqrt(ratio) + ", n/a\n";
		}
		gotHottestGoodRound = round;
		return mostDemand;
	}
	bool Add(string name, float production, float productionMultiplier, Recipe dep)
	{
		if (book.ContainsKey(name)) { return false; }
		Assert.IsNotNull(dep);

		book.Add(name, new ResourceController(name, production, productionMultiplier, dep));
		return true;
	}
    void PrintStat()
    {
		foreach (var item in book)
		{
			Debug.Log(item.Key + ": " + item.Value.price);
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
    // Use this for initialization
    void Init () {
		Debug.Log("Initializing commodities");
		foreach( var item in initialization)
		{
			Recipe dep = new Recipe();
			float prod_rate = 0;
			float prod_multiplier = 0;
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
				dep.Add(field.Key, field.Value);
			}
			if (!Add(item.Key, prod_rate, prod_multiplier, dep))
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
