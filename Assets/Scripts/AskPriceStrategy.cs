using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;

public abstract class AskPriceStrategy
{
	protected EconAgent agent;

	public AskPriceStrategy(EconAgent a)
	{
		agent = a;
	}

	protected virtual float FindSellCount(string c)
	{
		var numAsks = agent.inventory[c]
			.FindSellCount(agent.book[c], agent.config.historySize, agent.config.enablePriceFavorability);

		//leave some to eat if food
		if (c == "Food" && agent.config.foodConsumption)
		{
			numAsks = Mathf.Min(numAsks, Mathf.Max(0, agent.inventory[c].Quantity - 1));
		}

		return numAsks;
	}
	
	public abstract float GetSellPrice(string commodityName);

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
			if (sellQuantity <= 0)
				continue;
			//float sellPrice = sellStock.GetPrice();
			// + cost of food since last sell
			float sellPrice = GetSellPrice(commodityName);
			if (agent.config.sellPriceMinFoodExpense)
			{
				var minCost = stock.unitCost + (agent.foodExpense / sellQuantity);
				sellPrice = Mathf.Max(sellPrice, minCost);
				Assert.IsTrue(sellPrice > 0f);
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
	public float RandomizeSellPrice(float sellPrice, float cost)
	{
		if (agent.config.randomizeSellPrice)
		{
			var delta = agent.config.sellPriceDelta;
			var min = 1f - delta;
			var max = 1f + delta;
			sellPrice *= UnityEngine.Random.Range(min, max);
		}
		if (agent.config.sellPriceMinCost)
			sellPrice = Mathf.Max(sellPrice, cost);
		return sellPrice;
	}
}

public class AtCostAskStrategy : AskPriceStrategy
{
	public AtCostAskStrategy(EconAgent a) : base(a) { }
	public override float GetSellPrice(string commodityName)
	{
		var expense = Mathf.Max(0, agent.foodExpense);
		var cost = agent.inventory[commodityName].unitCost;
		var sellPrice = cost + expense;
		return RandomizeSellPrice(sellPrice, sellPrice);
	}
}

public class FixedProfitAskStrategy : AtCostAskStrategy
{
	public FixedProfitAskStrategy(EconAgent a) : base(a) { }
	public override float GetSellPrice(string commodityName)
	{
		var expense = Mathf.Max(0, agent.foodExpense);
		var cost = agent.inventory[commodityName].unitCost;
		//TODO expense should be spread over all units
		var sellPrice = cost * agent.config.profitMarkup + expense;
		return RandomizeSellPrice(sellPrice, sellPrice);
	}
}
public class MarketAskStrategy : AtCostAskStrategy
{
	public MarketAskStrategy(EconAgent a) : base(a) { }
	public override float GetSellPrice(string commodityName)
	{
		var sellPrice = agent.book[commodityName].marketPrice;
		var cost = agent.inventory[commodityName].unitCost;
		return RandomizeSellPrice(sellPrice, cost);
		
	}
}

public class FixedAskPriceStrategy : AtCostAskStrategy
{
	public FixedAskPriceStrategy (EconAgent a) : base(a) { }
	public override float GetSellPrice(string commodityName)
	{
		var sellPrice = agent.book[commodityName].setPrice;
		var cost = agent.inventory[commodityName].unitCost;
		return RandomizeSellPrice(sellPrice, cost);
	}
}

public class DynamicAskPriceStrategy : AskPriceStrategy
{
	public DynamicAskPriceStrategy(EconAgent a) : base(a) { }

	public override float GetSellPrice(string commodityName)
	{
		//if last round not all sold, -5% ask price
		return agent.inventory[commodityName].priceBelief;
	}
}