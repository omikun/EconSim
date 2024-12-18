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
	//build as many as one can 
	//TODO what if I don't want to produce as much as one can? what if costs are high rn?
	public float CalculateNumProduceable(ResourceController rsc, InventoryItem outputItem)
	{
		float numProduceable = float.MaxValue;
		
		//find max that can be made w/ available stock
		foreach (var com in rsc.recipe.Keys)
		{
			var numProduceableWithCom = Mathf.Floor(agent.inventory[com].NumProduceable(rsc));
			numProduceable = Mathf.Min(numProduceableWithCom, numProduceable);
			// Debug.Log(agent.auctionStats.round + " " + agent.name 
			// 	+ " can produce " + numProduceable + " " + outputItem.name
			// 	+ " with " + agent.inventory[com].Quantity + "/" + rsc.recipe[com] + " " + com);
		}
		float storage = outputItem.Deficit(); //can't produce more than max stock
		numProduceable = Mathf.Min(numProduceable, storage);
		numProduceable = Mathf.Min(numProduceable, outputItem.GetProductionRate());
		return numProduceable;
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
			var com = agent.book[outputName];
			var stock = agent.inventory[outputName];
			var numProduced = CalculateNumProduced(com, stock);

			var inputCosts = "";
			agent.ConsumeInput(com, numProduced, ref inputCosts);

			//auction wide multiplier (e.g. richer ore vien or forest fire)
			var multiplier = com.productionMultiplier;
			if (numProduced == 0f || multiplier == 0f)
				return 0;

			stock.Produced(numProduced * multiplier, agent.GetCostOf(com)); 
			agent.producedThisRound[outputName] = numProduced;

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
		var numProduced = CalculateNumProduceable(rsc, item);
		//can only build fixed rate at a time
		numProduced = Mathf.Clamp(numProduced, 0, item.GetProductionRate());
		numProduced = Mathf.Floor(numProduced);

		Assert.IsTrue(numProduced >= 0);
		return numProduced;
	}
}