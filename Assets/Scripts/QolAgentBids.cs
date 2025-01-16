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
        {
            (numFoodToBid, remainingCash) = allocateFund(numFoodToBid, foodMarketPrice, Cash);

            if (numBatchInputToBid > 0)
            {
                (numBatchInputToBid, remainingCash) = allocateFund(numBatchInputToBid, inputBatchCost, remainingCash);
            }
        }

        //split remaining cash on both (evenly for now) -- start with least quantity
        //if any money left over
        //TODO what if only enough for one more? then don't split!?
        var leftover = 0f;
        var additionalInput = float.PositiveInfinity;
        var additionalFood = (Profession == "Food") ? 0 : float.PositiveInfinity;
        var cashForFood = remainingCash / 2;
        var cashForInputs = remainingCash / 2;

        if (foodItem.Quantity < minInputBatches) //bid on food first if less quantity
        {
            (additionalFood, leftover) = allocateFund(additionalFood, foodMarketPrice, cashForFood);
            cashForInputs = remainingCash - cashForFood + leftover;
            (additionalInput, leftover) = allocateFund(additionalInput, inputBatchCost, cashForInputs);
        }
        else //swap
        {
            (additionalInput, leftover) = allocateFund(additionalInput, inputBatchCost, cashForInputs);
            cashForFood = remainingCash - cashForInputs + leftover;
            (additionalFood, leftover) = allocateFund(additionalFood, foodMarketPrice, cashForFood);
        }
        numFoodToBid += additionalFood;
        numBatchInputToBid += additionalInput;

        //loop over each inventory item and update offersThisRound
        inventory["Food"].offersThisRound = numFoodToBid;
        //only buy missing inputs to get to numBatchInput
        //determine how many batches of each input in current inventory
        //add numBatchInput - item.numbatches + minbatch
        if (true)
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
            cashForInputs = numBatchInputToBid * inputBatchCost;
            FillInputBids(numBatchInputToBid, cashForInputs);
        }
    }

    protected void FillInputBids(float inputBatchCost, float allocatedFunds)
    {
        //get total cash equivalent of inputs, add to allocated funds
        var inventoryWorth = GetInventoryWorth();
        allocatedFunds += inventoryWorth;
        //get max batch affordable by this
        
        //foreach item: check if quantity exceeds this in batches supported
        //subtract excess in value from allocatedFunds
        //get max number of batches from updated allocatedFunds
        //foreach item: offersThisRound = (maxBatch * numNeeded) - Quantity;
        //track money needed for this, should be <= allocatedFunds
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
        Assert.IsTrue(daysToDeath > 0);
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