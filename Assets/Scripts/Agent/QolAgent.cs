using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using System.Linq;

//medium agent
public partial class QolAgent : EconAgent
{
    protected float numBatchesConsumed = 0;
    private int numRoundsSinceLastBirth = 0;
    private int numRoundsPlanFamily = 0;
    protected Offers asks = new Offers();
    protected Offers bids = new Offers();

    public override void Decide()
    {
        decideProduction();
        //decide how much to bid and ask (just think of them as buy and sell for now)
        //randomly pick an inventory check if it's buying or selling it has a better utility than others
        //until out of money or can't sell anymore or can't buy anymore
        asks.Clear();
        bids.Clear();
        PopulateOffersFromInventory(); //called by AuctionHouse.UpdateAgentTable
        CreateOffersFromInventory();
    }
    protected void decideProduction()
    {
        if (outputName != "Labor" && book.ContainsKey(outputName) == true)
        {
            var rsc = book[outputName];
            var stock = inventory[outputName];
            var numBatches = NumBatchesProduceable(rsc, stock);
            // var minInputBatches = productionStrategy.NumBatchesProduceable(rsc, inventory[outputName]);
            var numProduced = Produce(numBatches);
            numUnitsProduced.ThisRound = numProduced;
            numBatchesProduced.ThisRound = numBatches;
            ConsumeGoods(numBatches);
            // ConsumeGoods(0);
            Debug.Log(auctionStats.round + " " + name + " produced " + numProduced + " " + rsc.name);
        }
        else
        {
            ConsumeGoods(0);
        }
        // var numProduced2 = productionStrategy.Produce();
        // Assert.AreEqual(numProduced, numProduced2);
    }

    public float NumBatchesProduceable(ResourceController rsc, InventoryItem outputItem)
    {
		float numBatches = float.MaxValue;
		foreach (var com in rsc.recipe.Keys)
		{
			var numProduceableWithCom = inventory[com].NumProduceable(rsc);
			numBatches = Mathf.Min(numProduceableWithCom, numBatches);
			Debug.Log(auctionStats.round + " " + name 
				+ " can produce " + numBatches + " batches of " + outputItem.name
				+ " with " + inventory[com].Quantity + "/" + rsc.recipe[com] + " " + com);
		}
        // numBatches = rsc.recipe.Keys.Min(com => inventory[com].NumProduceable(rsc));
        var numEmployees = (Employees == null) ? 0 : Employees.Count;
        var maxBatchRate = outputItem.GetMaxBatchRate() + numEmployees;
	    numBatches = Mathf.Min(maxBatchRate, numBatches);
        
	    var realProductionRate = outputItem.GetMaxProductionRate(numBatches);
	    var realBatchRate = Mathf.Ceil(realProductionRate / outputItem.ProductionPerBatch);
		Debug.Log(auctionStats.round + " " + name
		          + " produceable " + realBatchRate + " batches of " + outputItem.name);

        return realBatchRate;
    }
    
