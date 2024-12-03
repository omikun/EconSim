using UnityEngine;
using UnityEngine.Assertions;
using System.Linq;

public class QolAgent : QoLSimpleAgent
{
    public override void Init(SimulationConfig cfg, AuctionStats at, string b, float initStock, float maxstock)
    {
	    base.Init(cfg, at, b, initStock, maxstock);
    }
    protected float numBatchesConsumed = 0;

    public override void Decide()
    {
        var rsc = book[outputName];
        var stock = inventory[outputName];
        var numBatches = productionStrategy.NumBatchesProduceable(rsc, stock);
        ConsumeGoods(numBatches);
        var numProduced = Produce(numBatches);
        Debug.Log(auctionStats.round + " " + name + " produced " + numProduced + " " + rsc.name);
        base.Decide();
    }

    public void ConsumeGoods(float numBatches)
    {
        //determine number of batches worth of inputs to consume
        //iterate over each input and find 
        //else consume at least n food
        foreach (var item in inventory.Values)
        {
            if (!inRecipe(item.name)) //if not inputs
                continue;
            if (item.Quantity <= 0) //can't go below 0
                continue;
            float amountConsumed = book[outputName].recipe[item.name] * numBatches;
            item.Decrease(amountConsumed);
            Assert.IsTrue(item.Quantity >= 0);
        }
    }
    public float Produce(float numBatches)
    {
        var item = inventory[outputName];
        var maxProduction = item.GetProductionRate(numBatches);
        //produce less if sold less
        // var numSoldLastRound = item.saleHistory[^1].quantity;
        // var smoothedProduction = Mathf.Round((numSoldLastRound + maxProduction) / 2f);
        // var numProduced = Mathf.Min(smoothedProduction, maxProduction);
        // item.Increase(numProduced);
        //don't make any if missing a recipe ingredient
        item.Increase(maxProduction);
        return maxProduction;
    }
}