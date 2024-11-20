using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using System.Linq;
using UnityEngine.XR;
using System;
using Sirenix.Reflection.Editor;
using DG.Tweening;
using EconSim;
using UnityEditor;
using UnityEngine.UIElements;

public class QoLSimpleAgent : EconAgent
{
    //assumption: no recipes, all agents produce fixed amount when alive
    //if no food, die
    //quality of life depends on food wood tool
    //each agent makes 1 type of good
    //diminishing marginal utility with log(# good)
    //maximize utility per dollar spent

    //note: in the Thomas Simon approach, bids and asks occur randomly so the equilibrium point
    //will the different based on the order; which means it's not stable
    //but in the auction model all bids and asks are made separately, so how would that work? 
    //can't even use the auction model??
    Offers asks = new Offers();
    Offers bids = new Offers();
    public override void Init(SimulationConfig cfg, AuctionStats at, string b, float initStock, float maxstock)
    {
	    base.Init(cfg, at, b, initStock, maxstock);
    }
    //produce
    public override float Tick(Government gov, ref bool changedProfession, ref bool bankrupted, ref bool starving)
    {
        ConsumeGoods();
        //check if food=0
        if (inventory["Food"].Quantity <= 0)
        {
            Debug.Log(auctionStats.round + " " + name + " has died");
            Alive = false;
        }
        //die
        //birth conditions? enough food for 5 rounds?
        return 0;
    }

    public override float EvaluateHappiness()
    {
        return base.EvaluateHappiness();
    }

    public override void Sell(string commodity, float quantity, float price)
    {
        base.Sell(commodity, quantity, price);
    }

    public void ConsumeGoods()
    {
        foreach (var item in inventory.Values)
        {
            float amountConsumed = 0f;
            if (item.Quantity > 10)
                amountConsumed = 3;
            else if (item.Quantity > 5)
                amountConsumed = 2;
            else 
                amountConsumed = 1;
            item.Decrease(amountConsumed);
        }
    }

    public override float Produce()
    {
        var item = inventory[outputName];
        var numProduced = item.GetProductionRate();
        item.Increase(numProduced);
        return numProduced;
    }

    public override void Decide()
    {
        //decide how much to bid and ask (just think of them as buy and sell for now)
        //randomly pick an inventory check if it's buying or selling it has a better utility than others
        //until out of money or can't sell anymore or can't buy anymore
        asks.Clear();
        bids.Clear();
        bool canBuy = true;
        bool canSell = true;
        foreach (var item in inventory.Values)
        {
            item.offersThisRound = 0;
            item.canOfferAdditionalThisRound = true;
        }
        //determine how much of each good to offer
        int i = 0;
        while (inventory.Values.Any(item => item.canOfferAdditionalThisRound == true))
        {
            var idx = UnityEngine.Random.Range(0, inventory.Count);
            var (c, item) = inventory.ElementAt(idx);
            if (c == outputName)
                item.canOfferAdditionalThisRound = item.Quantity - item.offersThisRound >= 1;
            else
                item.canOfferAdditionalThisRound = ((Cash / book[c].marketPrice) - item.offersThisRound) > 1f;
            if (false == item.canOfferAdditionalThisRound)
                continue;
            
            var niceness = item.GetNiceness();
            var mostNice = inventory.Values
                .Where(item => item.name != c)
                .All(item => item.GetNiceness() <= niceness);
            foreach (var it in inventory.Values)
            {
                if (it.name != c)
                {
                    mostNice &= it.GetNiceness() <= niceness;
                }
            }
            if (mostNice)
            {
                item.offersThisRound++;
            }

            i++;
            if (i > 100)
                break;
        }

        //place bids and asks
        foreach (var (c, item) in inventory)
        {
            if (item.offersThisRound > 0)
            {
                if (c == outputName)
                    asks.Add(c, new Offer(c, item.GetPrice(), item.offersThisRound, this));
                else
                    bids.Add(c, new Offer(c, item.GetPrice(), item.offersThisRound, this));
            }
        }

    }

    public override Offers CreateAsks()
    {
        //ask only enough where utility matches others
        return asks;
    }

    public override Offers Consume(AuctionBook book)
    {
        return bids;
    }
    //consume
    //create asks
    //create bids
}