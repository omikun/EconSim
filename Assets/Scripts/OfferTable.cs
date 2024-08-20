
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Assertions;
using UnityEngine;
using System.Linq;
using System;

//all asks or bids
using CommodityName = System.String;
public class OfferTable : Dictionary<CommodityName, OfferList>
{
	public OfferTable() 
	{
        var com = Commodities.Instance.com;
        foreach (var c in com)
        {
            base.Add(c.Key, new OfferList());
        }
    }
	public void Add(Offers ts)
	{
		foreach (var entry in ts)
		{
			var commodity = entry.Key;
			var trade = entry.Value;
			base[commodity].Add(trade);
		}
	}
}