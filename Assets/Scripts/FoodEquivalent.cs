using UnityEngine;
public class FoodEquivalent
{
	protected EconAgent agent;

	public FoodEquivalent(EconAgent a)
	{
		agent = a;
	}
	public float GetNumFoodEquivalent(AuctionBook book, float numFoodHappy)
	{
		var numFood = GetNumFood();
		if (numFood >= numFoodHappy) return 1f;

		numFood += GetCashFood(book);
		if (numFood >= numFoodHappy) return 1f;
		
		numFood += GetOutputFood(book);
		if (numFood >= numFoodHappy) return 1f;
		
		numFood += GetInputFood(book);
		if (numFood >= numFoodHappy) return 1f;
		
		var happy = agent.foodToHappy.Evaluate(numFood / numFoodHappy);
		Debug.Log(agent.name + " happiness " + happy.ToString("n2"));
		//var profitRate = profits.TakeLast(config.historySize).Average();
		//happy *= cashToHappy.Evaluate(profitRate/config.historySize);
		return happy;
	}
	public float GetNumFood()
	{
		return agent.Food();
	}
	public float GetCashFood(AuctionBook book)
	{
		//TODO assumes enough supply; how to change happiness if demand is greater than supply?
		var foodPrice = book["Food"].marketPrice;
		var affordNumFood = agent.Cash / foodPrice;
		return affordNumFood;
	}
	public float GetOutputFood(AuctionBook book)
	{
		var foodPrice = book["Food"].marketPrice;
		var foodEquivalent = book[agent.Profession].marketPrice / foodPrice;
		var outputFoodEquivalent = agent.inventory[agent.Profession].Quantity * foodEquivalent;
		return outputFoodEquivalent;
	}
	public float GetInputFood(AuctionBook book)
	{
		var foodPrice = book["Food"].marketPrice;
		var foodEquivalent = book[agent.Profession].marketPrice / foodPrice;
		var inputFoodEquivalent =
			agent.productionStrategy.NumBatchesProduceable(book[agent.Profession], agent.inventory[agent.Profession]) * foodEquivalent;
		return inputFoodEquivalent;
	}

	public float GetNumDaysEquivalent(float numFood)
	{
		if (numFood < 0)  return 0f;
		if (numFood <= 5) return numFood;
		if (numFood <= 10) return (float)(int)((numFood-5)/2)+5;
		else return (float)(int)((numFood-9)/3)+7;
	}
}