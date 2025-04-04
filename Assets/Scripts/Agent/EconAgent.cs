using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using System.Linq;
using UnityEngine.XR;
using System;
using System.Text;
using DG.Tweening;
using EconSim;
using UnityEditor;
using UnityEngine.Serialization;
using UnityEngine.UIElements;

public class Inventory : Dictionary<string, InventoryItem>
{
}

public partial class EconAgent : MonoBehaviour
{
	protected internal SimulationConfig config;
	public static int uid_idx = 0;
	public int uid { get; protected set; }
	public float Cash { get; protected set; }

	public void ResetCash()
	{
		Cash = 0;
	}
	public string CashString { get{ return Cash.ToString("c2"); } }

	public bool Alive { get; protected set; }
	public int DaysStarving { get; protected set; }

	WaitNumRoundsNotTriggered noSaleIn = new();
	WaitNumRoundsNotTriggered noPurchaseIn = new();
	protected internal FoodEquivalent foodEquivalent;

	//private AskQuantityStrategy askQuantityStrategy;
	//private bidQuantityStrategy bidQuantityStrategy;
	//private BidPriceStrategy bidPriceStrategy;
	
	//////////////// NOTE FOR FIRMS ONLY //////////////////////

	public string outputName { get; protected set; } //can produce commodities

	//production has dependencies on commodities->populates stock
	//production rate is limited by assembly lines (queues/event lists)

	//can use profit to reinvest - produce new commodities
	//switching cost to produce new commodities - zero for now

	//from the paper (base implementation)
	// Use this for initialization
	protected internal AuctionBook book { get; set; }
	protected internal AuctionStats auctionStats;
	protected string log = "";

	public virtual String Stats(String header)
	{
		header += uid.ToString() + ", " + outputName + ", "; //profession
		foreach (var stock in inventory)
		{
			log += stock.Value.Stats(header);
		}

		log += header + "cash, stock, " + Cash + ", n/a\n";
		log += header + "profit, stock, " + Income + ", n/a\n";
		log += header + "taxes, idle, " + taxesPaidThisRound + ", n/a\n";
		foreach (var (good, quantity) in producedThisRound)
		{
			log += header + good + ", produced, " + quantity + ", n/a\n";
		}

		producedThisRound.Clear();
		var ret = log;
		log = "";
		return ret;
	}

	public virtual void Init(SimulationConfig cfg, AuctionStats at, string b, float _initStock, float maxstock, float cash=-1f)
	{
		Alive = true;
		config = cfg;
		uid = uid_idx++;
		initStock = _initStock;
		maxStock = maxstock;

		Configure();

		book = at.book;
		auctionStats = at;
		//list of commodities self can produce
		//get initial stockpiles
		outputName = b;
		
		Cash = (cash == -1f) ? config.initCash : cash;
		prevCash = Cash;
		inputs.Clear();
		//foreach (var buildable in outputName)
		{
			if (outputName != "Food")
			{
				var commodity = "Food";
				AddToInventory(commodity, initStock, maxStock, book[commodity]);
			}

			if (outputName == "Unemployed")
			{
				var labor = "Labor";
				AddToInventory(labor, 1, 1, book[labor]);
				return;
			}
			else
			{
				var com = "Labor";
				AddToInventory(com, 0, 1, book[com]);
			}

			if (!book.ContainsKey(outputName))
				Debug.Log("commodity not recognized: " + outputName);

			if (book[outputName].startingCash != -1)
				Cash = book[outputName].startingCash;

			if (book[outputName].recipe == null)
				Debug.Log(outputName + ": null dep!");

			foreach (var dep in book[outputName].recipe)
			{
				var commodity = dep.Key;
				inputs.Add(commodity);
				//Debug.Log("::" + commodity);
				AddToInventory(commodity, initStock, maxStock, book[commodity]);
			}


			AddToInventory(outputName, 0, maxStock, book[outputName]);
			Debug.Log(auctionStats.round + " New agent " + gameObject.name + " uid: " + uid + " cash: " + Cash.ToString("c2") + " has " + inventory[outputName].Quantity + " " + outputName);
		}
	}

	void Configure()
	{
		foodEquivalent = new(this);
		switch (config.consumerType)
		{
			case ConsumerType.Default:
				consumer = new SanityCheckConsumer(this);
				break;
			case ConsumerType.SanityCheck:
				consumer = new SanityCheckConsumer(this);
				break;
			case ConsumerType.QoLBased:
				consumer = new QoLConsumer(this);
				break;
			default:
				Assert.IsTrue(false, "Unknown consumer type: " + config.consumerType);
				break;
		}

		switch (config.productionRate)
		{
			case AgentProduction.FixedRate:
				productionStrategy = new FixedProduction(this);
				break;
			default:
				Assert.IsTrue(false, "unsupported production strategy");
				break;
		}

		switch (config.sellPrice)
		{
			case AgentSellPrice.FixedPrice:
				askPriceStrategy = new FixedAskPriceStrategy(this);
				break;
			case AgentSellPrice.AtCost:
				askPriceStrategy = new AtCostAskStrategy(this);
				break;
			case AgentSellPrice.MarketAverage:
				askPriceStrategy = new MarketAskStrategy(this);
				break;
			case AgentSellPrice.FixedProfit:
				askPriceStrategy = new FixedProfitAskStrategy(this);
				break;
			case AgentSellPrice.DemandBased:
				askPriceStrategy = new DynamicAskPriceStrategy(this);
				break;
			default:
				Assert.IsTrue(false, "unsupported agent sell price strategy");
				break;
		}
	}

