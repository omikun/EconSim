using UnityEngine;
public class FoodEquivalent
{
	public static float GetNumFoodEquivalent(AuctionBook book, EconAgent agent, float numFoodHappy)
	{
		var numFood = GetNumFood(agent);
		if (numFood >= numFoodHappy) return 1f;

		numFood += GetCashFood(book, agent);
		if (numFood >= numFoodHappy) return 1f;
		
		numFood += GetOutputFood(book, agent);
		if (numFood >= numFoodHappy) return 1f;
		
		numFood += GetInputFood(book, agent);
		if (numFood >= numFoodHappy) return 1f;
		
		var happy = agent.foodToHappy.Evaluate(numFood / numFoodHappy);
		Debug.Log(agent.name + " happiness " + happy.ToString("n2"));
		//var profitRate = profits.TakeLast(config.historySize).Average();
		//happy *= cashToHappy.Evaluate(profitRate/config.historySize);
		return happy;
	}
	public static float GetNumFood(EconAgent agent)
	{
		return agent.Food();
	}
	public static float GetCashFood(AuctionBook book, EconAgent agent)
	{
		//TODO assumes enough supply; how to change happiness if demand is greater than supply?
		var foodPrice = book["Food"].marketPrice;
		var affordNumFood = agent.Cash / foodPrice;
		return affordNumFood;
	}
	public static float GetOutputFood(AuctionBook book, EconAgent agent)
	{
		var foodPrice = book["Food"].marketPrice;
		var foodEquivalent = book[agent.Profession].marketPrice / foodPrice;
		var outputFoodEquivalent = agent.inventory[agent.Profession].Quantity * foodEquivalent;
		return outputFoodEquivalent;
	}
	public static float GetInputFood(AuctionBook book, EconAgent agent)
	{
		var foodPrice = book["Food"].marketPrice;
		var foodEquivalent = book[agent.Profession].marketPrice / foodPrice;
		var inputFoodEquivalent =
			agent.CalculateNumProduceable(book[agent.Profession], agent.inventory[agent.Profession]) * foodEquivalent;
		return inputFoodEquivalent;
	}
}