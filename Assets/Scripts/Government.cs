using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using System.Linq;
using UnityEngine.XR;
using System;
using Sirenix.Reflection.Editor;
using DG.Tweening;

public class Government : EconAgent {
	public float FoodTarget = 000;
	public override void Init(SimulationConfig cfg, AuctionStats at, string b, float _initStock, float maxstock, float cash=-1f) {
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
		
		inventory["Food"].Increase(FoodTarget);
		inventory["Food"].TargetQuantity = FoodTarget;
    }

    public override void Decide() {
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

        //replenish depended commodities
        foreach (var (com,item) in inventory)
		{
			if ((int)item.TargetQuantity <= (int)item.Quantity)
				continue;
			var offerQuantity = item.TargetQuantity - item.Quantity;
			var offerPrice = book[com].marketPrice * 1.05f;
			bids.Add(com, new Offer(com, offerPrice, offerQuantity, this));
		}
        return bids;
	}
	public override Offers CreateAsks()
	{
		var asks = new Offers();

        foreach (var (com,item) in inventory)
		{
			// if ((int)item.TargetQuantity >= (int)item.Quantity)
				// continue;

			// var offerQuantity = item.TargetQuantity - item.Quantity;
			var offerQuantity = item.Quantity;
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
            if (inventory.ContainsKey(good) == false)
                AddToInventory(good, item.Quantity, maxStock, item.rsc);
            else
                inventory[good].Increase(item.Quantity);
            item.Decrease(item.Quantity);
        }
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
		if (agentFood < refillThreshold)
		{
			var refill = refillThreshold - agentFood;
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