using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
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
public class CommodityStock {
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
	public float quantity;
	public float maxQuantity;
	public float minPriceBelief;
	public float maxPriceBelief;
	public float meanCost; //total cost spent to acquire stock
	//number of units produced per turn = production * productionRate
	public float productionRate = 1; //# of assembly lines


    public String Stats(String header) 
    {
        String ret = header + commodityName + ", stock, " + quantity + "\n"; 
        ret += header + commodityName + ", max_stock, " + maxQuantity + "\n"; 
        ret += header + commodityName + ", minPriceBelief, " + minPriceBelief + "\n";
        ret += header + commodityName + ", maxPriceBelief, " + maxPriceBelief + "\n";
        ret += header + commodityName + ", sellQuant, " + sellHistory.Peek().quantity + "\n";
        ret += header + commodityName + ", buyQuant, " + buyHistory.Peek().quantity + "\n";
        return ret;
    }
	public CommodityStock (string _name, float _quantity=1, float _maxQuantity=10, 
					float _meanPrice=1, float _production=1)
	{
		buyHistory = new History();
		sellHistory = new History();
		commodityName = _name;
		quantity = _quantity;
		maxQuantity = _maxQuantity;
		minPriceBelief = _meanPrice / 2;
		maxPriceBelief = _meanPrice * 2;
		meanCost = _meanPrice;
		buyHistory.Enqueue(new Transaction(1,_meanPrice));
		sellHistory.Enqueue(new Transaction(1,_meanPrice));
		productionRate = _production;
	}
	public void Tick()
	{
        sellHistory.Tick();
        buyHistory.Tick();
	}
	public float Buy(float quant, float price)
	{
		//update meanCost of units in stock
        var totalCost = meanCost * this.quantity + price * quant;
        this.meanCost = totalCost / this.quantity;
        Assert.IsTrue(Deficit() >= quant);
        quant = Mathf.Min(quant, Deficit());
		this.quantity += quant;
        buyHistory.Enqueue(new Transaction(price, quant));
		//return adjusted quant;
		return quant;
	}
	public void Sell(float quant, float price)
	{
		this.quantity += quant;
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
		minPriceBelief = Mathf.Max(cost, minPriceBelief);
		maxPriceBelief = Mathf.Max(minPriceBelief*1.1f, maxPriceBelief);
		minPriceBelief = Mathf.Clamp(minPriceBelief, 0.1f, 900);
		maxPriceBelief = Mathf.Clamp(maxPriceBelief, 1.1f, 1000);
	}
	
    public void UpdateBuyerPriceBelief(in Trade trade, in Commodity commodity)
    {
		//SanePriceBeliefs();
        Assert.IsTrue(minPriceBelief < maxPriceBelief);

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

        if ( quantityBought * 2 > trade.offerQuantity ) //at least 50% offer filled
        {
            // move limits inward by 10%
            var range = maxPriceBelief - minPriceBelief;
            maxPriceBelief -= range / 20;
            minPriceBelief += range / 20;
        }
        else 
        {
            maxPriceBelief *= 1.1f;
        }
        if ( commodity.bids[^1] > commodity.asks[^1] && quantity < maxQuantity/4 ) //more bids than asks and inventory < 1/4 max
        {
            maxPriceBelief += deltaMean;
            minPriceBelief += deltaMean;
        }
        else if ( trade.price > trade.clearingPrice || commodity.bids[^1] < commodity.asks[^1])   //bid price > trade price
                            // or (supply > demand and offer > historical mean)
        {
            var overbid = 0f; //bid price - trade price
            maxPriceBelief -= overbid * 1.1f;
            minPriceBelief -= overbid * 1.1f;
        }
        else if (commodity.bids[^1] > commodity.asks[^1])     //demand > supply
        {
            //translate belief range up 1/5th of historical mean price
            maxPriceBelief += historicalMeanPrice/5;
            minPriceBelief += historicalMeanPrice/5;
        } else {
            //translate belief range down 1/5th of historical mean price
            maxPriceBelief -= historicalMeanPrice/5;
            minPriceBelief -= historicalMeanPrice/5;
        }

        SanePriceBeliefs();
    }
public void UpdateSellerPriceBelief(in Trade trade, in Commodity commodity)
    {
		//SanePriceBeliefs();
        Assert.IsTrue(minPriceBelief < maxPriceBelief);

		var meanBeliefPrice = (minPriceBelief + maxPriceBelief) / 2;
		var deltaMean = meanBeliefPrice - trade.clearingPrice; //TODO or use auction house mean price?
        var quantitySold = trade.offerQuantity - trade.remainingQuantity;
        var historicalMeanPrice = commodity.prices.LastAverage(10);
        var price_mean = 12f;
        var market_share = quantitySold / commodity.trades[^1];
        var offer_price = trade.price;
        var weight = quantitySold / trade.offerQuantity; //quantitySold / quantityAsked
        var displacement = weight * meanBeliefPrice;
        if (weight == 0)
        {
            maxPriceBelief -= displacement / 6;
            minPriceBelief -= displacement / 6;
        }
        else if (market_share < .75f)
        {
            maxPriceBelief -= displacement / 7;
            minPriceBelief -= displacement / 7;
        }
        else if (offer_price < trade.clearingPrice)
        {
            var overbid = offer_price - trade.clearingPrice;
            maxPriceBelief += overbid * 1.2f;
            minPriceBelief += overbid * 1.2f;
        }
        else if (commodity.bids[^1] > commodity.asks[^1])     //demand > supply
        {
            //translate belief range up 1/5th of historical mean price
            maxPriceBelief += historicalMeanPrice/5;
            minPriceBelief += historicalMeanPrice/5;
        } else {
            //translate belief range down 1/5th of historical mean price
            maxPriceBelief -= historicalMeanPrice/5;
            minPriceBelief -= historicalMeanPrice/5;
        }
		
		//SanePriceBeliefs();
		Assert.IsFalse(float.IsNaN(minPriceBelief));
	}
	//TODO change quantity based on historical price ranges & deficit
	public float Deficit() { 
		var shortage = maxQuantity - quantity;
		//Assert.IsTrue(shortage >= 0);
		return Mathf.Max(0, shortage);
    }
	public float Surplus() { return quantity; }
}