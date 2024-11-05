using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using System.Linq;
using UnityEngine.XR;
using System;
using Sirenix.Reflection.Editor;
using DG.Tweening;
using UnityEngine.UIElements;

public class EconAgent : MonoBehaviour {
	protected internal SimulationConfig config;
	public static int uid_idx = 0;
	protected int uid;
	public float Cash { get; protected set; }

	public void ResetCash()
	{
		Cash = 0;
	}
	protected float prevCash;
	protected internal float foodExpense = 0;
	protected float initStock = 1;
	protected float maxStock = 1;
	public float Profit { get; protected set; }
	public float TaxableProfit { get; protected set; }
	public Dictionary<string, InventoryItem> inventory = new();
	protected Dictionary<string, float> perItemCost = new();
	float taxesPaidThisRound = 0;
	bool boughtThisRound = false;
	bool soldThisRound = false;
	WaitNumRoundsNotTriggered noSaleIn = new();
	WaitNumRoundsNotTriggered noPurchaseIn = new();
	private Consumer consumer;
	private Producer producer;

	public string Profession {
		get { return outputName; }
	}
	public string outputName { get; protected set; } //can produce commodities
	protected internal HashSet<string> inputs = new();
	//production has dependencies on commodities->populates stock
	//production rate is limited by assembly lines (queues/event lists)
	
	//can use profit to reinvest - produce new commodities
	//switching cost to produce new commodities - zero for now

	//from the paper (base implementation)
	// Use this for initialization
	protected internal AuctionBook book {get; set;}
	protected internal AuctionStats auctionStats;
	protected internal Dictionary<string, float> producedThisRound = new();
	public float numProducedThisRound = 0;
	protected string log = "";

	public virtual String Stats(String header)
	{
		header += uid.ToString() + ", " + outputName + ", "; //profession
		foreach (var stock in inventory)
		{
			log += stock.Value.Stats(header);
		}
		log += header + "cash, stock, " + Cash + ", n/a\n";
		log += header + "profit, stock, " + Profit + ", n/a\n";
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
	void Start () {
	}
	protected void AddToInventory(string name, float num, float max, float price, float production)
	{
		if (inventory.ContainsKey(name))
			return;

        inventory.Add(name, new InventoryItem(auctionStats, name, num, max, price, production));

		perItemCost[name] = book[name].marketPrice * num;
	}
	public float PayWealthTax(float amountExempt, float taxRate)
	{
		var taxableAmount = Cash - amountExempt;
		if (taxableAmount <= 0)
			return 0f;
		var tax = taxableAmount * taxRate;
		Cash -= tax;
		taxesPaidThisRound = tax;
		return tax;
	}
	public void Pay(float amount)
	{
		Cash -= amount;
	}
	public virtual void Init(SimulationConfig cfg, AuctionStats at, string b, float _initStock, float maxstock) {
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
		Cash = config.initCash;
		prevCash = Cash;
		inputs.Clear();
		//foreach (var buildable in outputName)
		var buildable = outputName;
		{

			if (!book.ContainsKey(buildable))
				Debug.Log("commodity not recognized: " + buildable);

            if (book[buildable].recipe == null)
				Debug.Log(buildable + ": null dep!");

			foreach (var dep in book[buildable].recipe)
			{
				var commodity = dep.Key;
				inputs.Add(commodity);
                //Debug.Log("::" + commodity);
				AddToInventory(commodity, initStock, maxStock, book[commodity].marketPrice, book[commodity].production);
			}
			AddToInventory(buildable, 0, maxStock, book[buildable].marketPrice, book[buildable].production);
			Debug.Log("New " + gameObject.name + " has " + inventory[buildable].Quantity + " " + buildable);
		}
    }

	void Configure()
	{
		if (config.sanityCheck)
			consumer = new SanityCheckConsumer(this);
		else
			consumer = new Consumer(this);
		producer = new Producer(this);
	}
	public void Respawn(bool bankrupted, string buildable, Government gov = null)
	{
		Assert.IsTrue(this is not Government);
		outputName = buildable;
		gov.Welfare(this);
		prevCash = Cash;
		foodExpense = 0;
		inputs.Clear();
		//foreach (var outputName in outputName)
		{
			if (!book.ContainsKey(outputName))
				Debug.Log("commodity not recognized: " + outputName);

			var output = book[outputName];
            if (output.recipe == null)
				Debug.Log(outputName + ": null dep!");

			PrintInventory("before reinit");
			
			Assert.IsTrue(gov != null);

			foreach (var dep in output.recipe)
			{
				var com = book[dep.Key];
				inputs.Add(com.name);
				AddToInventory(com.name, 0, maxStock, com.marketPrice, com.production);
			}
			AddToInventory(outputName, 0, maxStock, output.marketPrice, output.production);
			
			PrintInventory("post reinit");
		}
    }

	public void PrintInventory(string label)
	{
			string msg = "";
			foreach (var entry in inventory)
			{
				msg += entry.Value.Quantity + " " + entry.Key + ", ";
			}
			Debug.Log(auctionStats.round + ": " + name + " " + label + " reinit2: " + msg );
	}

	public float TaxProfit(float taxRate)
	{
		if (TaxableProfit <= 0)
			return 0;
		var taxAmt = TaxableProfit * taxRate;
		Cash -= taxAmt;
		return taxAmt;
	}

	private float losses = 0;
	//want to control when profit gets calculated in round
	public void CalculateProfit()
	{
		var prevLosses = losses;
		var delta = Cash - prevCash;
		prevCash = Cash;
		Profit = delta;
		var cumDelta = delta + prevLosses;
		losses = Mathf.Min(0, cumDelta);
		TaxableProfit = Mathf.Max(0, cumDelta);
	}
	const float bankruptcyThreshold = 0;
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
				foodConsumed = config.foodConsumptionCurve.Evaluate(food.Quantity/food.maxQuantity);
			if (food.Quantity >= foodConsumed)
			{
				var foodAmount = food.Decrease(foodConsumed);
				foodExpense += food.cost * foodConsumed;
				Debug.Log(auctionStats.round + ": " + name + "consumed " + foodConsumed.ToString("n2") + " food expense " + foodExpense);
			}
			starving = food.Quantity <= 0.1f;
			foodExpense = Mathf.Max(0, foodExpense - Mathf.Max(0, Profit));
		}
		
