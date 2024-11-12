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
		offers.Sort((x, y) => y.offerPrice.CompareTo(y.offerPrice)); //dec
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
    public virtual void ResolveOffers(ResourceController rsc, ref float moneyExchangedThisRound, ref float goodsExchangedThisRound)
	{
		var asks = askTable[rsc.name];
		var bids = bidTable[rsc.name];
    
		asks.Shuffle();
		bids.Shuffle();

	    askSorter.SortOffer(ref asks);
	    bidSorter.SortOffer(ref bids);
    
		int idx = 0;
		foreach (var ask in asks)
		{
			ask.agent.inventory[rsc.name].askOrder = idx;
			idx++;
		}
		
		idx = 0;
		foreach (var bid in bids)
		{
			bid.agent.inventory[rsc.name].bidOrder = idx;
			idx++;
		}
		moneyExchangedThisRound = 0;
		goodsExchangedThisRound = 0;
    
		int askIdx = 0;
		int bidIdx = 0;
    
		while (askIdx < asks.Count && bidIdx < bids.Count)
		{
			var ask = asks[askIdx];
			var bid = bids[bidIdx];

			var cond = EndTrades(ask, bid);
			if (cond == LoopState.Break)
				break;
			else if (cond == LoopState.ContinueAsks)
			{
				askIdx++;
				continue;
			}
			else if (cond == LoopState.ContinueBids)
			{
				bidIdx++;
				continue;
			}
    
			//var clearingPrice = ResolveClearingPrice(ask, bid);
			var clearingPrice = tradePriceResolver.ResolvePrice(ask, bid);
			var tradeQuantity = Mathf.Min(bid.remainingQuantity, ask.remainingQuantity);
			//var tradeQuantity = bid.UpdateOffer((ask.remainingQuantity));
			if (tradeQuantity <= 0)
				Assert.IsTrue(tradeQuantity > 0);
			Assert.IsTrue(clearingPrice > 0);
    
			// =========== trade ============== 
			//bought quantity may be lower than trade quantity (buyer can buy less depending on clearing price)
			var boughtQuantity = Trade(rsc, clearingPrice, tradeQuantity, ref bid, ask);
    
			moneyExchangedThisRound += clearingPrice * boughtQuantity;
			goodsExchangedThisRound += boughtQuantity;
    
			//this is necessary for price belief updates after the big loop
			ask.Accepted(clearingPrice, boughtQuantity);
			bid.Accepted(clearingPrice, boughtQuantity);
    
			Assert.IsTrue(ask.remainingQuantity >= 0);
			Assert.IsTrue(bid.remainingQuantity >= 0);
			//go to next ask/bid if fullfilled
			if (ask.remainingQuantity == 0)
			{
				ask.agent.UpdateSellerPriceBelief(ask, rsc);
				askIdx++;
			}

			if (bid.remainingQuantity == 0)
			{
				bid.agent.UpdateBuyerPriceBelief(bid, rsc);	
				bidIdx++;
			}
		}

		while (askIdx < asks.Count)
		{
			var ask = asks[askIdx];
			ask.agent.UpdateSellerPriceBelief(ask, rsc);
			askIdx++;
		}

		while (bidIdx < bids.Count)
		{
			var bid = bids[bidIdx];
			bid.agent.UpdateBuyerPriceBelief(bid, rsc);
			bidIdx++;
		}
		Assert.IsFalse(goodsExchangedThisRound < 0);
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
		var boughtQuantity = bid.agent.Buy(rsc.name, tradeQuantity, clearingPrice);
		ask.agent.Sell(rsc.name, boughtQuantity, clearingPrice);
		fiscalPolicy.AddSalesTax(rsc.name, boughtQuantity, clearingPrice, bid.agent);

		Debug.Log("Trade(), " + auctionTracker.round + ", " + ask.agent.name + ", " + bid.agent.name + ", " + 
			rsc.name + ", " + boughtQuantity.ToString("n2") + ", " + clearingPrice.ToString("c2") +
			", " + ask.offerPrice.ToString("c2") + ", " + bid.offerPrice.ToString("c2"));
		// Debug.Log(auctionTracker.round + ": " + ask.agent.name 
		// 	+ " ask " + ask.remainingQuantity.ToString("n2") + "x" + ask.offerPrice.ToString("c2")
		// 	+ " | " + bid.agent.name 
		// 	+ " bid: " + bid.remainingQuantity.ToString("n2") + "x" + bid.offerPrice.ToString("c2")
		// 	+ " -- " + rsc.name + " offer quantity: " + tradeQuantity.ToString("n2") 
		// 	+ " bought quantity: " + boughtQuantity.ToString("n2"));
		return boughtQuantity;
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
	    if (ask.offerPrice > bid.offerPrice)
		    return LoopState.Break;
	    else
		    return LoopState.None;
    }
}
