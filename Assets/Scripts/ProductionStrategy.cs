using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;

public abstract class ProductionStrategy
{
    protected EconAgent agent;

    public ProductionStrategy(EconAgent a)
    {
        agent = a;
    }
	public float NumBatchesProduceable(ResourceController rsc, InventoryItem outputItem)
	{
		float numBatches = float.MaxValue;
		
		//find max that can be made w/ available stock
		foreach (var com in rsc.recipe.Keys)
		{
			var numProduceableWithCom = Mathf.Floor(agent.inventory[com].NumProduceable(rsc));
			numBatches = Mathf.Min(numProduceableWithCom, numBatches);
			Debug.Log(agent.auctionStats.round + " " + agent.name 
				+ " can produce " + numBatches + " batches of " + outputItem.name
				+ " with " + agent.inventory[com].Quantity + "/" + rsc.recipe[com] + " " + com);
		}
		
	    numBatches = Mathf.Min(outputItem.GetMaxBatchRate(), numBatches);
	    var realProductionRate = outputItem.GetMaxProductionRate(numBatches);
	    var realBatchRate = Mathf.Floor(realProductionRate / outputItem.ProductionPerBatch);
	    
		Debug.Log(agent.auctionStats.round + " " + agent.name
		          + " can ultimately produce " + realBatchRate + " batches of " + outputItem.name);
		return realBatchRate;
	}

	protected internal abstract float CalculateNumProduced(ResourceController rsc, InventoryItem item);
	public float Produce() {
		//foreach (var outputName in agent.outputName)
		var outputName = agent.outputName;
		{
			if (!agent.book.ContainsKey(outputName))
			{
				Assert.IsTrue(false, agent.auctionStats.round + " " + agent.name + " not valid output " + outputName);
			}
			Assert.IsTrue(agent.book.ContainsKey(outputName));
			var rsc = agent.book[outputName];
			var stock = agent.inventory[outputName];
			var numProduced = CalculateNumProduced(rsc, stock);

			var inputCosts = "";
			agent.ConsumeInput(rsc, numProduced, ref inputCosts);

			//auction wide multiplier (e.g. richer ore vien or forest fire)
			var multiplier = rsc.productionMultiplier;
			numProduced *= multiplier; 

			stock.Produced(numProduced, agent.GetCostOf(rsc)); 
			agent.producedThisRound[outputName] = numProduced;
			agent.numProducedThisRound = numProduced;

			Debug.Log(agent.auctionStats.round + " " + agent.name 
				+ " has " + agent.Cash.ToString("c2") 
				+ " made " + numProduced.ToString("n2") + " " + outputName 
				+ " total: " + stock.Quantity 
				+ " cost: " + stock.unitCost.ToString("c2") 
				+ inputCosts);
			Assert.IsFalse(float.IsNaN(numProduced));
		}
		return agent.producedThisRound.Sum(x => x.Value);
	}
}

public class FixedProduction : ProductionStrategy
{
    public FixedProduction(EconAgent a) : base(a) {}
	protected internal override float CalculateNumProduced(ResourceController rsc, InventoryItem item)
	{
		var numProduced = NumBatchesProduceable(rsc, item);
		//can only build fixed rate at a time
		numProduced = Mathf.Clamp(numProduced, 0, item.GetMaxProductionRate());
		numProduced = Mathf.Floor(numProduced);

		Assert.IsTrue(numProduced >= 0);
		return numProduced;
	}
}