    protected void CreateOffersFromInventory()
    {
        //place bids and asks
        foreach (var (itemName, item) in inventory)
        {
            if (item.offersThisRound <= 0)
                continue;
            
            var selling = !isConsumable(itemName);
            if (itemName == "Labor")
                selling = Profession == "Unemployed";
            
            var offers = (selling) ? asks : bids;
            
            // item.CalculateSellPrice(item.offersThisRound);
            var price = item.GetPrice();
            offers.Add(itemName, new Offer(itemName, price, item.offersThisRound, this));
            item.offersThisRound = 0;
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

    public void ConsumeGoods(float numBatches)
    {
        //determine number of batches worth of inputs to consume
        //iterate over each input and find 
        //else consume at least n food
        foreach (var item in inventory.Values)
        {
            float amountConsumed = 0f;
            if (item.name == "Food" && "Food" != outputName)
            {
                if (item.Quantity > 20)
                    amountConsumed = 4;
                else 
                if (item.Quantity > 10)
                    amountConsumed = 3;
                else if (item.Quantity > 5)
                    amountConsumed = 2;
                else 
                    amountConsumed = 1;
            } else if (!inRecipe(item.name)) //if not inputs
                continue;
            else if (item.Quantity <= 0) //can't go below 0
                continue;

            if (numBatches > 0 && inRecipe(item.name))
            {
                var recipe = book[outputName].recipe;
                float toBeConsumedByBatch = recipe[item.name] * numBatches;
                float consumedByBatch = 0;
                float breakdown = book[item.name].breakdown_chance;
                for (int i = 0; i < toBeConsumedByBatch; i++)
                {
                    if (UnityEngine.Random.value <= breakdown)
                    {
                        consumedByBatch++;
                    }
                }
                amountConsumed = Mathf.Max(amountConsumed, consumedByBatch);
            }
            
            amountConsumed = Mathf.Min(item.Quantity, amountConsumed);
            Debug.Log(auctionStats.round + " " + name + " has " + item.Quantity + " " + item.name + " remaining, consumed " + amountConsumed + " " + item.name);
            item.Decrease(amountConsumed);
            Assert.IsTrue(item.Quantity >= 0);
        }
    }
    public float Produce(float numBatches)
    {
        if (outputName == "Labor")
            return 0;
        
        // return productionStrategy.Produce();
        var output = inventory[outputName];
        var numProducedTemp= output.GetMaxProductionRate(numBatches); //total
        
        output.Increase(numProducedTemp);
        Debug.Log(auctionStats.round + " " + name + " produced " + numProducedTemp + " " + outputName);
        return numBatches * (NumEmployees + 1);
        
        //determine how much is actually produced AND how much was consumed in the process
        float numBatchesProduceable = numBatches;
        Dictionary<string, float> consumed = new();
        
        foreach (var (com, numNeeded) in book[outputName].recipe)
        {
            var bdChance = book[com].breakdown_chance;
            if (book[com].breakdown_chance == 1)
            {
                // For 100% breakdown chance, we can only produce as many batches as our limiting input
                var maxBatchesByCom = inventory[com].Quantity / numNeeded;
                numBatchesProduceable = Mathf.Min(numBatchesProduceable, maxBatchesByCom);
                consumed[com] = numNeeded * numBatchesProduceable;
            }
            else
            {
                // For partial breakdown chance, simulate each batch
                int nbpByCom = 0;
                consumed[com] = 0;
                float remainingInventory = inventory[com].Quantity;
                
                while (nbpByCom < numBatchesProduceable && remainingInventory >= numNeeded)
                {
                    if (UnityEngine.Random.value < bdChance)
                        consumed[com] += numNeeded;
                    
                    nbpByCom++;
                }

                numBatchesProduceable = Mathf.Min(numBatchesProduceable, nbpByCom);
            }
        }
        
        foreach (var (com, numConsumed) in consumed)
        {
            inventory[com].Decrease(numConsumed);
        }
        
        var numProduced = numBatchesProduceable * output.ProductionPerBatch;
        var msg = "";
        foreach (var (com, numConsumed) in consumed)
            msg += "  " + numConsumed + " " + com + "\n";
        Debug.Log(auctionStats.round + " " + name + " produced " + numProduced + " " + outputName 
                  + " from " + numBatchesProduceable + " batches"
                  + "\nConsumed:" + msg);
        //produce less if sold less
        // var numSoldLastRound = item.saleHistory[^1].quantity;
        // var smoothedProduction = Mathf.Round((numSoldLastRound + maxProduction) / 2f);
        // var numProduced = Mathf.Min(smoothedProduction, maxProduction);
        // item.Increase(numProduced);
        //don't make any if missing a recipe ingredient
        output.Increase(numProduced);
        return numProduced;
    }
    public bool IsDying(ref bool starving)
    {
        // starving = inventory.Values.Any(item => item.Quantity <= 5);
        var farmerStarving = numUnitsProduced.ThisRound == 0 && outputName == "Food" && FoodInv() <= 0;
        var nonFarmerstarving = FoodInv() <= 0 && outputName != "Food";
        starving = farmerStarving || nonFarmerstarving;
        if (starving)
            DaysStarving++;
        else
            DaysStarving = 0;
        var nonFarmerDying = (outputName != "Food" && DaysStarving >= config.maxDaysAliveWhileStarving);
        var farmerDying = (outputName == "Food" && DaysStarving >= 2*config.maxDaysAliveWhileStarving);
        return nonFarmerDying || farmerDying;
    }
    public override float Tick(Government gov, ref bool changedProfession, ref bool bankrupted, ref bool starving)
    {
        if (Alive == false)
            return 0;

        if (Employees != null)
        {
            var pay = Income.Last() / NumEmployees * .4f;
            pay = Math.Min(book["Food"].marketPrice, pay);
            
            foreach (var (employee,wage) in Employees)
            {
                employee.Collect(pay);
                Cash -= pay;
                Debug.Log(auctionStats.round + " " + name + " paid " + pay + " to " + employee.name);
            }
        }
        
        gov.Welfare(this);
        
        var dying = IsDying(ref starving);

        if (config.changeProfession && starving)
        {
            bankrupted = Cash < book["Food"].marketPrice;
            ChangeProfession(gov, bankrupted);
            dying = false;
        }
        // if ( inventory.Values.Any(item => item.Quantity <= 0) )
        if ( dying )
        {
            HandleDeath();
            return 0;
        } 
        
        //chance of reproducing
        Debug.Log(auctionStats.round + " " + name + " since last birth " + numRoundsSinceLastBirth +  " fmaily planning " + numRoundsPlanFamily);
        if (config.SpawnNewAgent
            && numRoundsSinceLastBirth > config.MinDaysSinceLastOffspring 
            && numRoundsPlanFamily > config.MinDaysHaveCash)
        {
            if (UnityEngine.Random.Range(0, 1f) > .1f)
                return 0;
            numRoundsSinceLastBirth = 0;

            var foodPrice = Mathf.Max(4, book["Food"].marketPrice * 4);
            if (Cash >= foodPrice)
            {
                var inheritance = foodPrice / 4;
                Cash -= inheritance;
                return inheritance;
            }

            return 1;
        }
        else
        {
            numRoundsPlanFamily = (Cash > 0) 
                ? numRoundsPlanFamily + 1 : 0;
            numRoundsSinceLastBirth++;
        }
        
        return 0;
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