		foreach (var entry in inventory)
		{
			entry.Value.Tick();
        }

		//ClearRoundStats();

		bool changeProfessionAfterNRounds =  (config.earlyProfessionChange && (noSaleIn.Count() >= config.changeProfessionAfterNDays));
		bankrupted = IsBankrupt();
		changedProfession = (config.declareBankruptcy && bankrupted) || (config.starvation && starving);
        if (config.changeProfession && (changedProfession || changeProfessionAfterNRounds))
		{
			Debug.Log(auctionStats.round + " " + name + " producing " + outputName[0] + " is bankrupt: " + Cash.ToString("c2") 
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
	public float Food()
	{
		return inventory["Food"].Quantity;
	}
	public virtual float EvaluateHappiness()
	{
		var numFoodEq = FoodEquivalent.GetNumFoodEquivalent(book, this, config.numFoodHappy);
		return Mathf.Log10(numFoodEq);
	}

	void ChangeProfession(Government gov, bool bankrupted = true)
	{
		string bestGood = auctionStats.GetHottestGood();
		float profit = 0f;
		string mostDemand = auctionStats.GetMostProfitableProfession(ref profit, Profession);

		if (bestGood != "invalid")
        {
            mostDemand = bestGood;
		}
		Debug.Log(auctionStats.round + " " + name + " changing from " + Profession + " to " + mostDemand + " --  bestGood: " + bestGood + " bestProfession: " + mostDemand);

		string b = "";
		if (mostDemand != "invalid")
			b = mostDemand;
		else
			b = Profession;

		if (config.clearInventory)
		{
			inventory.Clear();
		}
		Respawn(bankrupted, b, gov);
	}

	/*********** Trading ************/
	public void modify_cash(float quant)
	{
		Cash += quant;
	}
	public void ClearRoundStats()
	{
		noSaleIn.Tick();
		noPurchaseIn.Tick();

		foreach( var item in inventory)
		{
			item.Value.ClearRoundStats();
		}
		taxesPaidThisRound = 0;
	}
	public float Buy(string commodity, float quantity, float price)
	{
		if (this is Government)
		{
			Debug.Log(auctionStats.round + " gov buying " + quantity.ToString("n0") + " " + commodity);
		}
		var boughtQuantity = inventory[commodity].Buy(quantity, price);
		Debug.Log(name + " has " + Cash.ToString("c2") 
			+ " want to buy " + quantity.ToString("n2") + " " + commodity 
			+ " for " + price.ToString("c2") + " bought " + boughtQuantity.ToString("n2"));
		if (outputName.Contains(commodity))
		{
			Debug.Log(name + " outputs: " + outputName);
		}
		Assert.IsFalse(outputName.Contains(commodity)); //agents shouldn't buy what they produce
		Cash -= price * boughtQuantity;
		boughtThisRound = true;
		return boughtQuantity;
	}
	public virtual void Sell(string commodity, float quantity, float price)
	{
		if (this is Government)
		{
			Debug.Log(auctionStats.round + " gov selling " + quantity.ToString("n0") + " " + commodity);
		}
		Assert.IsTrue(inventory[commodity].Quantity >= 0);
		inventory[commodity].Sell(quantity, price);
		Assert.IsTrue(inventory[commodity].Quantity >= 0);
		soldThisRound = true;
		Cash += price * quantity;
	}
	public void UpdateSellerPriceBelief(in Offer trade, in ResourceController rsc) 
	{
		inventory[rsc.name].UpdateSellerPriceBelief(name, in trade, in rsc);
	}
	public void UpdateBuyerPriceBelief(in Offer trade, in ResourceController rsc) 
	{
		inventory[rsc.name].UpdateBuyerPriceBelief(name, in trade, in rsc);
	}

    /*********** Produce and consume; enter asks and bids to auction house *****/
	public virtual Offers Consume(AuctionBook book)
	{
		return consumer.Consume(book);
	}
	public virtual float Produce() {
		numProducedThisRound = producer.Produce();
		return numProducedThisRound;
	}
	public float CalcMinProduction()
	{
		float minTotalProduced = float.MaxValue; 
		//foreach (var outputName in outputName)
		{
			Assert.IsTrue(book.ContainsKey(outputName));
			var com = book[outputName];
			var stock = inventory[outputName];
			var numProduced = CalculateNumProduced(com, stock);
			minTotalProduced = Mathf.Min(minTotalProduced, numProduced);

		}
		return minTotalProduced;
	}
	//build as many as one can 
	//TODO what if don't want to produce as much as one can? what if costs are high rn?
	public float CalculateNumProduceable(ResourceController rsc, InventoryItem item)
	{
		float numProduced = item.Deficit(); //can't produce more than max stock
		//find max that can be made w/ available stock
		foreach (var com in rsc.recipe.Keys)
		{
			numProduced = Mathf.Min(numProduced, inventory[com].NumProduceable(rsc));
			Debug.Log(auctionStats.round + " " + name 
				+ "can produce " + numProduced 
				+ " w/" + inventory[com].Quantity + "/" + rsc.recipe[com] + " " + com);
		}
		return numProduced;
	}
	protected internal float CalculateNumProduced(ResourceController rsc, InventoryItem item)
	{
		var numProduced = CalculateNumProduceable(rsc, item);
		//can only build fixed rate at a time
		numProduced = Mathf.Clamp(numProduced, 0, item.GetProductionRate());
		numProduced = Mathf.Floor(numProduced);

		Debug.Log(auctionStats.round + " " + name 
			+ " producing " + rsc.name 
			+ " currently in stock " + item.Quantity 
			+ " production rate: " + item.GetProductionRate() 
			+ " produced: " + numProduced
			+ " room: " + item.Deficit());
		Assert.IsTrue(numProduced >= 0);
		return numProduced;
	}
	protected internal void ConsumeInput(ResourceController rsc, float numProduced, ref string msg)
	{
		foreach (var dep in rsc.recipe)
		{
			var stock = inventory[dep.Key].Quantity;
			var numUsed = dep.Value * numProduced;
			Debug.Log(auctionStats.round + " " + name + " has " + stock + " " + dep.Key + " used " + numUsed);
			Assert.IsTrue(stock >= numUsed);
			inventory[dep.Key].Decrease(numUsed);
			msg += dep.Key + ": " + inventory[dep.Key].meanCost.ToString("c2");
		}
	}
	
    protected internal float GetCostOf(ResourceController rsc)
	{
		float cost = 0;
		foreach (var dep in rsc.recipe)
		{
			var depCommodity = dep.Key;
			var numDep = dep.Value;
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

	public virtual Offers CreateAsks()
	{
		return producer.CreateAsks();
	}
	void Update () {
	}
}
