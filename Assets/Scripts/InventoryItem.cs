using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Assertions;


public class InventoryItem {
	public string commodityName { get; private set; }
	const float significant = 0.25f;
	const float sig_imbalance = .33f;
	const float lowInventory = .1f;
	const float highInventory = 2f;
	public TransactionHistory buyHistory;
	public TransactionHistory sellHistory;
	public float cost = 1;
    public float last_price = 1;
	public float wobble = .02f;
	public float Quantity { get; private set; }
    public float quantityTradedThisRound = 0;
    public float costThisRound = 0;
	public float maxQuantity;
	public float minPriceBelief;
	public float maxPriceBelief;
	public float meanCostThisRound; //total cost spent to acquire stock
    public float meanCost; 
	//number of units produced per turn = production * productionRate
	public float productionRate = 1; //# of assembly lines
    List<string> debug_msgs = new List<string>();
    bool boughtThisRound = false;
    bool soldThisRound = false;
    public float bidPrice = 0;
    public float bidQuantity = 0;
    public float askPrice = 0;
    public float askQuantity = 0;



    public String Stats(String header) 
    {
        String ret = header + commodityName + ", stock, " + Quantity + ",n/a\n"; 
        foreach( var msg in debug_msgs )
        {
            //ret += header + commodityName + ", " + msg + "\n";
            ret += header + commodityName + ", minPriceBelief, " + minPriceBelief + ", " + msg + "\n";
            ret += header + commodityName + ", maxPriceBelief, " + maxPriceBelief + ", " + msg + "\n";
        }
        debug_msgs.Clear();
        //ret += header + commodityName + ", max_stock, " + maxQuantity + ",n/a\n"; 
        if (boughtThisRound)
        {
            ret += header + commodityName + ", buyQuant, " + buyHistory[^1].quantity + ",n/a\n";
        }
        if (soldThisRound)
        {
            ret += header + commodityName + ", sellQuant, " + sellHistory[^1].quantity + ",n/a\n";
        }
        if (boughtThisRound || soldThisRound)
        {
            ret += header + commodityName + ", meanPrice, " + meanCostThisRound + ",n/a\n";
        }
        ret += header + commodityName + ", bidPrice, " + bidPrice + ",n/a\n";
        ret += header + commodityName + ", bidQuantity, " + bidQuantity + ",n/a\n";
        ret += header + commodityName + ", askPrice, " + askPrice + ",n/a\n";
        ret += header + commodityName + ", askQuantity, " + askQuantity + ",n/a\n";

        bidPrice = 0;
        bidQuantity = 0;
        askPrice = 0;
        askQuantity = 0;
        return ret;
    }
	public InventoryItem (string _name, float _quantity=1, float _maxQuantity=10, 
					float _meanPrice=1, float _production=1)
	{
		buyHistory = new TransactionHistory();
		sellHistory = new TransactionHistory();
		commodityName = _name;
		Quantity = _quantity;
		maxQuantity = _maxQuantity;
        Assert.IsTrue(_meanPrice >= 0); //TODO really should never be 0???
		minPriceBelief = _meanPrice / 2;
		maxPriceBelief = _meanPrice * 2;
		meanCostThisRound = _meanPrice;
        meanCost = _meanPrice;
		buyHistory.Add(new Transaction(1,_meanPrice));
		sellHistory.Add(new Transaction(1,_meanPrice));
		productionRate = _production;
	}
	public void Tick()
	{
	}
    public float Increase(float quant)
    {
        Quantity += quant;
        Assert.IsTrue(quant >= 0);
        return Quantity;
    }
    public float Decrease(float quant)
    {
        Quantity -= quant;
        //Assert.IsTrue(Quantity >= 0);
        return Quantity;
    }
    public void ClearRoundStats() 
    {
        costThisRound = 0;
        quantityTradedThisRound = 0;
        soldThisRound = false;
        boughtThisRound = false;
    }
	public float Buy(float quant, float price)
	{
        // UnityEngine.Debug.Log("buying " + commodityName + " " + quant.ToString("n2") + " for " + price.ToString("c2") + " currently have " + Quantity.ToString("n2"));
		//update meanCost of units in stock
        Assert.IsTrue(quant > 0);

        meanCostThisRound = (quantityTradedThisRound == 0) ? 0 : costThisRound / quantityTradedThisRound;
        meanCost = (meanCost * Quantity + quant * price) / (Quantity + quant);
		Quantity += quant;
        quantityTradedThisRound += quant;
        costThisRound += price;
        var prevMinPriceBelief = minPriceBelief;
        var prevMaxPriceBelief = maxPriceBelief;
        last_price = price;
        if (boughtThisRound)
        {
            buyHistory.UpdateLast(new Transaction(price, quant));
        } else {
            buyHistory.Add(new Transaction(price, quant));
        }
        boughtThisRound = true;
		//return adjusted quant;
		return quant;
	}
	public void Sell(float quant, float price)
	{
		//update meanCost of units in stock
        Assert.IsTrue(Quantity >= 0);
        if (quant == 0 || Surplus() == 0)
            return;
        // UnityEngine.Debug.Log("sell quant is " + quant + " and surplus is " + Surplus());
        quant = Mathf.Min(quant, Surplus());
        Assert.IsTrue(quant > 0);
		Quantity -= quant;
        quantityTradedThisRound += quant;
        costThisRound += price;
        meanCostThisRound = (quantityTradedThisRound == 0) ? 0 : costThisRound / quantityTradedThisRound;
        last_price = price;
        if (soldThisRound)
        {
            sellHistory.UpdateLast(new Transaction(price, quant));
        } else {
            sellHistory.Add(new Transaction(price, quant));
        }
        soldThisRound = true;
	}
	public float GetPrice()
	{
		SanePriceBeliefs();
		var p = UnityEngine.Random.Range(minPriceBelief, maxPriceBelief);
		return p;
	}
	void SanePriceBeliefs()
	{
		//minPriceBelief = Mathf.Max(cost, minPriceBelief); TODO maybe consider this eventually?
		minPriceBelief = Mathf.Clamp(minPriceBelief, 0.1f, 900f);
		maxPriceBelief = Mathf.Max(minPriceBelief*1.1f, maxPriceBelief);
		maxPriceBelief = Mathf.Clamp(maxPriceBelief, 1.1f, 1000f);
        Assert.IsTrue(minPriceBelief < maxPriceBelief);
	}
	
