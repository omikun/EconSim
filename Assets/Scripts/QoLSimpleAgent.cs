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
        if (Alive == false)
            return 0;
        // starving = inventory.Values.Any(item => item.Quantity <= 5);
        starving = Food() <= 5;
        var dying = (Food() <= 0);

        if (config.changeProfession && dying)
        {
            bankrupted = Cash < book["Food"].marketPrice;
            ChangeProfession(gov, bankrupted);
            dying = false;
        }
        // if ( inventory.Values.Any(item => item.Quantity <= 0) )
        if ( dying )
        {
            var quants = inventory.Values.Select(item => item.Quantity);
            //var msg = string.Join(",", quants);
            var msg = $"{string.Join(",", inventory.Keys)}--{string.Join(",", inventory.Values.Select(item => item.Quantity))}";
            //var msg = string.Join(",", inventory.SelectMany(t => t.Key, (t, i) => t.Key + ", " + t.Value.Quantity ));

            Debug.Log(auctionStats.round + " " + name + " has died with " + msg);
            Alive = false;
            outputName = "Dead";
        }
        //die
        //birth conditions? enough food for 5 rounds?
        return 0;
    }

    public override float EvaluateHappiness()
    {
        var x = (inventory.Values.Sum(item => item.Quantity));
        return x / (x + 20);
    }

    public override void ConsumeGoods()
    {
        //consume food
        //unless consuming food to produce
        //consume all other inputs proportional to output
        foreach (var item in inventory.Values)
        {
            if (!inRecipe(item.name)) //if not inputs
                continue;
            if (item.Quantity <= 0) //can't go below 0
                continue;
            float amountConsumed = 0f;
            // if (item.Quantity > 20)
            //     amountConsumed = 6;
            // else 
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
        var maxProduction = item.GetProductionRate();
        //produce less if sold less
        // var numSoldLastRound = item.saleHistory[^1].quantity;
        // var smoothedProduction = Mathf.Round((numSoldLastRound + maxProduction) / 2f);
        // var numProduced = Mathf.Min(smoothedProduction, maxProduction);
        // item.Increase(numProduced);
        //don't make any if missing a recipe ingredient
        var produceNothing = book[outputName].recipe.Keys.Any(item => inventory[item].Quantity <= 0);
        if (produceNothing)
            return 0;
        item.Increase(maxProduction);
        return maxProduction;
    }

    public override void Decide()
    {
        //decide how much to bid and ask (just think of them as buy and sell for now)
        //randomly pick an inventory check if it's buying or selling it has a better utility than others
        //until out of money or can't sell anymore or can't buy anymore
        asks.Clear();
        bids.Clear();
        foreach (var item in inventory.Values)
        {
            item.offersThisRound = 0;
            item.canOfferAdditionalThisRound = true;
        }
        
        var msg = $"{string.Join(",", inventory.Keys)}--{string.Join(",", inventory.Values.Select(item => item.Quantity))}";
        Debug.Log(auctionStats.round + " produces " + outputName + " offering? -- " + msg);
        
        //determine how much of each good to offer
        for (float allocatedSpending = 0, i = 0; 
             i < 100 && inventory.Values.Any(item => item.canOfferAdditionalThisRound); 
             i++)
        {
            var idx = UnityEngine.Random.Range(0, inventory.Count);
            var (itemName, item) = inventory.ElementAt(idx);
            var selling = isSellable(itemName);
            var itemPrice = item.GetPrice();
            
            item.canOfferAdditionalThisRound = (selling)
                ? (item.Quantity - item.offersThisRound) >= 1
                : item.canOfferAdditionalThisRound = ((Cash - allocatedSpending) / itemPrice) > 1f;
            
            if (false == item.canOfferAdditionalThisRound)
                continue;
            
            var niceness = item.GetNiceness();
            //sell if not the nicest (sell until equal to least owned, weighed by price)
            //buy if the nicest option (buy least owned first, weighed by price)
            var worthTheOffer = (true == selling)
                ? inventory.Values
                    .Where(item => item.name != itemName && item.name != outputName)
                    .Any(item => item.GetNiceness() >= niceness)
                : inventory.Values
                    .Where(item => item.name != itemName && item.name != outputName)
                    .All(item => item.GetNiceness() <= niceness);
            
            if (!worthTheOffer)
                continue;
            
            item.offersThisRound++;
            if (false == selling)
            {
                // Debug.Log(auctionStats.round + " " + CashString + " " + name + " has " + item.QuantityString
                //     + item.offersThisRoundString + " with niceness " + niceness.ToString("n5"));
                allocatedSpending += itemPrice;
            }
        }

            var msg2 = $"{string.Join(",", inventory.Keys)}--{string.Join(",", inventory.Values.Select(item => item.Quantity))}";
            Debug.Log(auctionStats.round + " debug " + CashString + " " + name + " has " + msg2);
        //place bids and asks
        foreach (var (itemName, item) in inventory)
        {
            Debug.Log(auctionStats.round + " " + CashString + " " + name + " has " +
                      item.QuantityString + " market price: " + book[itemName].marketPriceString + " offers " + item.offersThisRound);

            if (item.offersThisRound <= 0)
                continue;
            var price = item.GetPrice();
            var selling = itemName == outputName;
            var offers = (selling) ? asks : bids;
            offers.Add(itemName, new Offer(itemName, price, item.offersThisRound, this));
            if (selling)
                Debug.Log(auctionStats.round + " " + name + " asking " + item.offersThisRound + " " + itemName + " for " + price.ToString("c2"));
            else
                Debug.Log(auctionStats.round + " " + name + " bidding " + item.offersThisRound + " " + itemName + " for " + price.ToString("c2"));
        }
    }

    public override Offers CreateAsks()
    {
        //ask only enough where utility matches others
        return asks;
    }

    public override Offers CreateBids(AuctionBook book)
    {
        return bids;
    }
}