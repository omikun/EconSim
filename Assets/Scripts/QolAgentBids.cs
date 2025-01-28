using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;

public partial class UserAgent 
{
    //only buys essential goods and inputs
    //  - essential goods are food or other goods in the future
    //  - inputs are future outputs
    //  - outputs are potential cash
    //  - goals:
    //    - MUST have food or die, so strong buy pressure when low on essentials
    //    - would like to have some inputs,
    //    - less pressure to buy inputs if have outputs / lots money
    //  - allocate available cash between essentials and inputs
    //  - decide how much cash to spend
    //    - already done in previous step?
    //  - split cash for inputs into each input
    protected override void PopulateOffersFromInventory()
    {
        foreach (var item in inventory.Values)
        {
            item.offersThisRound = 0;
            item.UpdateNiceness = true;
            item.CanOfferAdditionalThisRound = true;
        }

        if (outputName == "Metal")
            Debug.Log("Metal populateoffersfrominventory");
        float minQuant = 3f;
        // float outputPressure = buyOutputPressure(minQuant);
        float numBatchInputToBid = minBatchInputToBid();

        var numFoodToBid = minFoodToBid();
        var foodItem = inventory["Food"];
        var foodMarketPrice = book["Food"].marketPrice;
        var foodCost = numFoodToBid * foodMarketPrice;
        var remainingCash = 0f;
        var inputBatchCost = GetInputBatchCost();
        var output = book[outputName];
        var minInputBatches = productionStrategy.NumBatchesProduceable(output, foodItem);
        
        //allocate fund for min food, then min inputs for low cash situation
        if (false)
        {
            (numFoodToBid, remainingCash) = allocateFund(numFoodToBid, foodMarketPrice, Cash);

            if (numBatchInputToBid > 0)
            {
                (numBatchInputToBid, remainingCash) = allocateFund(numBatchInputToBid, inputBatchCost, remainingCash);
            }
        }
        else
        {
            numFoodToBid = 0;
            numBatchInputToBid = 0;
            remainingCash = Cash;
        }

        if (foodItem.Quantity < 2f) //bid on food first if less quantity
        {
            bool keepGoing = true;
            while (keepGoing)
            {
                keepGoing = false;
                if (remainingCash >= inputBatchCost && inputBatchCost > 0)
                {
                    numBatchInputToBid++;
                    remainingCash -= inputBatchCost;
                    keepGoing = true;
                }

                if (remainingCash >= foodMarketPrice)
                {
                    numFoodToBid++;
                    remainingCash -= foodMarketPrice;
                    keepGoing = true;
                }
            }
        }
        else //swap
        {
            bool keepGoing = true;
            while (keepGoing)
            {
                keepGoing = false;
                if (remainingCash >= foodMarketPrice)
                {
                    numFoodToBid++;
                    remainingCash -= foodMarketPrice;
                    keepGoing = true;
                }

                if (remainingCash >= inputBatchCost && inputBatchCost > 0)
                {
                    numBatchInputToBid++;
                    remainingCash -= inputBatchCost;
                    keepGoing = true;
                }
            }
        }

        //for cases where can't afford to buy enough inputs this round, ends up spending all money to food
        //ends up never enough to produce, always broke
        if (minInputBatches == 0 && numBatchInputToBid == 0)
        {
            numFoodToBid = 0;
        }
        //loop over each inventory item and update offersThisRound
        inventory["Food"].offersThisRound = numFoodToBid;
        //only buy missing inputs to get to numBatchInput
        //determine how many batches of each input in current inventory
        //add numBatchInput - item.numbatches + minbatch
        if (false)
        {
            foreach (var (com, numNeeded) in output.recipe)
            {
                var numBatches = Mathf.Floor(inventory[com].NumProduceable(output));
                var additionalBatches = numBatchInputToBid - numBatches + minInputBatches;
                additionalBatches = Mathf.Max(0, additionalBatches);
                inventory[com].offersThisRound = additionalBatches * numNeeded;
            }

        }
        else
        {
            var cashForInputs = numBatchInputToBid * inputBatchCost;
            FillInputBids(cashForInputs);
        }
    }

