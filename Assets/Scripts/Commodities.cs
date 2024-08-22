using System.Collections;
using System.Collections.Generic;
using UnityEngine.Assertions;
using UnityEngine;
using System.Linq;
using System;

using AYellowpaper.SerializedCollections;
using System.Security.Cryptography.X509Certificates;
//using Dependency = System.Collections.Generic.Dictionary<string, float>;

public class AuctionStats : MonoBehaviour
{
    public static AuctionStats Instance { get; private set; }

	public Dictionary<string, Commodity> book { get; private set; }
	public int round { get; private set; }
	public bool starvation = false;
	public bool foodConsumption = false;
	public bool simpleTradeAmountDet = false;
	public bool onlyBuyWhatsAffordable = false;
	[Tooltip("Use highest bid good vs most demand to supply good")]
	public bool changeToHIghestBidGood = false;
	public int historySize = 10;

	[SerializedDictionary("ID", "Dependency")]
	public SerializedDictionary<string, SerializedDictionary<string, float>> initialization;
	
	public void nextRound()
	{
		round += 1;
	}
    private void Awake()
    {
        Instance = this;
		book = new Dictionary<string, Commodity>(); //names, market price
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
	float GetRelativeDemand(Commodity c, int history=10)
	{
        var averagePrice = c.avgClearingPrice.LastAverage(history);
        var minPrice = c.avgClearingPrice.Min();
		var price = c.avgClearingPrice[c.avgClearingPrice.Count-1];
		var relativeDemand = (price - minPrice) / (averagePrice - minPrice);
		//Debug.Log("avgPrice: " + averagePrice.ToString("c2") + " min: " + minPrice.ToString("c2") + " curr: " + price.ToString("c2"));
		return relativeDemand;
	}
	public string GetHottestGood()
	{
		string mostDemand = "invalid";
		float max = 1.1f;
		string mostRDDemand = "invalid";
		float maxRD = max;

		if (changeToHIghestBidGood)
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
		foreach (var c in book)
		{
			var asks = c.Value.asks.LastAverage(historySize);
			var bids = c.Value.bids.LastAverage(historySize);
            asks = Mathf.Max(.5f, asks);
			var ratio = bids / asks;
			var relDemand = GetRelativeDemand(c.Value, historySize);

			if ( maxRD < relDemand)
			{
				mostRDDemand = c.Key;
				maxRD = relDemand;
			}
			if (max < ratio)
			{
				max = ratio;
				mostDemand = c.Key;
			}
			//Debug.Log(c.Key + " Ratio: " + ratio.ToString("n2") + " relative demand: " + relDemand);
		}
		Debug.Log("Most in demand: " + mostDemand + ": " + max);
		//Debug.Log("Most in rel demand: " + mostRDDemand + ": " + maxRD);
		return mostDemand;
	}
	bool Add(string name, float production, Dependency dep)
	{
		if (book.ContainsKey(name)) { return false; }
		Assert.IsNotNull(dep);

		book.Add(name, new Commodity(name, production, dep));
		return true;
	}
    void PrintStat()
    {
		foreach (var item in book)
		{
			Debug.Log(item.Key + ": " + item.Value.price);
			if (item.Value.dep != null)
			{
				Debug.Log("Dependencies: " );
				foreach (var depItem in item.Value.dep)
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
			Dependency dep = new Dependency();
			float prod_rate = 0;
			foreach (var field in item.Value)
			{
				if (field.Key == "Prod_rate")
				{
					prod_rate = field.Value;
					continue;
				}
				dep.Add(field.Key, field.Value);
			}
			if (!Add(item.Key, prod_rate, dep))
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
