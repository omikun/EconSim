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
	public override void Init(AgentConfig cfg, AuctionStats at, float _initCash, List<string> b, float _initStock, float maxstock) {
		config = cfg;
		uid = uid_idx++;
		initStock = _initStock;
		initCash = _initCash;
		maxStock = maxstock;

		book = at.book;
		auctionStats = at;
		//list of commodities self can produce
		//get initial stockpiles
		outputNames = b;
		cash = initCash;
		prevCash = cash;
		inputs.Clear();
        foreach(var good in book)
        {
            var name = good.Key;
            inputs.Add(name);
            AddToInventory(name, 0, maxstock, 1, 0);
        }
		inventory["Food"].Increase(200);
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
		header += uid.ToString() + ", " + "none" + ", "; //profession
		foreach (var stock in inventory)
		{
			log += stock.Value.Stats(header);
		}
		log += header + "cash, stock, " + cash + ", n/a\n";
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
	public void QueueOffer(string com, float quant)
	{
		var offerPrice = book[com].avgClearingPrice[^1] * 1.05f;
		inventory[com].ChangePendingOffer(quant, offerPrice);
	}
	public override Offers Consume(AuctionBook book) 
	{
        var bids = new Offers();

        //replenish depended commodities
        foreach (var entry in inventory)
		{
			var item = entry.Value;
			// if (item.bidQuantity <= 0)
				// continue;
			var minQuantity = 100f;

			if (item.OfferQuantity > 0)
			{
				bids.Add(item.name, new Offer(item.name, item.OfferPrice, item.OfferQuantity, this));
			} else if (item.Quantity < minQuantity)
			{
				var offerPrice = book[item.name].avgClearingPrice[^1] * 1.05f;
				var delta = minQuantity - item.Quantity;
				bids.Add(item.name, new Offer(item.name, offerPrice, delta, this));

			}
			//bids.Add(item.name, new Offer(item.name, item.bidPrice, item.bidQuantity, this));
			//TODO add either bid or asks from offer quantity/price
			//add back to offer when unsold or unbought
			//or only subtract on successful transaction
		}
        return bids;
	}
	public override Offers CreateAsks()
	{
		var asks = new Offers();

        foreach (var entry in inventory)
		{
			var item = entry.Value;

			if (item.OfferQuantity < 0)
			{
				asks.Add(item.name, new Offer(item.name, item.OfferPrice, -item.OfferQuantity, this));
				Debug.Log(auctionStats.round + " gov asked " + item.OfferQuantity.ToString("n2") + " " + item.name);
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
		//quant should be no more than what gov's inventory holds
		//only enough to refill agent's inv back to 2
		//should not be negative in case agent has more than 2 already
		const float refill = 1f;
		var quant = Mathf.Min(refill, inventory["Food"].Quantity);
		quant = Mathf.Min(quant, refill - agent.inventory["Food"].Quantity);
		quant = Mathf.Max(quant, 0f);
		inventory["Food"].Decrease(quant);
		agent.inventory["Food"].Increase(quant);
		Debug.Log(auctionStats.round + " Fed agent" + agent.name + " " + quant + " food");
	}
}