    //taking current inventory into account, create bids to get even batches
    protected void FillInputBids(float allocatedFunds)
    {
        var _recipe = new Recipe();
        foreach (var (k,v) in book[outputName].recipe)
            _recipe.Add(k,v);
        bool recompute;

        if (_recipe.Count == 0)
            return;
        
        do {
            float cashEquivalent = 0;
            float batchCost = 0;
            foreach (var (input, amount) in _recipe)
            {
                float priceOfGood = book[input].marketPrice;
                cashEquivalent += priceOfGood * inventory[input].Quantity;
                batchCost += priceOfGood * amount;
            }
            float totalCashEquivalent = cashEquivalent + allocatedFunds;
            float targetBatch = Mathf.Floor(totalCashEquivalent / batchCost);

            foreach (var com in book[outputName].recipe.Keys)
            {
                inventory[com].offersThisRound = 0;
            }

            recompute = false;
            foreach (var (input, amount) in _recipe.ToList()) {
                float amountNeeded = targetBatch * amount - inventory[input].Quantity;
                if (amountNeeded < 0) {     //more than needed, can discard and recompute w/o
                    recompute = true;
                    _recipe.Remove(input);
                    continue;
                }
                inventory[input].offersThisRound = amountNeeded;
            }
        } while (recompute);
    }

    //returns remaining fund and num bids that can be allocated given initial fund
    private (float, float) allocateFund(float numBids, float unitPrice, float fund)
    {
        var cost = numBids * unitPrice;
        if (numBids == 0 || unitPrice == 0 || fund < unitPrice)
            return (0f, fund);
        
        if (fund < cost)
        {
            numBids = (int)(fund / unitPrice);
            cost = numBids * unitPrice;
        }

        var remainingFund = fund - cost;
        return (numBids, remainingFund);
    }

    private float GetInventoryWorth()
    {
        //get numNeeded of each input for batch
        var rsc = book[outputName];
        var totalCost = rsc.recipe.Keys.Sum(com => inventory[com].Quantity * book[com].marketPrice);
        return totalCost;
    }
    private float GetInputBatchCost()
    {
        //get numNeeded of each input for batch
        var rsc = book[outputName];
        var totalCost = 0f;
        foreach (var com in rsc.recipe.Keys)
        {
            var numNeeded = rsc.recipe[com];
            var cost = inventory[com].GetPrice();
            totalCost += numNeeded * cost;
        }

        return totalCost;
    }

    //if 1 day to death, 0 food, need to buy at least 1 food
    //if 1 day to death, 0 food and 0 output, need to buy 2 food
    private float minFoodToBid()
    {
        if (outputName == "Food")
            return 0f;

        int daysToDeath = config.maxDaysStarving - DaysStarving;
        Assert.IsTrue(daysToDeath >= 0);
        var numFood =  (int)inventory["Food"].Quantity;
        float minFoodToBuy = (numFood <= 2) ? 1 : 0;
        if (daysToDeath == 1)
        {
            var foodToBuy = Mathf.Min(DaysStarving, 1);
            if (numFood == 0)
                minFoodToBuy = foodToBuy;
            if (numFood <= 1 && foodEquivalent.GetOutputFood() < 1)
                minFoodToBuy = foodToBuy;
        }

        return minFoodToBuy;
    }
    //find minimum number of batches of inputs to buy to keep having money
    //if low on cash and inputs, buy at least 1 batch of inputs
    private float minBatchInputToBid()
    {
        float buyPressureInput = 0f;
        var inputFood = foodEquivalent.GetInputFood();
        var outputFood = 0f;//foodEquivalent.GetOutputFood();
        return (inputFood == 0 && outputFood == 0) ? 1 : 0;
    }
    private float inputPressure(float minQuant)
    {
        var buyPressureInput = 0f;
        var recipe = book[outputName].recipe;
        foreach (var com in recipe.Keys)
        {
            Assert.IsTrue(com != "Food");
            float quant = inventory[com].Quantity;
            if (quant < minQuant)
                buyPressureInput += (minQuant - quant) / minQuant;
        }
        return buyPressureInput;
    }

    //TODO what if output is food?
    private float buyOutputPressure(float minQuant)
    {
        var buyPressureOutput = 0f;
        float numOutputs = inventory[outputName].Quantity;
        if (numOutputs < minQuant)
            buyPressureOutput = (minQuant - numOutputs)/ minQuant;
        return buyPressureOutput;
    }
}