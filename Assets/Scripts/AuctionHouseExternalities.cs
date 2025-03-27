using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using UnityEngine.Assertions;
using AYellowpaper.SerializedCollections;
using UnityEngine.Rendering;
using Sirenix.OdinInspector;
using ChartAndGraph;
using EconSim;
using Sirenix.Serialization;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Profiling;

public partial class AuctionHouse 
{
	[Button]
	public void UpdateAgentTable()
	{
		AgentTable.Clear();
		foreach (var agent in AgentManager.agents)
		{
			if (agent is UserAgent)
				((UserAgent)agent).UserTriggeredPopulateOffersFromInventory();
			AgentTable.Add(new (agent));
		}
	}
	
	[Title("Player Actions")]
	[Button(ButtonSizes.Large), GUIColor(0.4f, 0.8f,1)]
	public void DoNextRound()
	{
		// Profiler.BeginSample("begin round");
		LatchBids();
		Tick();
		district.nextRound();
		streamingGraphs.UpdateGraphs();
		UpdateAgentTable();
		// Profiler.EndSample();
	}

	//latch bid values in AgentTable to agent inventory offers
	public void LatchBids()
	{
		Debug.Log("Latching bids");
		// var entriesAgent = AgentTable.Zip(agents, (e, a) => new { entry = e, agent = a });
		// foreach(var ea in entriesAgent)
		for (int a = 0; a < AgentTable.Count && a < AgentManager.agents.Count; a++)
		{
			var agent = AgentManager.agents[a];
			var entry = AgentTable[a];
			
			string msg = agent.name + " offering ";
			foreach (var c in entry.Bids)
			{
				if (agent.inventory.ContainsKey(c.name) == false)
					Debug.Log("no key found");
				if (c.name == "Labor")
					Debug.Log("reseting labor offers");
				agent.inventory[c.name].offersThisRound = c.quantity;
				msg += c.name + ": " + c.quantity + " ";
			}
			Debug.Log(msg);
		}
	}
	
	bool forestFire = false;
	[HideIf("forestFire")]
	[Button(ButtonSizes.Large), GUIColor(0.4f, 0.8f,1)]
	public void ForestFire()
	{
		//TODO rapid decline (*.6 every round) for 2-5 rounds, then regrow at 1.1 until reaches back to 1 multiplier
		//do this in a new class
		var wood = district.book["Wood"];
		var weight = wood.productionMultiplier;
		weight = .5f;

		wood.ChangeProductionMultiplier(weight);
		wood.productionChance = 0.5f;

		forestFire = true;
	}
	[ShowIf("forestFire")]
	[Button(ButtonSizes.Large), GUIColor(1, 0.4f, 0.4f)]
	public void StopForestFire()
	{
		var wood = district.book["Wood"];
		var weight = wood.productionMultiplier;
		weight = 1f;
		wood.ChangeProductionMultiplier(weight);
		wood.productionChance = 1f;

		forestFire = false;
	}
	
	bool mineCollapse = false;
	[HideIf("mineCollapse")]
	[Button(ButtonSizes.Large), GUIColor(0.4f, 0.8f,1)]
	public void MineCollapse()
	{
		//TODO rapid decline (*.6 every round) for 2-5 rounds, then regrow at 1.1 until reaches back to 1 multiplier
		//do this in a new class
		var ore = district.book["Ore"];
		var weight = ore.productionMultiplier;
		weight = .5f;

		ore.ChangeProductionMultiplier(weight);
		ore.productionChance = 0.5f;

		mineCollapse = true;
	}
	[ShowIf("mineCollapse")]
	[Button(ButtonSizes.Large), GUIColor(1, 0.4f, 0.4f)]
	public void StopMineCollapse()
	{
		var ore = district.book["Ore"];
		var weight = ore.productionMultiplier;
		weight = 1f;
		ore.ChangeProductionMultiplier(weight);
		ore.productionChance = 1f;

		mineCollapse = false;
	}
	
	bool famine = false;
	[HideIf("famine")]
	[Button(ButtonSizes.Large), GUIColor(0.4f, 0.8f,1)]
	public void Famine()
	{
		//TODO rapid decline (*.6 every round) for 2-5 rounds, then regrow at 1.1 until reaches back to 1 multiplier
		//do this in a new class
		var food = district.book["Food"];
		var weight = food.productionMultiplier;
		weight = .5f;

		food.ChangeProductionMultiplier(weight);
		food.productionChance = 0.5f;

		famine = true;
	}
	[ShowIf("famine")]
	[Button(ButtonSizes.Large), GUIColor(1, 0.4f, 0.4f)]
	public void StopFamine()
	{
		var food = district.book["Food"];
		var weight = food.productionMultiplier;
		weight = 1f;
		food.ChangeProductionMultiplier(weight);
		food.productionChance = 1f;

		famine = false;
	}

	private static string[] comOptions = new string[] { "Food","Wood","Ore","Metal","Tool" };
	[PropertyOrder(4)]
	[HorizontalGroup("UpdateTarget")]
	[ValueDropdown("comOptions")]
	public string bidCom = "Food";

	[PropertyOrder(4)]
	[HorizontalGroup("UpdateTarget")]
	public float bidQuant = 0;
	[PropertyOrder(4)]
	[HorizontalGroup("UpdateTarget")]
	[Button]
	public void InsertBid() 
	{
		((Government)gov).UpdateTarget(bidCom, bidQuant);
	}

	[PropertyOrder(6)]
	[FormerlySerializedAs("AlwaysExpandedTable")] [TableList(AlwaysExpanded = true, DrawScrollView = false)]
	public List<AgentEntry> AgentTable = new List<AgentEntry>();

	private AgentManager agentManager;

	public AgentManager AgentManager
	{
		get { return agentManager; }
	}
}
