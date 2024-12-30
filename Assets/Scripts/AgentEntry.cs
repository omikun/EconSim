using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
using AYellowpaper.SerializedCollections;
using UnityEngine.Rendering;
using Sirenix.OdinInspector;
using ChartAndGraph;
using EconSim;
using Sirenix.Serialization;
using Sirenix.OdinInspector.Editor.ValueResolvers;
using UnityEngine.Serialization;

[Serializable]
[HideLabel]
public class CommodityEntry
{
	[HorizontalGroup("groups")]
	[TableColumnWidth(50), VerticalGroup("groups/com"), HideLabel, LabelWidth(20), ReadOnly]
	public string name;
	[VerticalGroup("groups/Quant"), HideLabel, LabelWidth(20)]
	public float quantity;
	[VerticalGroup("groups/Price"), HideLabel, LabelWidth(20)]
	public float price;

	public CommodityEntry(string n, float q, float p)
	{
		name = n;
		quantity = q;
		price = p;
	}
}
[Serializable]
public class AgentEntry
{
	[TableColumnWidth(50), VerticalGroup("Agent"), HideLabel, LabelWidth(42), ReadOnly]
	public string Agent;

	[VerticalGroup("Cash"), HideLabel, LabelWidth(30), ReadOnly]
	public float Cash;
	
	[VerticalGroup("Inventory"), ReadOnly, HideLabel]
	[InlineProperty]
	public List<CommodityEntry> Commodities = new();
	[VerticalGroup("Bids"), HideLabel]
	[InlineProperty]
	public List<CommodityEntry> Bids = new();

	[OnInspectorInit]
	private void CreateEntry()
	{
	}

	public AgentEntry()
	{
	}
	public AgentEntry(EconAgent agent)
	{
		foreach (var item in agent.inventory.Values)
		{
			item.offersThisRound = 0;
			item.UpdateNiceness = true;
			item.CanOfferAdditionalThisRound = true;
		}
		
		Agent = agent.name + "-" + agent.outputName;
		Cash = agent.Cash;
		var recipe = agent.book[agent.outputName].recipe;
		if (agent is Government)
			return;
		foreach (var (com, numDepends) in recipe)
		{
			var item = agent.inventory[com];
			Commodities.Add(new (com, item.Quantity, item.GetPrice()));
		}

		var food = agent.inventory["Food"];
		Commodities.Add(new (food.name, food.Quantity, food.GetPrice()));

		foreach (var (com, numDepends) in recipe)
		{
			var item = agent.inventory[com];
			Bids.Add(new (com, 0f, item.GetPrice()));
		}
		if (agent.outputName != "Food")
		{
			Bids.Add(new ("Food", 0f, agent.inventory["Food"].GetPrice()));
		}
	}
}
