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

public partial class UserAgent : QolAgent
{
    public override void Decide()
    {
        decideProduction();
        //decide how much to bid and ask (just think of them as buy and sell for now)
        //randomly pick an inventory check if it's buying or selling it has a better utility than others
        //until out of money or can't sell anymore or can't buy anymore
        asks.Clear();
        bids.Clear();
        // PopulateOffersFromInventory(); //called by AuctionHouse.UpdateAgentTable
        base.CreateOffersFromInventory();
    }
    public void UserTriggeredPopulateOffersFromInventory()
    {
        PopulateOffersFromInventory();
        PrePopulateSellingOffers();
    }
    // protected override void PopulateOffersFromInventory()
    protected void PrePopulateSellingOffers()
    {
        foreach (var (com, item) in inventory)
        {
            var selling = !isConsumable(item.name);
            if (selling)
                item.offersThisRound = item.Quantity;
        }
        return;
        var output = inventory[outputName];
        string msg = auctionStats.round + " " + name 
                     + " cash: " + CashString
                     + " output: " + output.Quantity + " " + output.name + " " + output.GetPrice();
        foreach (var (com, item) in inventory)
        {
            if (com != outputName)
            {
                msg += " " + item.Quantity + " " + com + item.GetPrice();
            }
        }
        Console.WriteLine(msg);
        foreach (var (com, item) in inventory)
        {
            if (isConsumable(com))
                Console.WriteLine("#bid " + com + ":");
            else
                Console.WriteLine("#ask " + com + ":");
            string numOffers = Console.ReadLine();
            item.offersThisRound = int.Parse(numOffers);
        }
    }
}
