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
    protected Offers asks = new Offers();
    protected Offers bids = new Offers();
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
        starving = Food() <= 0;
        if (starving)
            DaysStarving++;
        else
            DaysStarving = 0;
        var dying = (DaysStarving >= config.maxDaysStarving);// && (outputName != "Food");

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
            if (!isConsumable(item.name)) //if not inputs
                continue;
            if (item.Quantity <= 0) //can't go below 0
                continue;
            float amount = 0f;
            // if (item.Quantity > 20)
            //     amountConsumed = 6;
            // else 
            if (item.Quantity > 10)
                amount = 3;
            else if (item.Quantity > 5)
                amount = 2;
            else 
                amount = 1;
            item.Decrease(amount);
        }
    }

    public override float Produce()
    {
        var item = inventory[outputName];
        var maxProduction = item.GetMaxProductionRate();
        //produce less if sold less
        // var numSoldLastRound = item.saleHistory[^1].quantity;
        // var smoothedProduction = Mathf.Round((numSoldLastRound + maxProduction) / 2f);
        // var numProduced = Mathf.Min(smoothedProduction, maxProduction);
        // item.Increase(numProduced);
        //don't make any if missing a recipe ingredient
        item.Increase(maxProduction);
        return maxProduction;
    }

    //decide what to bid/ask
    public override void Decide()
    {
        //decide how much to bid and ask (just think of them as buy and sell for now)
        //randomly pick an inventory check if it's buying or selling it has a better utility than others
        //until out of money or can't sell anymore or can't buy anymore
        asks.Clear();
        bids.Clear();
        PopulateOffersFromInventory();
        CreateOffersFromInventory();
    }

    private void printInventoryNiceness(InventoryItem selectedItem)
    {
        string msg_nice = " ";
        foreach (var item in inventory.Values)
        {
            if (item.name != selectedItem.name && item.name != outputName)
            {
                msg_nice += item.name + ": " + item.GetNiceness().ToString("f4") + " -- ";
            }
        }

        msg_nice += " offering " + selectedItem.name + ": " + selectedItem.GetNiceness().ToString("f4"); 
        // Debug.Log(auctionStats.round + " " + name + msg_nice);
    }
    protected virtual void PopulateOffersFromInventory()
    {
        foreach (var item in inventory.Values)
        {
            item.offersThisRound = 0;
            item.UpdateNiceness = true;
            item.CanOfferAdditionalThisRound = true;
        }

        var msg =
            $"{string.Join(",", inventory.Keys)}--{string.Join(",", inventory.Values.Select(item => item.Quantity))}";
        Debug.Log(auctionStats.round + " produces " + outputName + " offering? -- " + msg);

        //determine how much of each good to offer
        WatchDogTimer timer = new(10);
        for (float allocatedSpending = 0, i = 0;
             timer.IsRunning() && inventory.Values.Any(item => item.CanOfferAdditionalThisRound);
             i++)
        {
            var idx = UnityEngine.Random.Range(0, inventory.Count);
            var (itemName, item) = inventory.ElementAt(idx);
            var selling = !isConsumable(itemName);
            var itemPrice = item.GetPrice();

            //TODO refactor this into two functions (worthSelling and worthBuying)
            item.CanOfferAdditionalThisRound = (selling)
                ? (item.Quantity - item.offersThisRound) >= 1
                : ((Cash - allocatedSpending) / itemPrice) >= 1f;

            if (false == item.CanOfferAdditionalThisRound)
                continue;

            var niceness = item.GetNiceness();
            var worthOffering = false;

            if (selling)
                worthOffering = worthSelling(item);
            else
                worthOffering = worthBuying(item, ref allocatedSpending);

            if (!worthOffering)
                continue;

            timer.Reset();
            item.offersThisRound++;
            printInventoryNiceness(item);
        }

        var msg2 =
            $"{string.Join(",", inventory.Keys)}--{string.Join(",", inventory.Values.Select(item => item.Quantity))}";
        Debug.Log(auctionStats.round + " " + name + " debug " + CashString + " has " + msg2);
    }

    //sell if not the nicest (sell until equal to least owned, weighed by price)
    private bool worthSelling(InventoryItem item)
    {
        var niceness = item.GetNiceness();
        if (niceness == float.NegativeInfinity)
            return true;
        
        var itemName = item.name;
        var worthTheOffer = inventory.Values
            .Where(item => item.name != itemName && item.name != outputName)
            .Any(item => item.GetNiceness() >= niceness);
        
        return worthTheOffer;
    }
    //buy if the nicest option (buy least owned first, weighed by price)
    private bool worthBuying(InventoryItem item, ref float allocatedSpending)
    {
        var niceness = item.GetNiceness();
        if (niceness == float.PositiveInfinity)
            return true;
        
        var itemName = item.name;
        var worthTheOffer = inventory.Values
            .Where(item => item.name != itemName && item.name != outputName)
            .All(item => item.GetNiceness() <= niceness);
        
        if (worthTheOffer)
            allocatedSpending += item.GetPrice();
        
        return worthTheOffer;
    }

    protected void CreateOffersFromInventory()
    {
        //place bids and asks
        foreach (var (itemName, item) in inventory)
        {
            if (item.offersThisRound <= 0)
                continue;
            var price = item.GetPrice();
            var selling = !isConsumable(itemName);
            var offers = (selling) ? asks : bids;
            offers.Add(itemName, new Offer(itemName, price, item.offersThisRound, this));
            if (selling)
                Debug.Log(auctionStats.round + name + " offers " + itemName + " asking " + item.offersThisRound 
                          + " for " + price.ToString("c2")
                          + " has " + item.QuantityString + " market price: " + book[itemName].marketPriceString);
            else
                Debug.Log(auctionStats.round + name + " offers " + itemName + " bidding " + item.offersThisRound 
                          + " for " + price.ToString("c2")
                          + " has " + item.QuantityString + " market price: " + book[itemName].marketPriceString);
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