    public void UpdateBuyerPriceBelief(String agentName, in Offer trade, in Commodity commodity)
    {
        var prevMinPriceBelief = minPriceBelief;
        var prevMaxPriceBelief = maxPriceBelief;

        // implementation following paper
		var meanBeliefPrice = (minPriceBelief + maxPriceBelief) / 2;
		var deltaMean = Mathf.Abs(meanBeliefPrice - trade.clearingPrice); //TODO or use auction house mean price?
        var quantityBought = trade.offerQuantity - trade.remainingQuantity;
        var historicalMeanPrice = commodity.avgClearingPrice.LastAverage(10);
        var displacement = deltaMean / historicalMeanPrice;
        Assert.IsTrue(historicalMeanPrice >= 0);
        string reason_msg = "none";

        if ( quantityBought * 2 > trade.offerQuantity ) //at least 50% offer filled
        {
            // move limits inward by 10 of upper limit%
            var adjustment = maxPriceBelief * 0.1f;
            maxPriceBelief -= adjustment;
            minPriceBelief += adjustment;
            reason_msg = "buy>.5";
        }
        else 
        {
            // move upper limit by 10%
            maxPriceBelief *= 1.1f;
            reason_msg = "buy<=.5";
        }
        if ( trade.offerQuantity < commodity.asks[^1] && Quantity < maxQuantity/4 ) //bid more than total asks and inventory < 1/4 max
        {
            maxPriceBelief *= displacement;
            minPriceBelief *= displacement;
            reason_msg += "_supply<demand_and_low_inv";
        }
        else if ( trade.offerPrice > trade.clearingPrice 
            || (commodity.asks[^1] > commodity.bids[^1] && trade.offerPrice > historicalMeanPrice))   //bid price > trade price
                            // or (supply > demand and offer > historical mean)
        {
            var overbid = Mathf.Abs(trade.offerPrice - trade.clearingPrice); //bid price - trade price
            maxPriceBelief -= overbid * 1.1f;
            minPriceBelief -= overbid * 1.1f;
            reason_msg += "_supply>demand_and_overbid";
        }
        else if (commodity.bids[^1] > commodity.asks[^1])     //demand > supply
        {
            //translate belief range up 1/5th of historical mean price
            maxPriceBelief += historicalMeanPrice/5;
            minPriceBelief += historicalMeanPrice/5;
            reason_msg += "_supply<demand";
        } else {
            //translate belief range down 1/5th of historical mean price
            maxPriceBelief -= historicalMeanPrice/5;
            minPriceBelief -= historicalMeanPrice/5;
            reason_msg += "_supply>demand";
        }

        SanePriceBeliefs();
        // UnityEngine.Debug.Log("buyer " + agentName + " stock: " + commodityName + " min price belief: " + prevMinPriceBelief + " -> " + minPriceBelief);
        // UnityEngine.Debug.Log("buyer " + agentName + " stock: " + commodityName + " max price belief: " + prevMaxPriceBelief + " -> " + maxPriceBelief);
        Assert.IsTrue(minPriceBelief < maxPriceBelief);
        debug_msgs.Add(reason_msg);
    }
public void UpdateSellerPriceBelief(String agentName, in Offer trade, in Commodity commodity)
    {
        var prevMinPriceBelief = minPriceBelief;
        var prevMaxPriceBelief = maxPriceBelief;
		//SanePriceBeliefs();

		var meanBeliefPrice = (minPriceBelief + maxPriceBelief) / 2;
		var deltaMean = meanBeliefPrice - trade.clearingPrice; //TODO or use auction house mean price?
        var quantitySold = trade.offerQuantity - trade.remainingQuantity;
        var historicalMeanPrice = commodity.avgClearingPrice.LastAverage(10);
        var market_share = quantitySold / commodity.trades[^1];
        var offer_price = trade.offerPrice;
        var weight = quantitySold / trade.offerQuantity; //quantitySold / quantityAsked
        var displacement = (1 - weight) * meanBeliefPrice;

        string reason_msg = "none";
        if (weight == 0)
        {
            maxPriceBelief -= displacement / 6;
            minPriceBelief -= displacement / 6;
            reason_msg = "seller_sold_none";
        }
        else if (market_share < .75f)
        {
            maxPriceBelief -= displacement / 7;
            minPriceBelief -= displacement / 7;
            reason_msg = "seller_market_share_<.75";
        }
        else if (offer_price < trade.clearingPrice)
        {
            var underbid = trade.clearingPrice - offer_price;
            maxPriceBelief += underbid * 1.2f;
            minPriceBelief += underbid * 1.2f;
            reason_msg = "seller_under_bid";
        }
        else if (commodity.bids[^1] > commodity.asks[^1])     //demand > supply
        {
            //translate belief range up 1/5th of historical mean price
            maxPriceBelief += historicalMeanPrice/5;
            minPriceBelief += historicalMeanPrice/5;
            reason_msg = "seller_demand>supply";
        } else {
            //translate belief range down 1/5th of historical mean price
            maxPriceBelief -= historicalMeanPrice/5;
            minPriceBelief -= historicalMeanPrice/5;
            reason_msg = "seller_demand<=supply";
        }
		
        //ensure buildable price at least cost of input commodities

		SanePriceBeliefs();
		Assert.IsFalse(float.IsNaN(minPriceBelief));
        // UnityEngine.Debug.Log("seller " + agentName + " stock: " + commodityName + " min price belief: " + prevMinPriceBelief + " -> " + minPriceBelief);
        // UnityEngine.Debug.Log("seller " + agentName + " stock: " + commodityName + " max price belief: " + prevMaxPriceBelief + " -> " + maxPriceBelief);
        Assert.IsTrue(minPriceBelief < maxPriceBelief);
        debug_msgs.Add(reason_msg);
	}
	//TODO change quantity based on historical price ranges & deficit
	public float Deficit() { 
		var shortage = maxQuantity - Quantity;
		return Mathf.Max(0, shortage);
    }
	public float Surplus() { return Quantity; }
}