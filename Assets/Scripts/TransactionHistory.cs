
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Assertions;


public class TransactionHistory : List<InventoryTransaction>
{
    float min = 0;
    float max = 0;
    float avg = 0;
    public int history_size = 10;
    ESList prices = new ESList();
    public float Min() { return min; }
	public float Max() { return max; }
    public new void Add(InventoryTransaction t)
    {
        base.Add(t);
  
        float sum = 0;
        int count = 0;
        min = t.Price;
        max = t.Price;
        for (int i = base.Count-1; i > 0 && i > (base.Count - history_size); i--)
        {
            min = Mathf.Min(base[i].Price, min);
            max = Mathf.Max(base[i].Price, max);
            sum += base[i].Price;
            count++;
        }
        avg = (count == 0) ? 0 : sum / (float)count;
    }
    public void UpdateLast(InventoryTransaction t)
    {
        var priceVolume = base[^1].Quantity * base[^1].Price;
        priceVolume += t.Price * t.Quantity;
        base[^1].Quantity += t.Quantity;
        base[^1].Price = priceVolume / base[^1].Quantity;
    }
}