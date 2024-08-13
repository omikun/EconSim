using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Assertions;


public class Transaction {
    public Transaction(float p, float q)
    {
        price = p;
        quantity = q;
    }
    public float price;
    public float quantity;
}
public class History : Queue<Transaction>
{
    public float min = 0;
    public float max = 0;
    public float Min() { return min; }
	public float Max() { return max; }
	public void Tick()
	{
		min = Min();
		max = Max();
	}
}
public class inventoryItem {
	public string commodityName { get; private set; }
	const float significant = 0.25f;
	const float sig_imbalance = .33f;
	const float lowInventory = .1f;
	const float highInventory = 2f;
	public History buyHistory;
	public History sellHistory;
	public float cost = 1;
    public float last_price = 1;
	public float wobble = .02f;
	public float Quantity { get; private set; }
    public float quantityTradedThisRound = 0;
    public float costThisRound = 0;
	public float maxQuantity;
	public float minPriceBelief;
	public float maxPriceBelief;
	public float meanPriceThisRound; //total cost spent to acquire stock
	//number of units produced per turn = production * productionRate
	public float productionRate = 1; //# of assembly lines
    List<string> debug_msgs = new List<string>();


    public String Stats(String header) 
    {
        String ret = header + commodityName + ", stock, " + Quantity + ",n/a\n"; 
        ret += header + commodityName + ", max_stock, " + maxQuantity + ",n/a\n"; 
        ret += header + commodityName + ", meanPrice, " + meanPriceThisRound + ",n/a\n"; 
        ret += header + commodityName + ", sellQuant, " + sellHistory.Peek().quantity + ",n/a\n";
        ret += header + commodityName + ", buyQuant, " + buyHistory.Peek().quantity + ",n/a\n";
        foreach( var msg in debug_msgs )
        {
            //ret += header + commodityName + ", " + msg + "\n";
            ret += header + commodityName + ", minPriceBelief, " + minPriceBelief + ", " + msg + "\n";
            ret += header + commodityName + ", maxPriceBelief, " + maxPriceBelief + ", " + msg + "\n";
        }
        debug_msgs.Clear();
        return ret;
    }
	public inventoryItem (string _name, float _quantity=1, float _maxQuantity=10, 
					float _meanPrice=1, float _production=1)
	{
		buyHistory = new History();
		sellHistory = new History();
		commodityName = _name;
		Quantity = _quantity;
		maxQuantity = _maxQuantity;
        Assert.IsTrue(_meanPrice >= 0); //TODO really should never be 0???
		minPriceBelief = _meanPrice / 2;
		maxPriceBelief = _meanPrice * 2;
		meanPriceThisRound = _meanPrice;
		buyHistory.Enqueue(new Transaction(1,_meanPrice));
		sellHistory.Enqueue(new Transaction(1,_meanPrice));
		productionRate = _production;
	}
	public void Tick()
	{
        sellHistory.Tick();
        buyHistory.Tick();
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
    }
	public float Buy(float quant, float price)
	{
        UnityEngine.Debug.Log("buying " + commodityName + " " + quant.ToString("n2") + " for " + price.ToString("c2") + " currently have " + Quantity.ToString("n2"));
		//update meanCost of units in stock
        Assert.IsTrue(quant > 0);

		Quantity += quant;
        quantityTradedThisRound += quant;
        costThisRound += price;
        meanPriceThisRound = (quantityTradedThisRound == 0) ? 0 : costThisRound / quantityTradedThisRound;
        var prevMinPriceBelief = minPriceBelief;
        var prevMaxPriceBelief = maxPriceBelief;
        last_price = price;
        buyHistory.Enqueue(new Transaction(price, quant));
		//return adjusted quant;
		return quant;
	}
	public void Sell(float quant, float price)
	{
		//update meanCost of units in stock
        Assert.IsTrue(Quantity >= 0);
        if (quant == 0 || Surplus() == 0)
            return;
        UnityEngine.Debug.Log("sell quant is " + quant + " and surplus is " + Surplus());
        quant = Mathf.Min(quant, Surplus());
        Assert.IsTrue(quant > 0);
		Quantity -= quant;
        quantityTradedThisRound += quant;
        costThisRound += price;
        meanPriceThisRound = (quantityTradedThisRound == 0) ? 0 : costThisRound / quantityTradedThisRound;
        last_price = price;
        sellHistory.Enqueue(new Transaction(price, quant));
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
	
    public void UpdateBuyerPriceBelief(String agentName, in Trade trade, in Commodity commodity)
    {
        var prevMinPriceBelief = minPriceBelief;
        var prevMaxPriceBelief = maxPriceBelief;
		//SanePriceBeliefs();

        // implementation following paper
        // TODO consolidate update to once per auction round per agent per commodity 
        // maybe multiple transactions depending on quantity bid/asked mismatch
        // need offer price, clearing/trace price, market share, mean price
        // demand vs supply, 
        //BUY
		var meanBeliefPrice = (minPriceBelief + maxPriceBelief) / 2;
		var deltaMean = meanBeliefPrice - trade.clearingPrice; //TODO or use auction house mean price?
        var quantityBought = trade.offerQuantity - trade.remainingQuantity;
        var historicalMeanPrice = commodity.prices.LastAverage(10);
        Assert.IsTrue(historicalMeanPrice >= 0);
        string reason_msg = "none";

        if ( quantityBought * 2 > trade.offerQuantity ) //at least 50% offer filled
        {
            // move limits inward by 10%
            var range = Mathf.Abs(maxPriceBelief - minPriceBelief);
            maxPriceBelief -= range / 20;
            minPriceBelief += range / 20;
            reason_msg = "buy>.5";
        }
        else 
        {
            maxPriceBelief *= 1.1f;
            reason_msg = "buy<=.5";
        }
        if ( commodity.bids[^1] > commodity.asks[^1] && Quantity < maxQuantity/4 ) //more bids than asks and inventory < 1/4 max
        {
            maxPriceBelief += deltaMean;
            minPriceBelief += deltaMean;
            reason_msg += "_supply<demand_and_low_inv";
        }
        else if ( trade.price > trade.clearingPrice || commodity.bids[^1] < commodity.asks[^1])   //bid price > trade price
                            // or (supply > demand and offer > historical mean)
        {
            var overbid = Mathf.Abs(trade.price - trade.clearingPrice); //bid price - trade price
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
        UnityEngine.Debug.Log("buyer " + agentName + " stock: " + commodityName + " min price belief: " + prevMinPriceBelief + " -> " + minPriceBelief);
        UnityEngine.Debug.Log("buyer " + agentName + " stock: " + commodityName + " max price belief: " + prevMaxPriceBelief + " -> " + maxPriceBelief);
        Assert.IsTrue(minPriceBelief < maxPriceBelief);
        debug_msgs.Add(reason_msg);
    }
public void UpdateSellerPriceBelief(String agentName, in Trade trade, in Commodity commodity)
    {
        var prevMinPriceBelief = minPriceBelief;
        var prevMaxPriceBelief = maxPriceBelief;
		//SanePriceBeliefs();

		var meanBeliefPrice = (minPriceBelief + maxPriceBelief) / 2;
		var deltaMean = meanBeliefPrice - trade.clearingPrice; //TODO or use auction house mean price?
        var quantitySold = trade.offerQuantity - trade.remainingQuantity;
        var historicalMeanPrice = commodity.prices.LastAverage(10);
        var market_share = quantitySold / commodity.trades[^1];
        var offer_price = trade.price;
        var weight = quantitySold / trade.offerQuantity; //quantitySold / quantityAsked
        var displacement = weight * meanBeliefPrice;

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
        UnityEngine.Debug.Log("seller " + agentName + " stock: " + commodityName + " min price belief: " + prevMinPriceBelief + " -> " + minPriceBelief);
        UnityEngine.Debug.Log("seller " + agentName + " stock: " + commodityName + " max price belief: " + prevMaxPriceBelief + " -> " + maxPriceBelief);
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