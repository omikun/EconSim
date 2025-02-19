using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using DG.Tweening;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Android;
using UnityEngine.Assertions;


/*
public class OncePerRoundFloat {
    float value;
    int round = 0;
    public float get()
        { 
        if (auctionStats.round == round) {
            return value;
        } else {
            round = auctionStats.round;
            value = ComputeProduction();
            return value;
        }
    }
}
//*/
public class InventoryItem {
	public string name { get; private set; }
    protected EconAgent agent;
    protected AuctionStats auctionStats;
    protected ResourceController rsc { get; private set; }
	const float significant = 0.25f;
	const float sig_imbalance = .33f;
	const float lowInventory = .1f;
	const float highInventory = 2f;
	public TransactionHistory buyHistory;
	public TransactionHistory saleHistory;
	public float unitCost = 1;
	public float wobble = .02f;
	public float Quantity { get; private set; }
	public string QuantityString
	{
		get { return Quantity.ToString("n2") + " " + name; }
	}
    //gov use
    public float TargetQuantity = 0f;// { get; private set; }
    public float OfferQuantity;// { get; private set; }
    public float OfferPrice;// { get; private set; }
    //end gov use
    public bool CanOfferAdditionalThisRound = true;
    private float _offersThisRound = 0;
    public float offersThisRound
    {
	    get { return _offersThisRound; }
	    set { _offersThisRound = value;
		    UpdateNiceness = true;
	    }
    }

    public string offersThisRoundString
    {
	    get { return " offering: " + offersThisRound.ToString("n2") + " " + name; }
    }

    public float quantityTradedThisRound = 0;
	public float meanPriceThisRound; //total cost spent to acquire stock
    public float costThisRound = 0;
    
	public float maxQuantity;
	public float priceBelief;
    public float meanCost; 
	//number of units produced per turn = production * productionRate
	public float ProductionPerBatch = 1; //num produced per batch
	public float BaseProduction = 0; //num produced per batch
	float batchRate = 1; //num produced per batch
    float realProductionRate; //actual after modifiers
    float productionDeRate = 1; //if agent gets hurt/reduced productivity
    public float productionChance = 1; //if agent gets into an accident?
    List<string> debug_msgs = new();
    bool boughtThisRound = false;
    bool soldThisRound = false;
    public float bidPrice = 0;
    public float bidQuantity = 0;
    public float askPrice = 0;
    public float askQuantity = 0;
    public int askOrder = 0;
    public int bidOrder = 0;

    public float Availability()
    {
        return Quantity/maxQuantity;
    }

    public float GetMaxBatchRate()
    {
	    return batchRate;
    }
    public float GetMaxProductionRate(float numBatches = -1)
    {
	    if (numBatches == -1)
		    numBatches = batchRate;

	    if (numBatches == 0)
		    return BaseProduction;
	    if (rsc.productionMultiplier < 1)
		    Debug.Log(name + " productionMultiplier " + rsc.productionMultiplier);
        //derate
        float rate = ProductionPerBatch * numBatches * productionDeRate * rsc.productionMultiplier;
        //random chance derate
        var chance = rsc.productionChance;
        realProductionRate = (UnityEngine.Random.value < chance) ? rate : 0;
        
		realProductionRate = Mathf.Min(realProductionRate, Deficit()); //can't produce more than max stock

        return realProductionRate;
    }


