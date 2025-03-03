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

public partial class AuctionHouse 
{
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
}
