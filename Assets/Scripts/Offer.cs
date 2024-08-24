using System.Collections;
using System.Collections.Generic;
using UnityEngine.Assertions;
using UnityEngine;
using System.Linq;
using System;

using CommodityName = System.String;
public class Offers : Dictionary<CommodityName, Offer> { }
public class Offer
{
	public Offer(CommodityName c, float p, float q, EconAgent a)
	{
		commodityName = c;
		offerPrice = p;
		clearingPrice = p;
		remainingQuantity = q;
		offerQuantity = q;
		agent = a;
	}
	public void Accepted(float p, float q)
	{
		remainingQuantity -= q;
		clearingPriceVolume += p * q;
		CalculateClearingPrice();
	}
	public void Print()
	{
		Debug.Log(agent.gameObject.name + ": " + commodityName + " trade: " + offerPrice + ", " + remainingQuantity);
	}
	public void CalculateClearingPrice()
	{
		var tradedQuantity = offerQuantity - remainingQuantity;
		if (tradedQuantity == 0)
		{
			return;
		}
		clearingPrice = clearingPriceVolume / tradedQuantity;
	}
	public CommodityName commodityName { get; private set; }
	public float offerPrice { get; private set; }
	public float offerQuantity { get; private set; }
	public float clearingPrice { get; private set; }
	float clearingPriceVolume; // total price of traded goods; sum of price of each good traded over multiple trades
	public float remainingQuantity { get; private set; }
	public EconAgent agent{ get; private set; }
}