	public void Respawn(bool bankrupted, string buildable, Government gov = null)
	{
		Assert.IsTrue(this is not Government);
		outputName = buildable;
		gov.AbsorbBankruptcy(this);
		gov.Welfare(this);
		prevCash = Cash;
		foodExpense = 0;
		inputs.Clear();
		DaysStarving = 0;
		Quit();
		//foreach (var outputName in outputName)
		{
			if (!book.ContainsKey(outputName))
			{
				Debug.Log("commodity not recognized: " + outputName);
				return;
			}

			var output = book[outputName];
			if (output.recipe == null)
				Debug.Log(outputName + ": null dep!");

			PrintInventory("before reinit");

			Assert.IsTrue(gov != null);

			foreach (var dep in output.recipe)
			{
				var com = book[dep.Key];
				inputs.Add(com.name);
				AddToInventory(com.name, 0, maxStock, com);
			}

			AddToInventory(outputName, 0, maxStock, output);

			PrintInventory("post reinit");
		}
	}

	//want to control when profit gets calculated in round

	const float bankruptcyThreshold = 30;

	public bool IsBankrupt()
	{
		return Cash < bankruptcyThreshold;
	}

	public virtual float Tick(Government gov, ref bool changedProfession, ref bool bankrupted, ref bool starving)
	{
		Assert.IsTrue(this is not Government);
		Debug.Log("agents ticking!");
		float taxConsumed = 0;

		if (config.foodConsumption && inventory.ContainsKey("Food"))
		{
			var food = inventory["Food"];

			var foodConsumed = config.foodConsumptionRate;
			if (config.useFoodConsumptionCurve)
				foodConsumed = config.foodConsumptionCurve.Evaluate(food.Quantity / config.numFoodHappy);
			else
			{
				if (food.Quantity > 10)
					foodConsumed = 3;
				else if (food.Quantity > 5)
					foodConsumed = 2;
				else 
					foodConsumed = 1;
			}
			//if (food.Quantity >= foodConsumed)
			{
				var foodAmount = food.Decrease(foodConsumed);
				foodExpense += food.unitCost * foodConsumed;
				Debug.Log(auctionStats.round + ": " + name + " consumed " + foodConsumed.ToString("n2") +
				          " food expense " + foodExpense.ToString("c2"));
			}
			gov.Welfare(this);

			starving = food.Quantity <= 0.1f;
			if (starving)
				DaysStarving++;
			else
				DaysStarving = 0;
			
			//if starving for 3 rounds, die off
			if (DaysStarving == 3)
			{
				Alive = false;
			}
			foodExpense = Mathf.Max(0, foodExpense - Mathf.Max(0, Income.Last()));
		}

		foreach (var entry in inventory)
		{
			entry.Value.Tick();
		}

		//ClearRoundStats();

		bool changeProfessionAfterNRounds =
			(config.earlyProfessionChange && (noSaleIn.Count() >= config.changeProfessionAfterNDays));
		bankrupted = IsBankrupt();
		changedProfession = (config.declareBankruptcy && bankrupted) || (config.starvation && starving);
		if (config.changeProfession && (changedProfession || changeProfessionAfterNRounds))
		{
			Debug.Log(auctionStats.round + " " + name + " producing " + outputName + " is bankrupt: " +
			          Cash.ToString("c2")
			          + " or starving where food=" + inventory["Food"].Quantity
			          + " or " + config.changeProfessionAfterNDays + " days no sell");
			//gov absorbs debt or cash on change profession
			//probably should be more complex than this
			//like agent takes out a loan, if after a certain point can declare bankruptcy and get out of debt
			//this only makes sense if there is high demand and supply of inputs exist
			//if high demand but no supply, change role to supplier??
			//gov can hand out food if starving
			//change jobs when not profitable, 
			//these 3 things can be separate events instead of rolled into one
			ChangeProfession(gov, bankrupted);
			noSaleIn.Reset();
			noPurchaseIn.Reset();
		}

		// cost updated on EconAgent::Produce()
		// foreach (var buildable in outputs)
		// {
		// 	inventory[buildable].cost = GetCostOf(buildable);
		// }
		return taxConsumed;
	}

	public AnimationCurve foodToHappy;
	public AnimationCurve cashToHappy;

	public float FoodInv()
	{
		return inventory["Food"].Quantity;
	}

	public virtual float EvaluateHappiness()
	{
		// var numFoodEq = foodEquivalent.GetHappyLevel(book, config.numFoodHappy);
		var numFood = FoodInv();
		var scaledFood = (numFood + 10) / 10;
		return Mathf.Log10(scaledFood) / Mathf.Log10(scaledFood + 1);
	}


	/*********** Trading ************/

	public void ClearRoundStats()
	{
		noSaleIn.Tick();
		noPurchaseIn.Tick();

		foreach (var item in inventory)
		{
			item.Value.ClearRoundStats();
		}

		taxesPaidThisRound = 0;
	}

	public virtual void Decide()
	{
		ConsumeGoods();
		Produce();
	}

	/*********** Produce and consume; enter asks and bids to auction house *****/

	protected internal float GetCostOf(ResourceController rsc)
	{
		float cost = 0;
		foreach (var (depCommodity, numDep) in rsc.recipe)
		{
			var depCost = inventory[depCommodity].meanCost;
			cost += numDep * depCost;
		}
		return cost;
	}

	// public virtual GetSellPrice()
	// {
	// 	var baseSellPrice = book[commodityName].price;
	// 	baseSellPrice *= UnityEngine.Random.Range(.97f, 1.03f);
	// 	sellPrice = Mathf.Max(sellPrice, baseSellPrice);
	// }
}
