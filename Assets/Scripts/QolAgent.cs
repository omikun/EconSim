using UnityEngine;
using UnityEngine.Assertions;
using System.Linq;

//medium agent
public class QolAgent : QoLSimpleAgent
{
    public override void Init(SimulationConfig cfg, AuctionStats at, string b, float initStock, float maxstock)
    {
	    base.Init(cfg, at, b, initStock, maxstock);
    }
    protected float numBatchesConsumed = 0;

    public override void Decide()
    {
        decideProduction();
        // decideOffers();
        base.Decide();
    }
    private void decideProduction()
    {
        var rsc = book[outputName];
        var stock = inventory[outputName];
        var numBatches = NumBatchesProduceable(rsc, stock);
        var numProduced = Produce(numBatches);
        ConsumeGoods(numBatches);
        Debug.Log(auctionStats.round + " " + name + " produced " + numProduced + " " + rsc.name);
    }

    // public virtual void decideOffers()
    public void testing()
    {
        //decide how much to buy and sell
        //how many inputs to buy at their respective price beliefs?
        float buyPress = buyPressure();
        //if input price is high, can current sell price be worth it?
        //how much was sold last round?
        //TODO what if none was sold last round??
        //can sell price go higher?
        //compute tollerable input price
        //get num food produced in batch
        var numOutputPerBatch = inventory[outputName].ProductionPerBatch;
        var outputPrice = inventory[outputName].GetPrice();
        var revenuePerBatch = numOutputPerBatch * outputPrice;
        var recipe = book[outputName].recipe;
        var inputCost = recipe.Sum(pair => inventory[pair.Key].GetPrice() * pair.Value);
        var foodCost = inventory["Food"].meanCost;
        float profitbility = revenuePerBatch / (inputCost + foodCost);
        foreach (var (com, numNeeded) in recipe)
        {
            var perBatchCost = inventory[com].meanCost * numNeeded;
            float cashAfford = Cash / perBatchCost;
            //how to combine buyPressure, cash to afford, and profitibility??
        }
        
        //TODO how to split buying food vs buying inputs?
        //if no other inputs, then buy all the food at current price belief
        //else buy enough for one batch if affordable
        //don't buy if already have one batch of inputs?
        //what about outputs?
        //think of inputs and outputs as batches of production
        //if total = 1 batch, buy another batch
        //if total = 2 batches, buy another batch if it is cheaper (by how much?)
        //don't care if it's all inputs or outputs
        //if last round sold 1 batch, buy another batch
        //if last round sold more than 1 batch, 
    }
    
    private float buyPressure()
    {
        //determine buy pressure
        //if low input/output inventory
        //if low on food
        float buyPressureOutput = 0;
        float buyPressureInput = 0;
        float buyPressureFood = 0;
        float numOutputs = inventory[outputName].Quantity;
        //TODO what if output is food??
        float minQuant = 3f;
        
        if (numOutputs < minQuant)
            buyPressureOutput = (minQuant - numOutputs)/ minQuant;
        var recipe = book[outputName].recipe;
        foreach (var com in recipe.Keys)
        {
            float quant = inventory[com].Quantity;
            if (quant < minQuant)
                buyPressureInput += (minQuant - quant) / minQuant;
        }

        var food =  inventory["Food"].Quantity;
        if (food < minQuant)
            buyPressureFood += (minQuant - food) / minQuant;
        float buyPressure = Mathf.Min(buyPressureOutput, buyPressureInput);
        buyPressure = Mathf.Min(buyPressure, buyPressureFood);
        return buyPressure;
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
	    numBatches = Mathf.Min(outputItem.GetMaxBatchRate(), numBatches);
        
	    var realProductionRate = outputItem.GetMaxProductionRate(numBatches);
	    var realBatchRate = Mathf.Floor(realProductionRate / outputItem.ProductionPerBatch);
		Debug.Log(auctionStats.round + " " + name
		          + " can ultimately produce " + realBatchRate + " batches of " + outputItem.name);

        return realBatchRate;
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
                    amountConsumed = 6;
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

            var recipe = book[outputName].recipe;
            if (recipe.ContainsKey(item.name))
            {
                float consumedByBatch = recipe[item.name] * numBatches;
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
        var item = inventory[outputName];
        var numProduced = item.GetMaxProductionRate(numBatches);
        //produce less if sold less
        // var numSoldLastRound = item.saleHistory[^1].quantity;
        // var smoothedProduction = Mathf.Round((numSoldLastRound + maxProduction) / 2f);
        // var numProduced = Mathf.Min(smoothedProduction, maxProduction);
        // item.Increase(numProduced);
        //don't make any if missing a recipe ingredient
        item.Increase(numProduced);
        return numProduced;
    }
}