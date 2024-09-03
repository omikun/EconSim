using System.Collections;
using System.Collections.Generic;
using UnityEngine.Assertions;
using UnityEngine;
using System.Linq;
using System;

public class Dependency : Dictionary<string, float> {}
public class Commodity
{
	const float defaultPrice = 1;
	public ESList buyers, sellers, bids, asks, avgBidPrice, avgAskPrice, avgClearingPrice, trades, profits;
	
	float avgPrice = 1;
	public float GetAvgPrice(int history) //average of last history size
	{
        var skip = Mathf.Max(0, avgClearingPrice.Count - history);
        avgPrice = avgClearingPrice.Skip(skip).Average();
        return avgPrice;
	}
	public Commodity(string n, float p, float pc, Dependency d)
	{
		name = n;
		production = p;
		productionMultiplier = pc;
		price = defaultPrice;
		dep = d;
		demand = 1;

		buyers   = new ESList();
		buyers.Add(1);
		sellers   = new ESList();
		sellers.Add(1);
		bids   = new ESList();
		bids.Add(1);
		asks   = new  ESList();
		asks.Add(1);
		trades = new  ESList();
		trades.Add(1);
		avgAskPrice = new  ESList();
		avgAskPrice.Add(1);
		avgBidPrice = new  ESList();
		avgBidPrice.Add(1);
		avgClearingPrice = new  ESList();
		avgClearingPrice.Add(1);
		profits = new ESList();
		profits.Add(1);
	}
	public void Update(float p, float dem)
	{
		price = p;
		demand = dem;
	}
	public void ChangeProductionMultiplier(float pm)
	{
		productionMultiplier = pm;
	}
	public string name { get; private set; }
	public float price { get; private set; } //market price
	public float demand { get; private set; }
	public float production { get; private set; }
	public float productionMultiplier { get; private set; } //forest fire or rich mineral vein
	public float resourceAmount { get; private set; } // for fish or finite ore
	public Dependency dep { get; private set; }
}