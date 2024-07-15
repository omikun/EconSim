using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;


public class Transaction {
    public Transaction(float p, float q)
    {
        price = p;
        quantity = q;
    }
    public float price;
    public float quantity;
}
public class History : Queue<Transaction>
{
    public float min = 0;
    public float max = 0;
    public float Min() { return min; }
	public float Max() { return max; }
	public void Tick()
	{
		min = Min();
		max = Max();
	}
}
public class CommodityStock {
	public string commodityName { get; private set; }
	const float significant = 0.25f;
	const float sig_imbalance = .33f;
	const float lowInventory = .1f;
	const float highInventory = 2f;
	public History buyHistory;
	public History sellHistory;
	public float cost = 1;
    public float last_price = 1;
	public float wobble = .02f;
	public float quantity;
	public float maxQuantity;
	private float _minPriceBelief;
	public float maxPriceBelief;
	public float meanCost; //total cost spent to acquire stock
	//number of units produced per turn = production * productionRate
	public float production; //scaler of productionRate
	public float productionRate = 1; //# of assembly lines


    public String Stats(String header) 
    {
        String ret = header + commodityName + ", stock, " + quantity + "\n"; 
        ret += header + commodityName + ", minPriceBelief, " + _minPriceBelief + "\n";
        ret += header + commodityName + ", maxPriceBelief, " + maxPriceBelief + "\n";
        return ret;
    }
	public CommodityStock (string _name, float _quantity=1, float _maxQuantity=10, 
					float _meanPrice=1, float _production=1)
	{
		buyHistory = new History();
		sellHistory = new History();
		commodityName = _name;
		quantity = _quantity;
		maxQuantity = _maxQuantity;
		minPriceBelief = _meanPrice / 2;
		maxPriceBelief = _meanPrice * 2;
		meanCost = _meanPrice;
		buyHistory.Enqueue(new Transaction(1,_meanPrice));
		sellHistory.Enqueue(new Transaction(1,_meanPrice));
		production = _production;
	}
	public void Tick()
	{
        sellHistory.Tick();
        buyHistory.Tick();
	}
	public float Buy(float quant, float price)
	{
		//update meanCost of units in stock
        var totalCost = meanCost * this.quantity + price * quant;
        this.meanCost = totalCost / this.quantity;
		var leftOver = quant - Deficit();
		this.quantity += quant;
		if (leftOver > 0)
		{
			//this.quantity -= leftOver;
			Debug.Log("Bought too much! Max: " + this.quantity + " " + quant.ToString("n2") + " leftover: " + leftOver.ToString("n2"));
		} else {
			leftOver = 0;
		}
		//update price belief
		updatePriceBelief(false, price);
        buyHistory.Enqueue(new Transaction(price, quant));
		//return quant;
		return quant;// - leftOver;
	}
	public void Sell(float quant, float price)
	{
		this.quantity += quant;
		updatePriceBelief(true, price);
        sellHistory.Enqueue(new Transaction(price, quant));
	}
	public float GetPrice()
	{
		SanePriceBeliefs();
		var p = UnityEngine.Random.Range(minPriceBelief, maxPriceBelief);
		return p;
	}
	void SanePriceBeliefs()
	{
		minPriceBelief = Mathf.Max(cost, minPriceBelief);
		maxPriceBelief = Mathf.Max(minPriceBelief*1.1f, maxPriceBelief);
		minPriceBelief = Mathf.Clamp(minPriceBelief, 0.1f, 900);
		maxPriceBelief = Mathf.Clamp(maxPriceBelief, 1.1f, 1000);
	}
	
	public void updatePriceBelief(bool sell, float price, bool success=true)
	{
		SanePriceBeliefs();
        Debug.Log(commodityName + " bounds: " + minPriceBelief.ToString("n2") + " < " + maxPriceBelief.ToString("n2"));
		if (minPriceBelief > maxPriceBelief)
		{
            Assert.IsTrue(minPriceBelief < maxPriceBelief);
			Debug.Log(commodityName + " ERROR " + minPriceBelief.ToString("n2") + " > "  + maxPriceBelief.ToString("n2"));
		}

        var buy = !sell;
		var mean = (minPriceBelief + maxPriceBelief) / 2;
		var deltaMean = mean - price; //TODO or use auction house mean price?
		Debug.Log("mean: " + mean.ToString("c2") + " price " + price.ToString("c2") + " dMean: " + deltaMean.ToString("c2"));
		if (success)
		{
			if ((sell && deltaMean < -significant * mean) //undersold
             || (buy  && deltaMean >  significant * mean))//overpaid
            {
				minPriceBelief -= deltaMean / 4; 		//shift toward mean
				maxPriceBelief -= deltaMean / 4;
            }
			minPriceBelief += wobble * mean;
			maxPriceBelief -= wobble * mean;
			if (minPriceBelief > maxPriceBelief)
			{
				var avg = (minPriceBelief + maxPriceBelief) / 2f;
				minPriceBelief = avg * (1 - wobble);
				maxPriceBelief = avg * (1 + wobble);
			}
            wobble /= 2;
		} else {
            minPriceBelief -= deltaMean / 4;        //shift toward mean
            maxPriceBelief -= deltaMean / 4;

			//if low inventory and can't buy or high inventory and can't sell
			if ((buy  && this.quantity < maxQuantity * lowInventory)
             || (sell && this.quantity > maxQuantity * highInventory))
            {
                //wobble += 0.02f;
            } else {
                //wobble -= 0.02f;
				//if too much demand or supply
				//new mean might be 0%-200% of market rate 
				//shift more based on market supply/demand
			}
			minPriceBelief -= wobble * mean;
			maxPriceBelief += wobble * mean;
		}

		//clamp to sane values
		//if (maxPriceBelief > 1000)
			//Debug.Log("ERROR " + maxPriceBelief.ToString("c2") + " > 1000");
	//	if (maxPriceBelief < 0 || minPriceBelief < 0)
//			Debug.Log("ERROR negative " + minPriceBelief.ToString("c2") + " " + maxPriceBelief.ToString("c2") );

        if (minPriceBelief < maxPriceBelief)
            minPriceBelief = maxPriceBelief / 2;
		Assert.IsTrue(minPriceBelief < maxPriceBelief);
		
		SanePriceBeliefs();

		if (float.IsNaN(minPriceBelief))
		{
			Debug.Log(commodityName + ": NaN! wobble" + wobble.ToString("n2") + " mean: " + mean.ToString("c2") + " sold price: " +price.ToString("c2"));
		}
	}
	//TODO change quantity based on historical price ranges & deficit
	public float Deficit() { 
		var shortage = maxQuantity - quantity;
		//Assert.IsTrue(shortage >= 0);
		return Mathf.Max(0, shortage);
    }
	public float Surplus() { return quantity; }
	public float minPriceBelief {
		get { return _minPriceBelief; }
		set {
			//if (value > 1000)
//                Debug.Log("minPriceBelief old: " + _minPriceBelief.ToString("c2") + " new: " + value.ToString("c2"));
			_minPriceBelief = value;
		}
	}
}