    public String Stats(String header) 
    {
        String ret = header + name + ", stock, " + Quantity + ",n/a\n"; 
        foreach( var msg in debug_msgs )
        {
            //ret += header + commodityName + ", " + msg + "\n";
            // ret += header + name + ", minPriceBelief, " + minPriceBelief + ", " + msg + "\n";
            ret += header + name + ", maxPriceBelief, " + priceBelief + ", " + msg + "\n";
        }
        debug_msgs.Clear();
        //ret += header + commodityName + ", max_stock, " + maxQuantity + ",n/a\n"; 
        if (boughtThisRound)
        {
            ret += header + name + ", buyQuant, " + buyHistory[^1].Quantity + ",n/a\n";
        }
        if (soldThisRound)
        {
            ret += header + name + ", sellQuant, " + saleHistory[^1].Quantity + ",n/a\n";
        }
        if (boughtThisRound || soldThisRound)
        {
            ret += header + name + ", meanPrice, " + meanPriceThisRound + ",n/a\n";
        }
        if (bidQuantity > 0)
        {
            ret += header + name + ", bidQuantity, " + bidQuantity + ",n/a\n";
        }
        if (bidPrice > 0)
        {
            ret += header + name + ", bidPrice, " + bidPrice + ", " + bidOrder +"\n";
        }
        if (askPrice > 0)
        {
            ret += header + name + ", askPrice, " + askPrice + ", " + askOrder +"\n";
        }
        if (askQuantity > 0)
        {
            ret += header + name + ", askQuantity, " + askQuantity + ",n/a\n";
        }

        bidPrice = 0;
        askPrice = 0;
        return ret;
    }

