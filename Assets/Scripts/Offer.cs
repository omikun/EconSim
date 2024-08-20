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
	public float Reduce(float q)
	{
		remainingQuantity -= q;
		return remainingQuantity;
	}
	public void Print()
	{
		Debug.Log(agent.gameObject.name + ": " + commodityName + " trade: " + offerPrice + ", " + remainingQuantity);
	}
	public CommodityName commodityName { get; private set; }
	public float offerPrice { get; private set; }
	public float offerQuantity { get; private set; }
	public float clearingPrice;
	public float remainingQuantity { get; private set; }
	public EconAgent agent{ get; private set; }
}