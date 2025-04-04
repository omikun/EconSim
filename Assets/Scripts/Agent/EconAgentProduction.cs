using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Serialization;

public partial class EconAgent
{
    protected internal Consumer consumer;
    protected internal ProductionStrategy productionStrategy;
    protected internal HashSet<string> inputs = new();
    protected internal Dictionary<string, float> producedThisRound = new();

    [FormerlySerializedAs("numProducedThisRound")]
    public ES2Float numUnitsProduced = new();

    public ES2Float numBatchesProduced = new();

    public virtual void ConsumeGoods()
    {
    }

    public virtual float Produce()
    {
        return productionStrategy.Produce();
    }

    public float CalcMinProduction()
    {
        Assert.IsTrue(book.ContainsKey(outputName));
        var rsc = book[outputName];
        var item = inventory[rsc.name];
        return productionStrategy.MinNumBatchesProduceable(rsc, item);
    }

    protected internal void ConsumeInput(ResourceController rsc, float numProduced, ref string msg)
    {
        float numBatches = numProduced / rsc.productionPerBatch;
        foreach (var dep in rsc.recipe)
        {
            var stock = inventory[dep.Key].Quantity;
            var numUsed = dep.Value * numBatches;
            Debug.Log(auctionStats.round + " " + name + " has " + stock + " " + dep.Key + " used " + numUsed);
            Assert.IsTrue(numUsed == 0 || stock >= numUsed);
            inventory[dep.Key].Decrease(numUsed);
            msg += dep.Key + ": " + inventory[dep.Key].meanCost.ToString("c2");
        }
    }
}