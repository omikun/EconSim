using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
using EconSim;
public abstract class OfferSorter
{
	public abstract void SortOffer(ref OfferList offers);
}
class SortOfferByPriceAscend : OfferSorter
{
	public override void SortOffer(ref OfferList offers)
	{
		offers.Sort((x, y) => x.offerPrice.CompareTo(y.offerPrice)); //inc
	}
}
class SortOfferByPriceDescend : OfferSorter
{
	public override void SortOffer(ref OfferList offers)
	{
		offers.Sort((x, y) => y.offerPrice.CompareTo(x.offerPrice)); //dec
	}
}

public abstract class TradePriceResolver
{
	public abstract float ResolvePrice(Offer ask, Offer bid);
}

class TakeAskPrice : TradePriceResolver
{
	public override float ResolvePrice(Offer ask, Offer bid)
	{
		return ask.offerPrice;
	}
}
class TakeBidPrice : TradePriceResolver
{
	public override float ResolvePrice(Offer ask, Offer bid)
	{
		return bid.offerPrice;
	}
}
class TakeAverage : TradePriceResolver
{
	public override float ResolvePrice(Offer ask, Offer bid)
	{
		return (ask.offerPrice + bid.offerPrice) / 2f;
	}
}

public class TradeStats
{
	public float moneyExchangedThisRound = 0;
	public float goodsExchangedThisRound = 0;
	public float minClearingPrice = float.MaxValue;
	public float maxClearingPrice = 0;
	public float minAskPrice = float.MaxValue;
	public float maxAskPrice = 0;
	public float minBidPrice = float.MaxValue;
	public float maxBidPrice = 0;
}
public abstract class TradeResolution
{
    protected OfferTable askTable, bidTable;
    private AuctionStats auctionTracker;
    protected FiscalPolicy fiscalPolicy;
    protected OfferSorter askSorter;
    protected OfferSorter bidSorter;
    protected TradePriceResolver tradePriceResolver;
    protected enum LoopState { None, ContinueBids, ContinueAsks, Break }


    public TradeResolution(AuctionStats aStats, FiscalPolicy fp, OfferTable at, OfferTable bt)
    {
	    auctionTracker = aStats;
	    fiscalPolicy = fp;
	    askTable = at;
	    bidTable = bt;

	    var cfg = auctionTracker.config;

	    askSorter = (cfg.askSortOrder == OfferSortOrder.Ascending)
		    ? new SortOfferByPriceAscend()
		    : new SortOfferByPriceDescend();
	    bidSorter = (cfg.bidSortOrder == OfferSortOrder.Ascending)
		    ? new SortOfferByPriceAscend()
		    : new SortOfferByPriceDescend();

	    //what about random?
	    ConfigureTradePriceResolution();
    }
    public void ConfigureTradePriceResolution()
    {
	    var cfg = auctionTracker.config;
	    switch (cfg.resolveTradePrice)
	    {
		    case ResolveTradePrice.TakeAskPrice:
			    tradePriceResolver = new TakeAskPrice();
			    break;
		    case ResolveTradePrice.TakeBidPrice:
			    tradePriceResolver = new TakeBidPrice();
			    break;
		    case ResolveTradePrice.TakeAveragePrice:
			    tradePriceResolver = new TakeAverage();
			    break;
		    default:
			    Assert.IsTrue(false, "Unknown trade price type");
			    break;
	    }
    }

