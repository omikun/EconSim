using System.Collections;
using System.Collections.Generic;
using UnityEngine.Assertions;
using UnityEngine;
using System.Linq;
using System;
using AYellowpaper.SerializedCollections;
using Sirenix.OdinInspector;
using Sirenix.Utilities;

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

	public string name { get; private set; }
	public string profession { get; private set; }
	private float _marketPrice;
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
	public float startingCash = -1;
	public string Type { get; private set; }
	public ResourceController(string n, string prof, float p, float bp, float br, float pm, float sp, float bc, Recipe r, float sc = -1)
	{
		name = n;
		profession = prof;
		productionPerBatch = p;
		baseProduction = bp;
		batchRate = br;
		productionMultiplier = pm;
		breakdown_chance = bc;
		setPrice = sp; //initial price at start of simulation
		marketPrice = sp;
		recipe = r;
		startingCash = sc;

		Init();
	}
	public void Init()
	{
		demand = 1;
		buyers.AddnUpdate(1);
		sellers.AddnUpdate(1);
		bids.AddnUpdate(1);
		asks.AddnUpdate(1);
		trades.AddnUpdate(1);
		inventory.AddnUpdate(1);
		cash.AddnUpdate(1);
		avgAskPrice.AddnUpdate(setPrice);
		avgBidPrice.AddnUpdate(setPrice);
		minAskPrice.AddnUpdate(setPrice);
		minBidPrice.AddnUpdate(setPrice);
		maxAskPrice.AddnUpdate(setPrice);
		maxBidPrice.AddnUpdate(setPrice);
		avgClearingPrice.AddnUpdate(setPrice);
		minClearingPrice.AddnUpdate(setPrice);
		maxClearingPrice.AddnUpdate(setPrice);
		incomes.AddnUpdate(1);
		bankrupted.AddnUpdate(1);
		starving.AddnUpdate(1);
		changedProfession.AddnUpdate(1);
	}
	public ResourceController(string n, SerializedDictionary<string, float> init)
	{
		name = n;
		recipe = new ();
		foreach (var field in init)
		{

			if (field.Key == "Prod_rate") productionPerBatch = field.Value;
			else if (field.Key == "Base_rate") baseProduction = field.Value;
			else if (field.Key == "Batch_rate") batchRate = field.Value;
			else if (field.Key == "Prod_multiplier") productionMultiplier = field.Value;
			else if (field.Key == "Breakdown_chance") breakdown_chance = field.Value;
			else if (field.Key == "Starting_cash") startingCash = field.Value;
			else if (field.Key == "Set_price") setPrice = field.Value;
			else if (field.Key.StartsWith("Producer_"))
			{
				var parts = field.Key.Split('_');
				if (parts.Length == 2)
					profession = parts[1].SplitPascalCase();
				continue;
			} else {
				recipe.Add(field.Key, field.Value);
			}
		}

		Assert.IsNotNull(recipe);
		marketPrice = setPrice;
		Init();
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