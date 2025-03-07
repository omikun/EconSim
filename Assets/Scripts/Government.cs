using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using System.Linq;
using UnityEngine.XR;
using System;
using Sirenix.Reflection.Editor;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine.Serialization;

public class Government : EconAgent {
	public float FoodTarget = 020;
	[ShowInInspector]
	public int EmploymentTarget = 4;  //should vary with tax base

	[FormerlySerializedAs("marketPriceCoefficient")] [ShowInInspector]
	public float payCoefficient = .9f; //low pay to force employees to find a real job?

	public override void Init(SimulationConfig cfg, AuctionStats at, string b, float _initStock, float maxstock, float cash=-1f)
	{
		Employees = new();
		config = cfg;
		uid = uid_idx++;
		initStock = _initStock;
		maxStock = maxstock;
		Alive = true;

		book = at.book;
		auctionStats = at;
		//list of commodities self can produce
		//get initial stockpiles
		outputName = b;
		Cash = config.initGovCash;
		prevCash = Cash;
		inputs.Clear();
        foreach(var good in book)
        {
            var name = good.Key;
            inputs.Add(name);
            AddToInventory(name, 0, maxstock, good.Value);
        }
		
        var com = "Labor";
        AddToInventory(com, 1, 1, book[com]);
		inventory["Food"].Increase(FoodTarget);
		inventory["Food"].TargetQuantity = FoodTarget;
    }

    public override void Decide() {
	    //pay employees
	    foreach (var (employee,wage) in Employees)
	    {
		    var pay = book["Food"].marketPrice * payCoefficient;
		    employee.Earn(pay);
		    Cash -= pay;
	    }
    }
	public override float EvaluateHappiness()
    {
        return 0;
    }
	public override String Stats(String header)
	{
		header += uid.ToString() + ", " + "government" + ", "; //profession
		foreach (var stock in inventory)
		{
			log += stock.Value.Stats(header);
		}
		log += header + "cash, stock, " + Cash + ", n/a\n";
		log += header + "profit, stock, " + Profit + ", n/a\n";
		foreach (var (good, quantity) in producedThisRound)
		{
			log += header + good + ", produced, " + quantity + ", n/a\n";
		}
		var ret = log;
		log = "";
		return ret; 
	}
    public void InsertBid(string com, float quant, float price)
    {
		inventory[com].ChangePendingOffer(quant, price);
    }
	public void UpdateTarget(string com, float quant)
	{
		inventory[com].TargetQuantity += quant;
	}

	public override Offers CreateBids(AuctionBook book) 
	{
        var bids = new Offers();
        if (EmploymentTarget > Employees.Count)
        {
	        var offerQuantity = EmploymentTarget - Employees.Count;
	        var com = "Labor";
	        bids.Add(com, new Offer(com, book["Food"].marketPrice * payCoefficient, offerQuantity, this));
        }

        //replenish depended commodities
        foreach (var (com,item) in inventory)
		{
			if ((int)item.TargetQuantity <= (int)item.Quantity)
				continue;
			var offerQuantity = item.TargetQuantity - item.Quantity;
			var offerPrice = book[com].marketPrice * .90f;
			bids.Add(com, new Offer(com, offerPrice, offerQuantity, this));
		}
        return bids;
	}
	public override Offers CreateAsks()
	{
		var asks = new Offers();

        foreach (var (com,item) in inventory)
		{
			if ((int)item.TargetQuantity >= (int)item.Quantity)
				continue;

			var offerQuantity = item.Quantity - item.TargetQuantity;
			var offerPrice = book[com].marketPrice * 1.1f;
			// if (item.OfferQuantity > 0)
			if (offerQuantity > 0)
			{
				asks.Add(com, new Offer(com, offerPrice, offerQuantity, this));
				Debug.Log(auctionStats.round + " gov asked " + offerQuantity.ToString("n2") + " " + item.name);
			}
		}
		return asks;
	}

    public void LiquidateInventory(Inventory agentInventory)
    {
        foreach (var (good, item) in agentInventory)
        {
	        if (good == "Labor")
		        continue;
	        
            if (inventory.ContainsKey(good) == false)
                AddToInventory(good, item.Quantity, maxStock, item.rsc);
            else
                inventory[good].Increase(item.Quantity);
            item.Decrease(item.Quantity);
        }
    }

    public void Tick(int numAgents)
    {
	    var temp = Math.Log((double)numAgents);
	    EmploymentTarget = (int)Math.Floor(temp) * 2;
	    Debug.Log(auctionStats.round +  " gov employment target: " + EmploymentTarget.ToString("n2"));
    }
    public override float Tick(Government gov, ref bool changedProfession, ref bool bankrupted, ref bool starving)
    {
	    Debug.Log(name + " outputs: " + outputName);
        return 0f;
    }

    public void AbsorbBankruptcy(EconAgent agent)
    {
		//if (agent.IsBankrupt())
		if (config.declareBankruptcy)
		{
			var transferCash = config.initCash - agent.Cash;
			
			auctionStats.Transfer(this, agent, "Cash", transferCash);
			agent.AddToCash(transferCash);
			Cash -= transferCash;
		}
    }
	public void Welfare(EconAgent agent)
	{
		if (!config.GovWelfare)
		{
			return;
		}
		//quant should be no more than what gov's inventory holds
		//only enough to refill agent's inv back to 2
		//should not be negative in case agent has more than 2 already
		var agentFood = agent.Food();
		var govFood = Food();
		var refillThreshold = 5f;
		if (agent.DaysStarving >= refillThreshold)
		{
			var refill = 1f;
			var quant = Mathf.Min(refill, govFood);
			Debug.Log(auctionStats.round + " Fed agent" + agent.name + " " + quant.ToString("n1") + " food, prev had " + agentFood.ToString("n1"));
			if (quant == 0)
				return;
			auctionStats.Transfer(this, agent, "Food", quant);
			inventory["Food"].Decrease(quant);
			agent.inventory["Food"].Increase(quant);
		}
	}
}