    protected abstract LoopState EndTrades(Offer ask, Offer bid);
    public virtual void ResolveOffers(ResourceController rsc, ref TradeStats stats)
	{
		var asks = askTable[rsc.name];
		var bids = bidTable[rsc.name];
    
		asks.Shuffle();
		bids.Shuffle();

	    askSorter.SortOffer(ref asks);
	    bidSorter.SortOffer(ref bids);
    
	    for (int i = 0; i < asks.Count; i++)
			asks[i].agent.inventory[rsc.name].askOrder = i;

	    for (int i = 0; i < bids.Count; i++)
			bids[i].agent.inventory[rsc.name].bidOrder = i;
		
		int askIdx = 0;
		int bidIdx = 0;
    
		while (askIdx < asks.Count && bidIdx < bids.Count)
		{
			var ask = asks[askIdx];
			var bid = bids[bidIdx];

			var cond = EndTrades(ask, bid);
			if (cond == LoopState.Break) { break; }
			else if (cond == LoopState.ContinueAsks) { askIdx++; continue; }
			else if (cond == LoopState.ContinueBids) { bidIdx++; continue; }
    
			var clearingPrice = tradePriceResolver.ResolvePrice(ask, bid);
			stats.maxClearingPrice = Mathf.Max(stats.maxClearingPrice, clearingPrice);
			stats.minClearingPrice = Mathf.Min(stats.minClearingPrice, clearingPrice);
			var tradeQuantity = Mathf.Min(bid.remainingQuantity, ask.remainingQuantity);
			if (tradeQuantity <= 0)
				Assert.IsTrue(tradeQuantity > 0);
			Assert.IsTrue(clearingPrice > 0);
    
			// =========== trade ============== 
			//bought quantity may be lower than trade quantity (buyer can buy less depending on clearing price)
			var boughtQuantity = Trade(rsc, clearingPrice, tradeQuantity, ref bid, ask);
			Assert.IsTrue(boughtQuantity >= 0);
    
			stats.moneyExchangedThisRound += clearingPrice * boughtQuantity;
			stats.goodsExchangedThisRound += boughtQuantity;
    
			ask.Accepted(clearingPrice, boughtQuantity);
			bid.Accepted(clearingPrice, boughtQuantity);
    
			Assert.IsTrue(ask.remainingQuantity >= 0);
			Assert.IsTrue(bid.remainingQuantity >= 0);
			
			if (ask.remainingQuantity == 0) askIdx++;
			if (bid.remainingQuantity == 0) bidIdx++;
		}

		foreach (var ask in asks)
		{
			ask.agent.UpdateSellerPriceBelief(ask, rsc);
			stats.minAskPrice = Mathf.Min(stats.minAskPrice, ask.offerPrice);
			stats.maxAskPrice = Mathf.Max(stats.maxAskPrice, ask.offerPrice);
		}

		foreach (var bid in bids)
		{
			bid.agent.UpdateBuyerPriceBelief(bid, rsc);
			stats.minBidPrice = Mathf.Min(stats.minBidPrice, bid.offerPrice);
			stats.maxBidPrice = Mathf.Max(stats.maxBidPrice, bid.offerPrice);
		}
		Assert.IsFalse(stats.goodsExchangedThisRound < 0);
		
		//at end of auction, if someone bid and someone else asked, goods exchanged should not be zero??
		var numBids = bids.Sum(item => item.offerQuantity);
		var numAsks = asks.Sum(item => item.offerQuantity);
		if (numBids > 0 && numAsks > 0 && stats.goodsExchangedThisRound == 0)
		{
			foreach (var ask in asks)
				Debug.Log("summary " + stats.goodsExchangedThisRound + " goods exchanged: " + rsc.name + " " + ask.agent.name + " ask: " + ask.offerQuantity.ToString("n2") + " for " + ask.offerPrice.ToString("c2"));
			foreach (var bid in bids)
				Debug.Log("summary " + stats.goodsExchangedThisRound + " goods exchanged: " + rsc.name + " " + bid.agent.name + " bid: " + bid.offerQuantity.ToString("n2") + " for " + bid.offerPrice.ToString("c2"));
		}
	}

	protected float OnlyBuyAffordable(Offer ask, ref Offer bid, float quantity, float price)
	{
		if (bid.agent.config.onlyBuyWhatsAffordable)
		{
			quantity = Mathf.Clamp((float)(int)(bid.agent.Cash/price), 0, quantity);
			bid.UpdateOffer(quantity);
			Debug.Log(bid.agent.name + " only buying " + quantity.ToString("n2") + " " + ask.commodityName +
			          " new bid " + bid.offerQuantity.ToString("n2") + " remaining " +
			          bid.remainingQuantity.ToString("n2"));
		}

		return quantity;
	}

	protected float Trade(ResourceController rsc, float clearingPrice, float tradeQuantity, ref Offer bid, Offer ask)
	{
		tradeQuantity = OnlyBuyAffordable(ask, ref bid, tradeQuantity, clearingPrice);
		if (tradeQuantity <= 0)
			return tradeQuantity;
		
		//transfer goods from seller to buyer
		auctionTracker.Transfer(ask.agent, bid.agent, rsc.name, tradeQuantity);
		//transfer cash from buyer to seller
		auctionTracker.Transfer(bid.agent, ask.agent, "Cash", tradeQuantity * clearingPrice);
		
		bid.agent.Buy(rsc.name, tradeQuantity, clearingPrice);
		ask.agent.Sell(rsc.name, tradeQuantity, clearingPrice);
		fiscalPolicy.CollectSalesTax(rsc.name, tradeQuantity, clearingPrice, bid.agent);

		Debug.Log("Trade(), " + auctionTracker.round + ", " + ask.agent.name + ", " + bid.agent.name + ", " + 
			rsc.name + ", " + tradeQuantity.ToString("n2") + ", " + clearingPrice.ToString("c2") +
			", " + ask.offerPrice.ToString("c2") + ", " + bid.offerPrice.ToString("c2"));
		return tradeQuantity;
	}
}

class XEvenResolution : TradeResolution
{
	public XEvenResolution(AuctionStats aStats, FiscalPolicy fp, OfferTable at, OfferTable bt) : base(aStats, fp, at, bt) {}

    protected override LoopState EndTrades(Offer ask, Offer bid)
    {
	    return LoopState.None;
    }
}

// the idea was match bids and asks by price, where bid price always above ask price
class OmisTradeResolution : TradeResolution
{
	public OmisTradeResolution(AuctionStats aStats, FiscalPolicy fp, OfferTable at, OfferTable bt) : base(aStats, fp, at, bt) {}
    protected override LoopState EndTrades(Offer ask, Offer bid)
    {
	    //skip bids until bid price is higher than ask price
	    if (ask.offerPrice > bid.offerPrice)
		    return LoopState.ContinueBids;
	    else
		    return LoopState.None;
    }
}
// from https://thomassimon.dev/ps/4
class SimonTradeResolution : TradeResolution
{
	public SimonTradeResolution(AuctionStats aStats, FiscalPolicy fp, OfferTable at, OfferTable bt) : base(aStats, fp, at, bt) {}
    protected override LoopState EndTrades(Offer ask, Offer bid)
    {
	    if (ask.offerPrice > bid.offerPrice + 0.1f)
		    return LoopState.Break;
	    else
		    return LoopState.None;
    }
}
