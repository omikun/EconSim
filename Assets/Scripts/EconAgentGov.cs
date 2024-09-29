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
	public override void Init(AgentConfig cfg, float _initCash, List<string> b, float _initStock, float maxstock) {
		config = cfg;
		uid = uid_idx++;
		initStock = _initStock;
		initCash = _initCash;
		maxStock = maxstock;

        if (book == null)
			book = AuctionStats.Instance.book;
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
        inventory[com].bidQuantity = quant;
        inventory[com].bidPrice = price;
    }
	public override Offers Consume(AuctionBook book) {
        var bids = new Offers();

        //replenish depended commodities
        foreach (var entry in inventory)
		{
			var item = entry.Value;
			if (item.bidQuantity <= 0)
				continue;

			bids.Add(item.name, new Offer(item.name, item.bidPrice, item.bidQuantity, this));
		}
        return bids;
	}

    public override float Tick()
    {
        return 0f;
    }
}