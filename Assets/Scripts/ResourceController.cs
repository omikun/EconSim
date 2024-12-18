using System.Collections;
using System.Collections.Generic;
using UnityEngine.Assertions;
using UnityEngine;
using System.Linq;
using System;

public class Recipe : Dictionary<string, float> {}
public class ResourceController
{
	const float defaultPrice = 1;
	public ESList buyers = new();
	public ESList sellers = new();
 	public ESList bids = new();
 	public ESList asks = new();
 	public ESList avgBidPrice = new();
 	public ESList avgAskPrice = new();
 	public ESList avgClearingPrice = new();
 	public ESList trades = new();
 	public ESList profits = new();
 	public ESList changedProfession = new();
 	public ESList bankrupted = new();
 	public ESList starving = new();

	public int numAgents; //num agents in this profession for current round before profession changes
	public float happiness;
	public float approval;
	public int numBankrupted;
	public int numStarving;
	public int numChangedProfession;
	public int numNegProfit;
	public int numNoInput;
	public float gdp;
	public float gini;
	float quantity = 10; //total quantity of resource agents can extract
	
	float avgPrice = 1;
    public float setPrice = 1; //predetermined price from initializer for sanity check
	public float GetAvgPrice(int history) //average of last history size
	{
        var skip = Mathf.Max(0, avgClearingPrice.Count - history);
        avgPrice = avgClearingPrice.Skip(skip).Average();
        return avgPrice;
	}
	public ResourceController(string n, float p, float br, float pm, float sp,Recipe r)
	{
		name = n;
		productionPerBatch = p;
		batchRate = br;
		productionMultiplier = pm;
		setPrice = sp; //why can't setPrice and price be the same thing?
		marketPrice = sp;
		recipe = r;
		demand = 1;

		buyers.Add(1);
		sellers.Add(1);
		bids.Add(1);
		asks.Add(1);
		trades.Add(1);
		avgAskPrice.Add(1);
		avgBidPrice.Add(1);
		avgClearingPrice.Add(1);
		profits.Add(1);
		bankrupted.Add(1);
		starving.Add(1);
		changedProfession.Add(1);
	}
	public void Update(float p, float dem)
	{
		marketPrice = p;
		demand = dem;
	}
	public void ChangeProductionMultiplier(float pm)
	{
		productionMultiplier = pm;
	}
	public string name { get; private set; }
	public float marketPrice { get; private set; } //market price
	public string marketPriceString
	{
		get { return marketPrice.ToString("c2");  }
	}
	public float demand { get; private set; }
	public float productionPerBatch { get; private set; }
	public float batchRate { get; private set; }
	public float productionMultiplier { get; private set; } //forest fire or rich mineral vein
	public float resourceAmount { get; private set; } // for fish or finite ore
	public Recipe recipe { get; private set; }
}