	public InventoryItem (EconAgent a, AuctionStats at, string _name, float _quantity, float _maxQuantity, 
					ResourceController _rsc)
	{
		var _meanPrice = _rsc.marketPrice;
		var _production = _rsc.productionPerBatch;
		var _baseProduction = _rsc.baseProduction;
		var _batchRate = _rsc.batchRate;
		rsc = _rsc;
		agent = a;
        auctionStats = at;
		buyHistory = new TransactionHistory();
		saleHistory = new TransactionHistory();
		name = _name;
		Quantity = _quantity;
		maxQuantity = _maxQuantity;
        Assert.IsTrue(_meanPrice >= 0); //TODO really should never be 0???
        priceBelief = _meanPrice;
		meanPriceThisRound = _meanPrice;
        meanCost = _meanPrice;
		ProductionPerBatch = _production;
		BaseProduction = _baseProduction;
		batchRate = _batchRate;
	}
	public void Tick()
	{
	}
    public void ChangePendingOffer(float quant, float price)
    {
        //if going back towards zero, make sure don't overshoot to simplify zeroing out offer
        if (OfferQuantity != 0 && Mathf.Sign(OfferQuantity) != Mathf.Sign(quant) && Mathf.Abs(quant) > Mathf.Abs(OfferQuantity))
        {
            OfferQuantity = 0;
        } else {
            OfferQuantity += quant;
        }
        //don't sell more than what's in inventory
        if (Quantity + OfferQuantity < 0)
        {
            OfferQuantity = -Quantity;
        }
        OfferPrice = price;

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
    public void Produced(float quant, float costVol)
    {
        var prevCostVol = unitCost * Quantity;
        Increase(quant);
        unitCost = (costVol + prevCostVol) / Quantity;
    }
    public void ClearRoundStats() 
    {
        costThisRound = 0;
        quantityTradedThisRound = 0;
        soldThisRound = false;
        boughtThisRound = false;
    }
	public void Buy(float quant, float price)
	{
        meanCost = (meanCost * Quantity + quant * price) / (Quantity + quant);
		Quantity += quant;
        OfferQuantity -= quant;
        OfferQuantity = Mathf.Max(0, OfferQuantity);
        bidQuantity -= quant;

        quantityTradedThisRound += quant;
        costThisRound += price * quant;
        meanPriceThisRound = (quantityTradedThisRound == 0) ? 0 : costThisRound / quantityTradedThisRound;

        //may buy multiple times per round
        if (boughtThisRound)
        {
            buyHistory.UpdateLast(new InventoryTransaction(price, quant));
        } else {
            buyHistory.Add(new InventoryTransaction(price, quant));
        }
        boughtThisRound = true;
        Assert.IsFalse(soldThisRound);
		//return adjusted quant;
	}
	public void Sell(float quant, float price)
	{
        Assert.IsTrue(quant <= Quantity);
        if (quant <= 0)
            return;

        OfferQuantity += quant; //offerQuantity is negative for sales
		Quantity -= quant;
        askQuantity -= quant;
        quantityTradedThisRound += quant;
        costThisRound += price * quant;
        meanPriceThisRound = (quantityTradedThisRound == 0) ? 0 : costThisRound / quantityTradedThisRound;
        
        if (soldThisRound)
        {
            saleHistory.UpdateLast(new InventoryTransaction(price, quant));
        } else {
            saleHistory.Add(new InventoryTransaction(price, quant));
        }
        soldThisRound = true;
        Assert.IsFalse(boughtThisRound);
	}
    public float FindSellCount(ResourceController rsc, int historySize, bool enablePriceFavorability)
	{
		if (Quantity < 1)
			return 0;

		float numAsks = Mathf.Floor(Quantity);

		if (enablePriceFavorability) {
			var avgPrice = rsc.avgBidPrice.LastAverage(historySize);
			var lowestPrice = saleHistory.Min();
			var highestPrice = saleHistory.Max();
			float favorability = .5f;
			if (true || lowestPrice != highestPrice)
			{
				favorability = Mathf.InverseLerp(lowestPrice, highestPrice, avgPrice);
			}
			//sell at least 1
			numAsks = Mathf.Max(1, favorability * Quantity);
			numAsks = Mathf.Floor(numAsks);

			Assert.IsTrue(numAsks <= Quantity);

            //Debug.Log(auctionStats.round + " " + agent.name + " FindSellCount " + c + ": avgPrice: " + avgPrice.ToString("c2") + " favorability: " + favorability.ToString("n2") + " numAsks: " + numAsks.ToString("n2") + " highestPrice: " + highestPrice.ToString("c2") + ", lowestPrice: " + lowestPrice.ToString("c2"));
        }
		return numAsks;
	}
    
    public float FindBuyCount(ResourceController rsc, int historySize, bool enablePriceFavorability)
	{
		float numBids = Mathf.Floor(Deficit());
		if (enablePriceFavorability)
		{
			var avgPrice = rsc.avgBidPrice.LastAverage(historySize);
			var lowestPrice = buyHistory.Min();
			var highestPrice = buyHistory.Max();
			
			float favorability = .5f;
			if (lowestPrice != highestPrice)
			{
				favorability = Mathf.InverseLerp(lowestPrice, highestPrice, avgPrice);
				favorability = Mathf.Clamp(favorability, 0, 1);
			}
			
			numBids = (1 - favorability) * Deficit();
			numBids = Mathf.Floor(numBids);
			numBids = Mathf.Max(0, numBids);

			Debug.Log(auctionStats.round + " " + agent.name + " FindBuyCount " + name + ": avgPrice: " + avgPrice.ToString("c2") + " favorability: " + (1 - favorability).ToString("n2") + " numBids: " + numBids.ToString("n2") + " highestPrice: " + highestPrice.ToString("c2") + ", lowestPrice: " + lowestPrice.ToString("c2"));
			Assert.IsTrue(numBids <= Deficit());
		}
		return numBids;
    }

	float Utility(float additionalQuant, float delta = 1f)
	{
		var c = name;
		var recipe = agent.book[agent.outputName].recipe;
		float numNeeded;
		if (c == agent.outputName)
		{
			var quant = agent.inventory[c].Quantity - additionalQuant;
			numNeeded = agent.book[c].productionPerBatch;
			var totalDelta = delta;// * numNeeded;
			//TODO only sell in increments of totalDelta??
			if (quant < totalDelta)
				return 0;
			return -1 * Mathf.Log10((quant - totalDelta) / quant); 
		}
		else if (c != "Food" && !recipe.ContainsKey(c)) //not input/output, MUST SELL
		{
			return Mathf.NegativeInfinity;
		} else //buying
		{
			var quant = agent.inventory[c].Quantity + additionalQuant;

			numNeeded = (c == "Food") ? 1f : recipe[c];
			var totalDelta = delta * numNeeded;
			return Mathf.Log10((quant + totalDelta) / quant); //FIXME account for recipe in niceness too
		}
	}

	private float _niceness = 0;
	public bool UpdateNiceness = true;
    public float GetNiceness(float delta = 1f)
    {
	    if (false == UpdateNiceness)
		    return _niceness;
	    
		var marketPrice = agent.book[name].marketPrice;
		Assert.IsTrue(marketPrice > 0);
		var utility = Utility(offersThisRound, delta);
		var niceness = utility / marketPrice;
//		Debug.Log(name + " GetNiceness " + niceness.ToString("f8") + " utility " + utility.ToString("f8") + "	marketPrice " + marketPrice.ToString("c2"));
		
		_niceness = niceness;
		UpdateNiceness = false;
		return _niceness;
    }
	public float GetPrice()
	{
		//SanePriceBeliefs();
		// var p = UnityEngine.Random.Range(minPriceBelief, priceBelief);
		return priceBelief;
	}
	void SanePriceBeliefs()
	{
		//minPriceBelief = Mathf.Max(cost, minPriceBelief); TODO maybe consider this eventually?
		priceBelief = Mathf.Clamp(priceBelief, 0.9f, 900f);
		// priceBelief = Mathf.Max(minPriceBelief*1.1f, priceBelief);
		// priceBelief = Mathf.Clamp(priceBelief, 1.1f, 1000f);
        // Assert.IsTrue(minPriceBelief < priceBelief);
	}
	
    public void UpdateBuyerPriceBelief(String agentName, in Offer trade, in ResourceController rsc)
    {
        if (trade.offerQuantity == 0)
	        return;

        // implementation following paper
        int history = 3;
        var meanBeliefPrice = priceBelief; //(minPriceBelief + priceBelief) / 2;
		var deltaMean = Mathf.Abs(meanBeliefPrice - trade.clearingPrice); //TODO or use auction house mean price?
        var quantityBought = trade.offerQuantity - trade.remainingQuantity;
        var historicalMeanPrice = rsc.avgClearingPrice.LastAverage(history);
        var minAskPrice = rsc.minAskPrice.Last();
        var minPrice = rsc.minClearingPrice.Last();
        var maxClearingPrice = rsc.maxClearingPrice.Last();
	    var supply = rsc.asks.LastAverage(3);
	    var demand = rsc.bids.LastAverage(3);
        var urgency = Quantity / maxQuantity; //if 5 remaining, 
        
        var displacement = deltaMean / historicalMeanPrice;
        Assert.IsTrue(historicalMeanPrice >= 0);
        string reason_msg = "no reason";

        var prevPriceBelief = priceBelief; 
        float boughtRatio = quantityBought / trade.offerQuantity;
        float supplyRatio = supply / demand;
        //if unable to fulfill full bid, 
			//if supply < demand
			//use market average or increase price belief
			//(lerp using how buy pressure if market is higher?)
			//if demand > supply
				//do 1-10% more than market price (depending on buy pressure and cash)
		//if fulfill all bids
			//if supply < demand
				//do nothing?
				//or raise price 0-1% depending on buy pressure
			//if supply > demand
				//drop price 5-10% depending on demand ratio


	    bool moreDemand = (supplyRatio < 0.8f); 
	    bool equalDemand = (supplyRatio <= 1.2f); 
	    bool moreSupply = supplyRatio > 1.2f;
	    
	    if (name == "Wood")
		    Debug.Log("wood buy");
	    if (quantityBought == 1 && trade.offerQuantity == 1)
	    {
		    priceBelief *= Mathf.Min(1, logRatio(supplyRatio, 3));
	    } else if (boughtRatio == 0)
	    {
		    // var upbid = Mathf.Pow(1.07f, Mathf.Sqrt(agent.DaysStarving*2));
		    var upbid = Mathf.Max(1.1f,logQuantity(Quantity));
		    // if (maxClearingPrice > priceBelief)
			   //  priceBelief = maxClearingPrice;
		    if (supply < 1)
			    priceBelief = historicalMeanPrice * upbid * 2;
		    else
			    priceBelief *= upbid;
		    Debug.Log(agent.name + " bought no " + name 
		              + " quantity: " + Quantity
		              + "; price belief: " + priceBelief.ToString("c2") 
		              + " max clearing price: " + maxClearingPrice.ToString("c2") 
		              + " upbid: " + upbid.ToString("n2"));
	    } else if (boughtRatio < 0.9f) //didn't buy it all / potentially no supply, raise price to signal
        {
	        var delta = 1 + agent.config.sellPriceDelta;
	        //buy more if have more money
	        //buy more if inventory is lower
				//target inventory? quality of life metric?
				
            
            float minItems = 4; //minimum items before price belief explodes
            float scaler = minItems - Quantity;
            if (scaler > 0 )
	            delta += Mathf.Pow(scaler, 4)/100f;
            if (supply > quantityBought)
	            priceBelief = historicalMeanPrice * logRatio(supplyRatio);
	        if (supply > trade.offerQuantity)
		        priceBelief *= delta;
        }
        else //bought all or bought enough
        {
	        var delta = logRatio(Quantity) * logRatio(supplyRatio);
	        priceBelief *= delta;
	        reason_msg = " bought all or enough quantity: " + Quantity + " delta: " + delta.ToString("n2");
        }
        var tempPriceBelief = priceBelief;
        if (Quantity < agent.config.minItemRaiseBuyPrice)
        {
	        // priceBelief = Mathf.Max(priceBelief, agent.book[name].marketPrice);
	        priceBelief = agent.book[name].marketPrice;
	        priceBelief *= (1 + .01f * Mathf.Pow(agent.config.minItemRaiseBuyPrice - Quantity, 2));
        }

        string demandstr = moreDemand ? "more demand" : equalDemand ? "equal demand" : "more supply";
        
        
        SanePriceBeliefs();
        Debug.Log(agent.auctionStats.round + " " + agent.name + " price belief update: " + name 
                  + " bid: " + trade.offerQuantity + " bought: " + quantityBought 
                  + " prev price " + prevPriceBelief.ToString("c2") 
                  + " new price belief " + priceBelief.ToString("c2")
                  + " boughtRatio " + boughtRatio.ToString("n2")
                  + " supply/demand " + supplyRatio.ToString("n2") 
                  + " tempPriceBelief " + tempPriceBelief.ToString("c2")
                  + " minItemRaiseBuyPrice " + agent.config.minItemRaiseBuyPrice.ToString("c2")
                  + reason_msg);
        return;

        if ( quantityBought * 2 > trade.offerQuantity ) //at least 50% offer filled
        {
            // move limits inward by 10 of upper limit%
            var adjustment = priceBelief * 0.1f;
            priceBelief -= adjustment;
            reason_msg = "buy>.5";
        }
        else 
        {
            // move upper limit by 10%
            priceBelief *= 1.1f;
            reason_msg = "buy<=.5";
        }
        if ( trade.offerQuantity < rsc.asks[^1] && Quantity < maxQuantity/4 ) //bid more than total asks and inventory < 1/4 max
        {
            priceBelief *= displacement;
            reason_msg += "_supply<demand_and_low_inv";
        }
        else if ( trade.offerPrice > trade.clearingPrice 
            || (rsc.asks[^1] > rsc.bids[^1] && trade.offerPrice > historicalMeanPrice))   //bid price > trade price
                            // or (supply > demand and offer > historical mean)
        {
            var overbid = Mathf.Abs(trade.offerPrice - trade.clearingPrice); //bid price - trade price
            priceBelief -= overbid * 1.1f;
            reason_msg += "_supply>demand_and_overbid";
        }
        else if (rsc.bids[^1] > rsc.asks[^1])     //demand > supply
        {
            //translate belief range up 1/5th of historical mean price
            priceBelief += historicalMeanPrice/5;
            reason_msg += "_supply<demand";
        } else {
            //translate belief range down 1/5th of historical mean price
            priceBelief -= historicalMeanPrice/5;
            reason_msg += "_supply>demand";
        }

        SanePriceBeliefs();
        // UnityEngine.Debug.Log("buyer " + agentName + " stock: " + commodityName + " min price belief: " + prevMinPriceBelief + " -> " + minPriceBelief);
        // UnityEngine.Debug.Log("buyer " + agentName + " stock: " + commodityName + " max price belief: " + prevMaxPriceBelief + " -> " + maxPriceBelief);
        // Assert.IsTrue(minPriceBelief < priceBelief);
        debug_msgs.Add(reason_msg);
    }

    float logRatio(float x, float scaler=10)
    {
	    return Mathf.Min(2, 1 - Mathf.Log10(x) / scaler);
    }

    //gentle slope with 1 at x=10, maxes at 1.5 when x=0, less than 1 when x>10
    float logQuantity(float x)
    {
	    return 1 - Mathf.Log((x + 1) / 10) / 10;
    }
    public void UpdateSellerPriceBelief(String agentName, in Offer trade, in ResourceController rsc)
    {
	    if (trade.offerQuantity == 0)
		    return;

	    if (name == "Tool")
		    Debug.Log("Tool updatesellerpricebelief");

	    var meanPriceBelief = priceBelief;//(minPriceBelief + priceBelief) / 2;
	    var deltaMean = meanPriceBelief - trade.clearingPrice; //TODO or use auction house mean price?
	    var quantitySold = trade.offerQuantity - trade.remainingQuantity;
	    var historicalMeanPrice = rsc.avgClearingPrice.LastAverage(3);
	    var minClearingPrice = rsc.minClearingPrice.Last();
	    var maxBidPrice = rsc.maxBidPrice.Last();
	    var avgBidPrice = rsc.avgBidPrice.Last();
	    //TODO market share over last 3 rounds
	    var market_share = quantitySold / rsc.trades.Last();
	    var offerPrice = trade.offerPrice;
	    var weight = quantitySold / trade.offerQuantity;
	    var displacement = (1 - weight) * meanPriceBelief;
	    var supply = rsc.asks.LastAverage(3);
	    var demand = rsc.bids.LastAverage(3);
	    var demandLastRound = rsc.bids.Last();
	    float supplyRatio = supply / demand;
	    var soldRatio = quantitySold / trade.offerQuantity;
	    var food = agent.inventory["Food"];

	    var prevPriceBelief = priceBelief;
	    var tempPriceBelief = priceBelief;

	    string reason = "last bids: " + demandLastRound + " reason: ";
	    //don't bother adjusting ask price if there is no demand!
	    if (demandLastRound < 1f)
	    {
		    reason += " no demand ";
	    } else if (market_share > .8f)
		    // if only seller, drive price up
        {
	        reason += "	market_share " + market_share.ToString("f2");
	        if (quantitySold > 1)
		        priceBelief *= 1.10f;
        }
        // Case 1: Sold all quantity offered
        else if (quantitySold == trade.offerQuantity)
        {
	        var delta = 1 + agent.config.sellPriceDelta;
	        priceBelief *= delta;
        }
        // Case 2: Did not sell any quantity, snap to highest bid
        else if (quantitySold == 0)
        {
            // Calculate a discount based on starvation days
            var power = 1 + agent.DaysStarving * Mathf.Log(supplyRatio*10)/10f;
            var delta = Mathf.Pow(.99f, power);
	        reason += " sold none / delta: " + delta + " .99 ^ " + power + " ";
            
            // Apply discount to the minimum clearing price if available
	        if (avgBidPrice < priceBelief)
	        {
		        priceBelief = avgBidPrice;
	        }
	        priceBelief *= delta;
        } 
        // General case: Adjust based on sell performance
        else
        {
	        reason += " sold " + quantitySold.ToString("f2") + " ratio " + soldRatio.ToString("f2");
	        var translator = -logRatio(soldRatio);//1.4f * soldRatio - 1f);
	        //TODO how does market price adjust this logic?
	        var sellPriceMultiplier = 1 + agent.config.sellPriceDelta * translator;
	        Assert.IsTrue(sellPriceMultiplier > .5f && sellPriceMultiplier < 1.5f);
	        priceBelief *= sellPriceMultiplier;
	        tempPriceBelief = priceBelief;

            // Additional constraint: Ensure minimum sell price based on costs
	        if (agent.config.minSellPrice)
	        {
                // Calculate total input costs
		        var otherCosts = agent.inventory.Values
			        .Where(item => agent.inputs.Contains(item.name))
			        .Sum(item => agent.book[item.name].marketPrice);
		        otherCosts += agent.book["Food"].marketPrice * 2 ;
                
                // Compute amortized quantity for cost per unit
                var amortizedQuantity = Mathf.Max(rsc.productionPerBatch, Quantity * 0.8f);
                var minCost = otherCosts / amortizedQuantity;
                // Ensure price is at least the cost
		        reason += " raise min cost " + minCost.ToString("c2") + " price belief: " + priceBelief.ToString("c2");
                priceBelief = Mathf.Max(minCost, priceBelief);
	        }
        }

		SanePriceBeliefs();
        // Log the update for debugging purposes
        Debug.Log(agent.auctionStats.round + " " + agent.name + " price belief update: " + name
                  + " asked: " + trade.offerQuantity + " sold: " + quantitySold
                  + " prev price " + prevPriceBelief.ToString("c2")
                  + " current price belief " + priceBelief.ToString("c2")
                  + " supply ratio " + supplyRatio.ToString("n2")
                  + " pre minSellPrice " + tempPriceBelief.ToString("c2")
                  + " sold ratio " + soldRatio.ToString("n2")
				  + reason);
        return;
        
        string reason_msg = "none";
        if (weight == 0)
        {
            priceBelief -= displacement / 6;
            reason_msg = "seller_sold_none";
        }
        else if (market_share < .75f)
        {
            priceBelief -= displacement / 7;
            reason_msg = "seller_market_share_<.75";
        }
        else if (offerPrice < trade.clearingPrice)
        {
            var underbid = trade.clearingPrice - offerPrice;
            priceBelief += underbid * 1.2f;
            reason_msg = "seller_under_bid";
        }
        else if (rsc.bids[^1] > rsc.asks[^1])     //demand > supply
        {
            //translate belief range up 1/5th of historical mean price
            priceBelief += historicalMeanPrice/5;
            reason_msg = "seller_demand>supply";
        } else {
            //translate belief range down 1/5th of historical mean price
            priceBelief -= historicalMeanPrice/5;
            reason_msg = "seller_demand<=supply";
        }
		
        //ensure buildable price at least cost of input commodities

		SanePriceBeliefs();
		// Assert.IsFalse(float.IsNaN(minPriceBelief));
        // UnityEngine.Debug.Log("seller " + agentName + " stock: " + commodityName + " min price belief: " + prevMinPriceBelief + " -> " + minPriceBelief);
        // UnityEngine.Debug.Log("seller " + agentName + " stock: " + commodityName + " max price belief: " + prevMaxPriceBelief + " -> " + maxPriceBelief);
        // Assert.IsTrue(minPriceBelief < priceBelief);
        debug_msgs.Add(reason_msg);
	}
	//TODO change quantity based on historical price ranges & deficit
	public float Deficit() { 
		var shortage = maxQuantity - Quantity;
		return Mathf.Max(0, shortage);
    }

	public float NumProduceable(ResourceController rsc)
	{
		return Quantity / rsc.recipe[name];
	}
}