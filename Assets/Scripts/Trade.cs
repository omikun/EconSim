using System.Collections;
using System.Collections.Generic;
using UnityEngine.Assertions;
using UnityEngine;
using System.Linq;
using System;

public class Trade
{
	public Trade(string c, float p, float q, EconAgent a)
	{
		commodity = c;
		price = p;
		clearingPrice = p;
		remainingQuantity = q;
		offerQuantity = q;
		agent = a;
	}
	public float Reduce(float q)
	{
		remainingQuantity -= q;
		return remainingQuantity;
	}
	public void Print()
	{
		Debug.Log(agent.gameObject.name + ": " + commodity + " trade: " + price + ", " + remainingQuantity);
	}
	public string commodity { get; private set; }
	public float price { get; private set; }
	public float clearingPrice;
	public float remainingQuantity { get; private set; }
	public float offerQuantity { get; private set; }
	public EconAgent agent{ get; private set; }
}