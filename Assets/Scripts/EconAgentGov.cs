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
	public float FoodTarget = 50;
	public override void Init(SimulationConfig cfg, AuctionStats at, List<string> b, float _initStock, float maxstock) {
		config = cfg;
		uid = uid_idx++;
		initStock = _initStock;
		maxStock = maxstock;

		book = at.book;
		auctionStats = at;
		//list of commodities self can produce
		//get initial stockpiles
		outputNames = b;
		Cash = config.initGovCash;
		prevCash = Cash;
		inputs.Clear();
        foreach(var good in book)
        {
            var name = good.Key;
            inputs.Add(name);
            AddToInventory(name, 0, maxstock, 1, 0);
        }
		
		inventory["Food"].Increase(FoodTarget);
		inventory["Food"].TargetQuantity = FoodTarget;
    }

    public override float Produce() {
        return 0;
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
	public override Offers Consume(AuctionBook book) 
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
			if ((int)item.TargetQuantity >= (int)item.Quantity)
				continue;

			var offerQuantity = item.TargetQuantity - item.Quantity;
			var offerPrice = book[com].marketPrice * .95f;
			if (item.OfferQuantity < 0)
			{
				asks.Add(com, new Offer(com, offerPrice, offerQuantity, this));
				Debug.Log(auctionStats.round + " gov asked " + offerQuantity.ToString("n2") + " " + item.name);
			}
		}
		return asks;
	}

    public override float Tick(Government gov, ref bool changedProfession, ref bool bankrupted, ref bool starving)
    {
        return 0f;
    }

	public void Welfare(EconAgent agent)
	{
		if (agent.IsBankrupt())
		{
			Cash += agent.Cash;
			Cash -= config.initCash;
			agent.ResetCash();
			agent.modify_cash(config.initCash);
		}
		//quant should be no more than what gov's inventory holds
		//only enough to refill agent's inv back to 2
		//should not be negative in case agent has more than 2 already
		const float refill = 1f;
		var quant = Mathf.Min(refill, inventory["Food"].Quantity);
		var agentFood = agent.inventory["Food"].Quantity;
		quant = Mathf.Min(quant, refill - agentFood);
		quant = Mathf.Max(quant, 0f);
		Debug.Log(auctionStats.round + " Fed agent" + agent.name + " " + quant.ToString("n1") + " food, prev had " + agentFood.ToString("n1"));
		if (quant > 0)
		{
			inventory["Food"].Decrease(quant);
			agent.inventory["Food"].Increase(quant);
		}
	}
}