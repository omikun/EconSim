#define Graph_And_Chart_PRO
using UnityEngine;
using System.Collections;
using ChartAndGraph;
using UnityEngine.Assertions;
using Sirenix.OdinInspector;
using System.Linq;
using System.Collections.Generic;
using System;
using TMPro;
using UnityEngine.Serialization;

[Serializable]
public class GraphObject : MonoBehaviour
{
    public GraphChart chart;
    private VerticalAxis vaxis;
    private double newMaxY = 0;
    public int TotalPoints = 20;
    public bool EnableDynamicFit = false; //may need to expose this

    public void ResetY()
    {
        newMaxY = 0;
    }

    public void Plot(AuctionBook book, Func<ResourceController, ESList> selector, 
                     float lastX, float SlideTime)
    {
        if (!chart.gameObject.activeSelf)
            return;
        
        newMaxY = 0;
        foreach (var rsc in book.Values)
        {
            var values = selector(rsc);
            chart.DataSource.AddPointToCategoryRealtime(rsc.name, lastX, values[^1], SlideTime);
            newMaxY  = Math.Max(newMaxY,  values.TakeLast(TotalPoints+1).Max());
        }
        chart.DataSource.VerticalViewSize = nearestBracket(vaxis, newMaxY);
    }

    public GraphObject(GraphChart c)
    {
        chart = c;
        vaxis = chart.transform.GetComponent<VerticalAxis>();
    }
    
    double nearestBracket(VerticalAxis vaxis, double value)
    {
        if (value < 0 || !EnableDynamicFit)
            return value;
        else if (value < 1)
        {
            vaxis.MainDivisions.FractionDigits = 2;
            vaxis.MainDivisions.Total = 4;
            return 1;
        }
        else if (value < 2)
        {
            vaxis.MainDivisions.FractionDigits = 2;
            vaxis.MainDivisions.Total = 4;
            return 2;
        }
        else if (value < 5)
        {
            vaxis.MainDivisions.FractionDigits = 1;
            vaxis.MainDivisions.Total = 5;
            return 5;
        }
        else if (value < 10)
        {
            vaxis.MainDivisions.FractionDigits = 0;
            vaxis.MainDivisions.Total = 5;
            return 10;
        }
        else if (value < 100)
        {
            vaxis.MainDivisions.FractionDigits = 0;
            var roundedValue = Math.Ceiling(value/10) * 10;
            var divisor = (value < 50) ? 5 : 10;
            vaxis.MainDivisions.Total = (int)(roundedValue / divisor);
            return roundedValue;
        }
        else 
        {
            vaxis.MainDivisions.FractionDigits = 0;
            vaxis.MainDivisions.Total = (int)(value / 5);
            return value;
        }
    }
}