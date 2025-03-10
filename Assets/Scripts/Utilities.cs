using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using System.Linq;
using UnityEngine.XR;
using System;
using System.Net.WebSockets;
using System.Security.Cryptography.X509Certificates;

public class Utilities 
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
        from.AddToCash(-quantity);
        to.AddToCash(quantity);
    }
}

public class ESList : List<float>
{
    float avg;
    private float ema = 0; //exponential moving average
    public ESList()
    {
    }
    new public void Add(float num)
    {
        base.Add(num);
        float period = 3;
        ema = (num - ema) * 2 / (period + 1) + ema;
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
        var index = Mathf.Max(0, base.Count - history);
        var count = Math.Min(history, base.Count);
        return base.GetRange(index, count).Average();
    }

    public float ExpAverage()
    {
        return ema;
    }

    public float LastSum(int history)
    {
        if (base.Count == 0)
        {
            return 0;
        }
        var skip = Mathf.Max(0, base.Count - history);
        var numElements = Math.Min(history, base.Count);
        return base.GetRange(skip, numElements).Sum();
    }
}

public class WeightedRandomPicker<T>
{
    private List<(T item, float weight)> items = new List<(T, float)>();
    private float totalWeight = 0;

    public void AddItem(T item, float weight)
    {
        items.Add((item, weight));
        totalWeight += weight;
    }
    public bool IsEmpty()
    {
        return items.Count == 0;
    }
    public void Clear()
    {
        items.Clear();
        totalWeight = 0;
    }
    public float GetWeight(T t)
    {
        foreach (var (item, weight) in items)
        {
            if (EqualityComparer<T>.Default.Equals(item, t))
            {
                return weight;
            }
        }
        return -1f;
    }
    public T PickRandom()
    {
        if (items.Count == 0)
            throw new InvalidOperationException("No items to pick from.");

        float randomValue = UnityEngine.Random.Range(0, totalWeight);
        UnityEngine.Debug.Log("random value: " + randomValue + " total weight: " + totalWeight);
        float cumulativeWeight = 0;

        foreach (var (item, weight) in items)
        {
            cumulativeWeight += weight;
            UnityEngine.Debug.Log("randompicker: good: " + item + " cumweight: " + cumulativeWeight);
            if (randomValue < cumulativeWeight)
                return item;
        }

        return items[items.Count - 1].item; // Fallback, should rarely happen
    }
}

public class WaitNumRoundsNotTriggered {
    int numRounds = 0;
    bool triggered = false;
    public int Count()
    {
        return numRounds;
    }
    public void Reset()
    {
        numRounds = 0;
    }
    public void Tick()
    {
        if (triggered)
        {
            numRounds = 0;
            triggered = false;
        } else {
            numRounds++;
        }
    }
}

public class WatchDogTimer
{
    private int count = 0;
    private int timeOut = 0;

    public WatchDogTimer(int timeOutValue)
    {
        timeOut = timeOutValue;
    }

    public bool IsRunning()
    {
        count++;
        return count <= timeOut;
    }

    public void Reset()
    {
        count = 0;
    }
}
public static class ListUtil {
    public static string ListToString(List<float> list, string format)
    {
        string msg = "";
        foreach(var elem in list)
        {
            msg += elem.ToString(format) + ", ";
        }
        return msg;
    }
}