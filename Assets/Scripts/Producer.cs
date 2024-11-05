using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;

public class Producer
{
    protected EconAgent agent;

    public Producer(EconAgent a)
    {
        agent = a;
    }
    protected virtual float FindSellCount(string c)
	{
		var numAsks = agent.inventory[c].FindSellCount(agent.book[c], agent.config.historySize, agent.config.enablePriceFavorability);

		//leave some to eat if food
		if (c == "Food" && agent.config.foodConsumption)
		{
			numAsks = Mathf.Min(numAsks, Mathf.Max(0,agent.inventory[c].Quantity - 1));
		}

		return numAsks;
	}
    public virtual Offers CreateAsks()
    {
	    //sell everything not needed by output
	    var asks = new Offers();
    
	    foreach (var item in agent.inventory)
	    {
		    var commodityName = item.Key;
		    if (agent.inputs.Contains(commodityName))
		    {
			    continue;
		    }
		    var stock = agent.inventory[commodityName];
		    float sellQuantity = FindSellCount(commodityName);
		    //float sellPrice = sellStock.GetPrice();
		    // + cost of food since last sell
		    var expense = Mathf.Max(0, agent.foodExpense);
		    float sellPrice = agent.inventory[commodityName].cost * agent.config.profitMarkup + expense;
		    if (agent.config.baselineSellPrice)
		    {
			    var baseSellPrice = agent.book[commodityName].marketPrice;
			    var delta = agent.config.baselineSellPriceDelta;
			    var min = 1f - delta;
			    var max = 1f + delta;
			    baseSellPrice *= UnityEngine.Random.Range(min, max);
			    // sellPrice = baseSellPrice;
			    if (agent.config.baselineSellPriceMinCost)
				    sellPrice = Mathf.Max(sellPrice, baseSellPrice);
			    else
				    sellPrice = baseSellPrice;
		    }
		    if (agent.config.sanityCheckSellPrice)
		    {
			    sellPrice = agent.book[commodityName].setPrice;
		    }
		    if (agent.config.sanityCheckSellQuant)
		    {
			    sellQuantity = item.Value.GetProductionRate() * agent.config.sanityCheckTradeVolume;
			    sellQuantity = Mathf.Min(sellQuantity, agent.inventory[commodityName].Quantity);
		    }
    
		    if (sellQuantity > 0 && sellPrice > 0)
		    {
			    Debug.Log(agent.auctionStats.round + ": " + agent.name 
			              + " wants to sell " + sellQuantity + " " + commodityName 
			              + " for " + sellPrice.ToString("c2") 
			              + ", has in stock" + agent.inventory[commodityName].Quantity);
			    Assert.IsTrue(sellQuantity <= agent.inventory[commodityName].Quantity);
			    asks.Add(commodityName, new Offer(commodityName, sellPrice, sellQuantity, agent));
			    stock.askPrice = sellPrice;
			    stock.askQuantity = sellQuantity;
		    }
	    }
	    return asks;
    }
	public virtual float Produce() {
		//foreach (var outputName in agent.outputName)
		var outputName = agent.outputName;
		{
			if (!agent.book.ContainsKey(outputName))
			{
				Debug.Log(agent.auctionStats.round + " " + agent.name + " not valid output " + outputName);
			}
			Assert.IsTrue(agent.book.ContainsKey(outputName));
			var com = agent.book[outputName];
			var stock = agent.inventory[outputName];
			var numProduced = agent.CalculateNumProduced(com, stock);

			var inputCosts = "";
			agent.ConsumeInput(com, numProduced, ref inputCosts);

			//auction wide multiplier (e.g. richer ore vien or forest fire)
			var multiplier = com.productionMultiplier;
			if (numProduced == 0f || multiplier == 0f)
				return 0;

			stock.Produced(numProduced * multiplier, numProduced * agent.GetCostOf(com)); 

			Debug.Log(agent.auctionStats.round + " " + agent.name 
				+ " has " + agent.Cash.ToString("c2") 
				+ " made " + numProduced.ToString("n2") + " " + outputName 
				+ " total: " + stock.Quantity 
				+ " cost: " + stock.cost.ToString("c2") 
				+ inputCosts);
			Assert.IsFalse(float.IsNaN(numProduced));

			agent.producedThisRound[outputName] = numProduced;
		}
		return agent.producedThisRound.Sum(x => x.Value);
	}
}
