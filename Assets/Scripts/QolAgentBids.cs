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

        var reason = "";

        if (name == "agent10")
            Debug.Log("Agent10 bid logic");
        if (outputName == "Metal")
            Debug.Log("Metal populateoffersfrominventory");
        float minQuant = 3f;
        // float outputPressure = buyOutputPressure(minQuant);
        float numBatchInputToBid = 0;
        var inputFood = foodEquivalent.GetInputFood();
        var outputFood = foodEquivalent.GetOutputFood();

        var numFoodToBid = 0;
        var foodItem = inventory["Food"];
        var foodMarketPrice = book["Food"].marketPrice;
        var inputBatchCost = GetInputBatchCost();
        var output = book[outputName];
        var inputCashEquivalent = inputInventoryCashEquivalent(output.recipe);
        var minInputBatches = productionStrategy.NumBatchesProduceable(output, foodItem);
        
        var remainingCash = Cash;
        var remainingInputCash = remainingCash + inputCashEquivalent;
        //if cash can't cover 1 inputBatch (inventory may have some stuff), borrow enough money to pay for cash
        //need money equivalent of inputs that contributes towards one batch (say 3 wood 0 tool, only 2 of those woods contribute to a batch)
        //if more than 1 batch in input inventory, this condition is void
        DecideAndBorrow(ref remainingCash, inputBatchCost, foodMarketPrice);

        if (outputName == "Food")
        {
            bool keepGoing = true;
            while (keepGoing)
            {
                keepGoing = false;
                if (remainingInputCash >= inputBatchCost && inputBatchCost > 0)
                {
                    numBatchInputToBid++;
                    remainingCash -= inputBatchCost;
                    remainingInputCash -= inputBatchCost;
                    keepGoing = true;
                }
            }

            reason += " is farmer ";
        }
        //buy input first if no input and no output
        //else buy food first
        else if (foodItem.Quantity > 1f) //bid on input first if have enough food
        {
            reason += " >1 food ";
            bool keepGoing = true;
            while (keepGoing)
            {
                keepGoing = false;
                if (remainingInputCash >= inputBatchCost && inputBatchCost > 0)
                {
                    numBatchInputToBid++;
                    remainingCash -= inputBatchCost;
                    remainingInputCash -= inputBatchCost;
                    keepGoing = true;
                }

                if (remainingCash >= foodMarketPrice)
                {
                    numFoodToBid++;
                    remainingCash -= foodMarketPrice;
                    remainingInputCash -= foodMarketPrice;
                    keepGoing = true;
                }
            }
        }
        else //bid on food first if less quantity
        {
            reason += " <=1 food ";
            bool keepGoing = true;
            while (keepGoing)
            {
                keepGoing = false;
                if (remainingCash >= foodMarketPrice)
                {
                    numFoodToBid++;
                    remainingCash -= foodMarketPrice;
                    remainingInputCash -= foodMarketPrice;
                    keepGoing = true;
                }

                if (remainingInputCash >= inputBatchCost && inputBatchCost > 0)
                {
                    numBatchInputToBid++;
                    remainingCash -= inputBatchCost;
                    remainingInputCash -= inputBatchCost;
                    keepGoing = true;
                }
            }
        }

        //for cases where can't afford to buy enough inputs this round, ends up spending all money to food
        //ends up never enough to produce, always broke
        if (minInputBatches == 0 && numBatchInputToBid == 0 && foodItem.Quantity > 0)
        {
            numFoodToBid = 0;
            reason += " no bid food bc didn't bid on inputs when <1 batch inputs ";
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
            var cashForInputs = Cash - numFoodToBid * foodMarketPrice;
            // var cashForInputs = numBatchInputToBid * inputBatchCost - inputCashEquivalent;
            var bids = fillInputBids(cashForInputs);
            reason += " #inputs " + numBatchInputToBid.ToString("n2")  
                                  + " addtl input$ " + cashForInputs.ToString("c2")
                                  + " inputCost " + inputBatchCost.ToString("c2") 
                                  + " input$eq " + inputCashEquivalent.ToString("c2");
        }
        Debug.Log(name + " reason " + reason);
    }

    private bool DecideAndBorrow(ref float remainingCash, float inputBatchCost, float foodMarketPrice)
    {
        var outputRecipe = book[outputName].recipe;
        var inputCashEquivalent = inputInventoryCashEquivalent(outputRecipe);
        
        var enoughCashForInput = (remainingCash + inputCashEquivalent) >= inputBatchCost * 1.2f;
        var enoughCashForFood = (enoughCashForInput)
            ? (remainingCash - inputBatchCost) > foodMarketPrice
            : remainingCash > foodMarketPrice;
        var enoughOutput = inventory[outputName].Quantity >= book[outputName].productionPerBatch;
        var enoughFoodTarget = (enoughCashForInput) ? 1 : 2;
        var enoughFood = inventory["Food"].Quantity >= enoughFoodTarget || outputName != "Food"; //farmers are covered in enoughOutput
        var enoughFoodOrCashForFood = (enoughFood || enoughCashForFood) && outputName != "Food";
        bool enough = enoughCashForInput || enoughOutput || enoughFoodOrCashForFood;
        
        bool doesBorrow = !enough;
        float loanAmount = 0;
        if (doesBorrow)
        {
            loanAmount = inputBatchCost - inputCashEquivalent - remainingCash;
            loanAmount *= 1.2f; //padding
            if (!enoughFoodOrCashForFood && outputName != "Food")
                loanAmount += foodMarketPrice * 1.2f;
            loanAmount = Mathf.Max(30, loanAmount); //aggressive banker pushes min loan amount
            auctionStats.bank.Borrow(this, loanAmount, "cash");
        }

        var reason = "";
        reason += enoughFoodOrCashForFood ? "" : "!Food & !CashForFood ";
        reason += enoughOutput ? "" : "!output ";
        reason += enoughCashForInput ? "" : "!CashForInput ";
        Debug.Log(name + " borrowed " + loanAmount + " Cash " + remainingCash + " -> "+ Cash 
                  + " reason " + reason);
        remainingCash = Cash;

        return doesBorrow;
    }

    protected float inputInventoryCashEquivalent(Recipe recipe)
    {
        float cashEquivalent = 0;
        foreach (var input in recipe.Keys)
        {
            cashEquivalent += book[input].marketPrice * inventory[input].Quantity;
        }
        return cashEquivalent;
    }
    //taking current inventory into account, create bids to get even batches
    protected string fillInputBids(float allocatedFunds)
    {
        var origRecipe = book[outputName].recipe;
        if (origRecipe.Count == 0)
            return " none ";
        var _recipe = new Recipe(origRecipe);
        
        bool recompute;
        var reason = "";

        do
        {
            reason = "";
            float cashEquivalent = 0;
            float batchCost = 0;
            foreach (var com in origRecipe.Keys)
                inventory[com].offersThisRound = 0;
            
            foreach (var (input, amount) in _recipe)
            {
                float priceOfGood = inventory[input].GetPrice();
                cashEquivalent += priceOfGood * inventory[input].Quantity;
                batchCost += priceOfGood * amount;
            }
            float totalCashEquivalent = cashEquivalent + allocatedFunds;
            float bidNumBatches = Mathf.Floor(totalCashEquivalent / batchCost);

            recompute = false;
            foreach (var (input, amount) in _recipe.ToList()) {
                float amountNeeded = bidNumBatches * amount - inventory[input].Quantity;
                if (amountNeeded < 0) {     //more than needed, can discard and recompute w/o
                    recompute = true;
                    _recipe.Remove(input);
                    continue;
                }
                inventory[input].offersThisRound = amountNeeded;
            }
        } while (recompute);

        return reason;
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
            var cost = inventory[com].GetPrice(); //rsc.avgBidPrice.Last();
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
        float minFoodToBuy = (DaysStarving > 0) ? 1 : 0;
        //TODO buy more food if can afford it?

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