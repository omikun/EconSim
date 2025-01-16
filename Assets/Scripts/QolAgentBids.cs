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
        float output = buyOutputPressure(minQuant);
        float numBatchInput = minBatchInput();
        
        //allocate fund for min food, then min inputs
        var numFood = minFood();
        var foodPrice = book["Food"].marketPrice;
        var foodCost = numFood * foodPrice;
        var remainingCash = 0f;
        (numFood, remainingCash) = allocateFund(numFood, foodPrice, Cash);
        var inputBatchCost = GetInputBatchCost();
        
        if (numBatchInput > 0)
        {
            (numBatchInput, remainingCash) = allocateFund(numBatchInput, inputBatchCost, remainingCash);
        }
        //split remaining cash on both (evenly for now?)
        var cashForFood = remainingCash / 2;
        var cashForInputs = remainingCash - cashForFood;
        var leftover = 0f;
        var additionalFood = (Profession == "Food") ? 0 : float.PositiveInfinity;
        (additionalFood, leftover) = allocateFund(additionalFood, foodPrice, cashForFood);
        
        cashForInputs += leftover;
        var additionalInput = float.PositiveInfinity;
        (additionalInput, cashForInputs) = allocateFund(additionalInput, inputBatchCost, cashForInputs);

        numFood += additionalFood;
        numBatchInput += additionalInput;
        
        //loop over each inventory item and update offersThisRound
        inventory["Food"].offersThisRound = numFood;
        foreach (var (com, numNeeded) in book[outputName].recipe)
        {
            inventory[com].offersThisRound = numBatchInput * numNeeded;
        }
    }

    //returns remaining fund and num bids that can be allocated given initial fund
    private (float, float) allocateFund(float numBids, float unitPrice, float fund)
    {
        var cost = numBids * unitPrice;
        if (fund < cost)
        {
            numBids = (int)(fund / unitPrice);
            cost = numBids * unitPrice;
        }

        var remainingFund = fund - cost;
        return (numBids, remainingFund);
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
    private float minFood()
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
    private float minBatchInput()
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