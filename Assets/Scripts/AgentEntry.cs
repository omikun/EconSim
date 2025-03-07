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
using Michsky.MUIP;
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
	[TableColumnWidth(40), VerticalGroup("Agent"), HideLabel, LabelWidth(42), ReadOnly]
	public string Agent;
	[VerticalGroup("Agent"), HideLabel, LabelWidth(42), ReadOnly]
	public string Employer;

	[VerticalGroup("Agent"), HideLabel, LabelWidth(42), ReadOnly]
	public int NumEmployees;

	[VerticalGroup("Stats"), LabelWidth(50), ReadOnly] [GUIColor("GetCashColor")]
	public float Cash;

	[VerticalGroup("Stats"), LabelWidth(50), ReadOnly] [GUIColor("GetDepositColor")]
	public float Deposit;
	
	[VerticalGroup("Stats"), LabelWidth(50), ReadOnly] [GUIColor("GetDebtColor")]
	public float Debt;
	
	[VerticalGroup("Stats"), LabelWidth(50), ReadOnly] [GUIColor("GetFoodColor")]
	public int DaysStarving;

	[VerticalGroup("Inventory"), ReadOnly, HideLabel]
	[InlineProperty]
	public List<CommodityEntry> Commodities = new();
	[VerticalGroup("Bids"), HideLabel]
	[InlineProperty]
	public List<CommodityEntry> Bids = new();

	private float food = 0;
	private float foodPrice = 0;
	private Color GetFoodColor()
	{
		if (DaysStarving > 2)
			return Color.magenta;
		else if (DaysStarving > 0)
			return Color.red;
		else if (food <= 2)
			return Color.yellow;
		else
			return Color.green;
	}

	private Color GetDebtColor()
	{
		if (Debt < 1f)
			return Color.green;
		else
			return Color.red;
	}
	private Color GetDepositColor()
	{
		if (Deposit > foodPrice)
			return Color.green;
		else if (Deposit + Cash > foodPrice)
			return Color.yellow;
		else
			return Color.red;
	}
	private Color GetCashColor()
	{
		if (Cash > foodPrice)
			return Color.green;
		else if (Deposit + Cash > foodPrice)
			return Color.yellow;
		else
			return Color.red;
	}
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
			// item.offersThisRound = 0; this clears all offers before they can be entered to auction
		}

		if (agent is Bank)
		{
			Agent = agent.name;
			Cash = agent.auctionStats.bank.Wealth;
			Deposit = agent.auctionStats.bank.TotalDeposits;
			Debt = agent.auctionStats.bank.liability;
		}
		else
		{
			Agent = agent.name + "-" + agent.outputName;
			Cash = agent.Cash;
			Deposit = agent.auctionStats.bank.CheckAccountBalance(agent);
			Debt = agent.auctionStats.bank.QueryLoans(agent);
		}
		DaysStarving = agent.DaysStarving;
		Employer = (agent.Employer == null) ? "Self employed" : agent.Employer.name + "-" + agent.Employer.outputName;
		NumEmployees = (agent.Employees == null) ? 0 : agent.Employees.Count;
		
		//all inventory
		foreach (var (com, numDepends) in agent.inventory)
		{
			var item = agent.inventory[com];
			Commodities.Add(new (com, item.Quantity, item.GetPrice()));
			if (com == "Food")
			{
				food = item.Quantity;
				foodPrice = item.GetPrice();
			}
		}

		//if bank/gov/unemployed/employed
		if (agent.book.ContainsKey(agent.outputName) == false)
		{
			foreach (var (com, numDepends) in agent.inventory)
			{
				var item = agent.inventory[com];
				Bids.Add(new (com, item.offersThisRound, item.GetPrice()));
			}

			return;
		}
		//TODO add asks to entry
		
		var recipe = agent.book[agent.outputName].recipe;
		//bids (inputs + food if output is not food)
		foreach (var (com, numDepends) in recipe)
		{
			var item = agent.inventory[com];
			Bids.Add(new (com, item.offersThisRound, item.GetPrice()));
		}
		if (agent.outputName != "Food")
		{
			var food = agent.inventory["Food"];
			Bids.Add(new ("Food", food.offersThisRound, food.GetPrice()));
		}
	}
}
