using UnityEngine;
public class FoodEquivalent
{
	protected EconAgent agent;

	public FoodEquivalent(EconAgent a)
	{
		agent = a;
	}
	public float GetHappyLevel(AuctionBook book, float numFoodHappy)
	{
		var numFood = GetNumFood();
		if (numFood >= numFoodHappy) return 1f;

		numFood += GetCashFood();
		if (numFood >= numFoodHappy) return 1f;
		
		numFood += GetOutputFood();
		if (numFood >= numFoodHappy) return 1f;
		
		numFood += GetInputFood();
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
	//returns number of food that can be bought with cash at market price
	public float GetCashFood()
	{
		//TODO assumes enough supply; how to change happiness if demand is greater than supply?
		var foodPrice = agent.book["Food"].marketPrice;
		var affordNumFood = agent.Cash / foodPrice;
		return affordNumFood;
	}
	//returns number of food that 
	public float GetOutputFood()
	{
		var numFoodPerOutput = GetNumFoodPerOutput();
		var outputItem = agent.inventory[agent.Profession];
		var outputFoodEquivalent = outputItem.Quantity * numFoodPerOutput;
		return outputFoodEquivalent;
	}

	//returns number of food that can be purchased from num outputs produceable by current input inventory
	public float GetInputFood()
	{
		var numFoodPerOutput = GetNumFoodPerOutput();
		var outputItem = agent.inventory[agent.Profession];
		var outputRsc = agent.book[agent.Profession];
		var numBatches= agent.productionStrategy.NumBatchesProduceable(outputRsc, outputItem);
		var numOutputs = Mathf.Min(numBatches, outputItem.GetMaxProductionRate(numBatches));
		var numFood = numOutputs * numFoodPerOutput;
		return numBatches;
		// return numFood;
	}
	public float GetNumFoodPerOutput()
	{
		var foodPrice = agent.book["Food"].marketPrice;
		var numFoodPerOutput = agent.book[agent.Profession].marketPrice / foodPrice;
		return numFoodPerOutput;
	}

	public float GetNumDaysEquivalent(float numFood)
	{
		if (numFood < 0)  return 0f;
		if (numFood <= 5) return numFood;
		if (numFood <= 10) return (float)(int)((numFood-5)/2)+5;
		else return (float)(int)((numFood-9)/3)+7;
	}
}