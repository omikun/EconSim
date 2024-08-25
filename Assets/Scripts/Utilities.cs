using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using System.Linq;
using UnityEngine.XR;
using System;
using System.Net.WebSockets;

public class Utilities : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public static void TransferQuantity(float quantity, EconAgent from, EconAgent to)
    {
        from.modify_cash(-quantity);
        to.modify_cash(quantity);
    }
}

public class ESList : List<float>
{
    float avg;
    int lastRound = 0;
    AuctionStats comInstance;
    public ESList()
    {
        comInstance = AuctionStats.Instance;
    }
    new public void Add(float num)
    {
        base.Add(num);
        lastRound = comInstance.round;
    }
    public float LastHighest(int history)
    {
        if (base.Count == 0)
        {
            return 0;
        }
        var skip = Mathf.Max(0, base.Count - history);
        var end = Math.Min(history, base.Count);
        if (skip == end)
        {
            return 0;
        }
        return base.GetRange(skip, end).Max();
    }
    public float LastAverage(int history)
    {
        if (base.Count == 0)
        {
            return 0;
        }
        var skip = Mathf.Max(0, base.Count - history);
        var end = Math.Min(history, base.Count);
        return base.GetRange(skip, end).Average();
    }

    public float LastSum(int history)
    {
        if (base.Count == 0)
        {
            return -1;
        }
        var skip = Mathf.Max(0, base.Count - history);
        var end = Math.Min(history, base.Count);
        if (skip == end)
        {
            return -2;
        }
        return base.GetRange(skip, end).Sum();
    }
}