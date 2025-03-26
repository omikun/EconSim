using System.Collections;
using System.Collections.Generic;
using UnityEngine.Assertions;
using UnityEngine;
using System.Linq;
using System;
using Sirenix.OdinInspector;

public class Recipe : Dictionary<string, float>
{
	public Recipe() { }

	public Recipe(Recipe recipe) : base(recipe) { }
}
public class ResourceController
{
	const float defaultPrice = 1;
	public ESHistory buyers = new();
	public ESHistory sellers = new();
 	public ESHistory bids = new();
 	public ESHistory asks = new();
 	public ESHistory avgBidPrice = new();
 	public ESHistory minBidPrice = new();
 	public ESHistory maxBidPrice = new();
 	public ESHistory avgAskPrice = new();
 	public ESHistory minAskPrice = new();
 	public ESHistory maxAskPrice = new();
 	public ESHistory avgClearingPrice = new();
 	public ESHistory minClearingPrice = new();
 	public ESHistory maxClearingPrice = new();
 	public ESHistory trades = new();
 	public ESHistory inventory = new();
 	public ESHistory cash = new();
 	public ESHistory incomes = new();
 	public ESHistory changedProfession = new();
 	public ESHistory bankrupted = new();
 	public ESHistory starving = new();

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
    public float breakdown_chance = 1;
	public ResourceController(string n, float p, float bp, float br, float pm, float sp, float bc, Recipe r)
	{
		name = n;
		productionPerBatch = p;
		baseProduction = bp;
		batchRate = br;
		productionMultiplier = pm;
		breakdown_chance = bc;
		setPrice = sp; //initial price at start of simulation
		marketPrice = sp;
		recipe = r;
		demand = 1;

		buyers.Add(1);
		sellers.Add(1);
		bids.Add(1);
		asks.Add(1);
		trades.Add(1);
		inventory.Add(1);
		cash.Add(1);
		avgAskPrice.Add(setPrice);
		avgBidPrice.Add(setPrice);
		minAskPrice.Add(setPrice);
		minBidPrice.Add(setPrice);
		maxAskPrice.Add(setPrice);
		maxBidPrice.Add(setPrice);
		avgClearingPrice.Add(setPrice);
		minClearingPrice.Add(setPrice);
		maxClearingPrice.Add(setPrice);
		incomes.Add(1);
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
	private float _marketPrice;
	public float marketPrice
	{
		get { return _marketPrice;}
		private set { _marketPrice = value; } //Mathf.Max(0.01f, value); }
	} 
	public string marketPriceString
	{
		get { return marketPrice.ToString("c2");  }
	}
	public float demand { get; private set; }
	public float productionPerBatch { get; private set; } //base line production is productionPerBatch * batchRate
	public float baseProduction { get; private set; } //min production if no inputs
	public float batchRate { get; private set; } //num batches per round
	public float productionMultiplier { get; private set; } //forest fire or rich mineral vein
	public float productionChance = 1;
	public float resourceAmount { get; private set; } // for fish or finite ore
	[DictionaryDrawerSettings(IsReadOnly = false, DisplayMode = DictionaryDisplayOptions.OneLine)]
	public Recipe recipe { get; private set; }
}