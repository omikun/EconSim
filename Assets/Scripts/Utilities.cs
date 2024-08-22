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
    Commodities comInstance;
    public ESList()
    {
        comInstance = Commodities.Instance;
    }
    new public void Add(float num)
    {
        base.Add(num);
        lastRound = comInstance.round;
    }
    public float LastAverage(int history)
    {
		if (base.Count == 0)
		{
			return 0;
		}
        var skip = Mathf.Min(base.Count-1, Mathf.Max(0, base.Count - history));
		var end = Mathf.Min(base.Count-1, history);
		if (end == skip)
		{
			return 0;
		}
        return base.GetRange(skip, end).Average();
    }

    public float LastSum(int history)
    {
		if (base.Count == 0)
		{
			return 0;
		}
        var skip = Mathf.Min(base.Count-1, Mathf.Max(0, base.Count - history));
		var end = Mathf.Min(base.Count-1, history);
		if (end == skip)
		{
			return 0;
		}
        return base.GetRange(skip, end).Sum();
    }
}