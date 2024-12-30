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

public class ESStreamingGraph : MonoBehaviour
{
    [Required]
    public AuctionHouse district;
    [Required]
    public AuctionStats auctionTracker;
    [Required]
    public GraphChart meanPriceGraph;
    [Required]
    public GraphChart tradeGraph;
    [Required]
    public GraphChart inventoryGraph;
    [Required]
    public GraphChart cashGraph;
    [Required]
    public GraphChart perAgentGraph;
    [Required]
    public PieChart jobChart;
    public int TotalPoints = 20;
    float lastTime = 0f;
    float lastX = 0f;
    VerticalAxis vaxisPriceGraph;
    VerticalAxis vaxisTradeGraph;
    VerticalAxis vaxisInventoryGraph;
    VerticalAxis vaxisCashGraph;
    VerticalAxis vaxisPerAgentGraph;
    [Serializable]
    public struct ChartCategoryData
    {
        public string category;
        public double lineThickness;
        public List<Material> lineMaterials;
        public MaterialTiling lineTiling;
        public Material innerFill;
        public bool strechFill;
        public Material pointMaterial;
        public double pointSize;
        public bool maskPoints;
        public ChartItemEffect lineHover; 
        public ChartItemEffect pointHover;
    }
    [SerializeField]
    public ChartCategoryData template;

    void AddCategory(GraphChart graph, string name, int lineStyle)
    {
        var s = template;
        graph.DataSource.AddCategory(name, 
            s.lineMaterials[lineStyle], 
            s.lineThickness,
            s.lineTiling,
            s.innerFill,
            s.strechFill,
            s.pointMaterial,
            s.pointSize,
            s.maskPoints);
        graph.DataSource.Set2DCategoryPrefabs(name, s.lineHover, s.pointHover);
    }
    void InitPieChart(PieChart chart)
    {
        Assert.IsFalse(chart == null);

        chart.DataSource.StartBatch(); 

        int index = 0;
        chart.DataSource.Clear();
        foreach (var good in auctionTracker.book.Keys)
        {
            //chart.DataSource.SetCategoryLine(good, template.lineMaterials[index], template.lineThickness, template.lineTiling);
            //chart.DataSource.SetCategoryFill(good, null, false);
            Debug.Log("init line material for " + good);
            index++;
        }
        chart.DataSource.EndBatch(); // finally we call EndBatch , this will cause the GraphChart to redraw itself

        lastX = 0;
    }
    void InitGraph(GraphChart graph, string title)
    {
        Assert.IsFalse(graph == null);

        graph.DataSource.StartBatch();
        // Find the specific child by name and then get the component
        Transform childTransform = transform.Find("Title");
        if (childTransform != null)
        {
            TextMeshProUGUI tmp = childTransform.GetComponent<TextMeshProUGUI>();
            Assert.IsTrue (tmp != null);
                tmp.text = title;
        }

        int index = 0;
        foreach (var good in auctionTracker.book.Keys)
        {
            graph.DataSource.ClearCategory(good);
            graph.DataSource.SetCategoryLine(good, template.lineMaterials[index], template.lineThickness, template.lineTiling);
            graph.DataSource.SetCategoryFill(good, null, false);
            index++;
        }
        graph.DataSource.EndBatch(); // finally we call EndBatch , this will cause the GraphChart to redraw itself

        lastX = 0;
    }
    void InitPerAgentGraph(GraphChart graph, string title)
    {
        Assert.IsFalse(graph == null);

        graph.DataSource.StartBatch();
        // Find the specific child by name and then get the component
        Transform childTransform = transform.Find("Title");
        if (childTransform != null)
        {
            TextMeshProUGUI tmp = childTransform.GetComponent<TextMeshProUGUI>();
            Assert.IsTrue (tmp != null);
                tmp.text = title;
        }

        int index = 0;
        foreach (var good in auctionTracker.book.Keys)
        {
            foreach (var agent in district.agents)
            {
                //TODO add gov back in later
                if (agent is Government)
                    continue;
                var categoryName = agent.name + good;
                AddCategory(graph, categoryName, index);
                graph.DataSource.ClearCategory(categoryName);
                graph.DataSource.SetCategoryLine(categoryName, template.lineMaterials[index], template.lineThickness, template.lineTiling);
                graph.DataSource.SetCategoryFill(categoryName, null, false);
            }
            index++;
        }
        graph.DataSource.EndBatch(); // finally we call EndBatch , this will cause the GraphChart to redraw itself

        lastX = 0;
    }
    void Start()
    {
        vaxisPriceGraph = meanPriceGraph.transform.GetComponent<VerticalAxis>();
        vaxisTradeGraph = tradeGraph.transform.GetComponent<VerticalAxis>();
        vaxisInventoryGraph = inventoryGraph.transform.GetComponent<VerticalAxis>();
        vaxisCashGraph = cashGraph.transform.GetComponent<VerticalAxis>();
        vaxisPerAgentGraph = perAgentGraph.transform.GetComponent<VerticalAxis>();
        Assert.IsFalse(vaxisPriceGraph == null);

        InitGraph(meanPriceGraph, "Changed Profession -test");
        InitGraph(tradeGraph, "Trades");
        InitGraph(inventoryGraph, "Inventory");
        InitGraph(cashGraph, "Cash");
        InitPerAgentGraph(perAgentGraph, "Per Agent Inventory");
        //InitPieChart(jobChart);

        lastX = 0;//TotalPoints;
        lastTime = Time.time;
    }

    public float SlideTime = -1f;//.5f; //-1 will update y axis?
    List<float> starvValues = new();

    // public void NewUpdateGraphs()
    // {
    //     foreach (var rsc in auctionTracker.book.Values)
    //     {
    //         jobChart.DataSource.SetValue(rsc.name, rsc.numAgents);
    //     }
    //     UpdateGraph(meanPriceGraph, vaxisPriceGraph, auctionTracker.book, value => value.avgClearingPrice);
    //     UpdateGraph(tradeGraph, vaxisTradeGraph, auctionTracker.book, value => value.trades);
    //     UpdateGraph(inventoryGraph, vaxisInventoryGraph, auctionTracker.book, value => value.inventory);
    //     lastX += 1;
    // }
    // public void UpdateGraph<TKey, TValue, TResult>(GraphChart chart, VerticalAxis vaxis, Dictionary<TKey, TValue> dic, Func<TValue, TResult> selector)
    // {
    //     double newMaxY = 0;
    //
    //     foreach (var rsc in dic.Values)
    //     {
    //         var values = selector(rsc);
    //         chart.DataSource.AddPointToCategoryRealtime(rsc.name, lastX, values[^1], SlideTime);
    //
    //         newMaxY  = Math.Max(newMaxY,  values.TakeLast(TotalPoints+2).Max());
    //     }
    //     chart.DataSource.VerticalViewSize = nearestBracket(vaxis, newMaxY);
    // }
    private ESList values5 = new();
    public void UpdateGraph()
    {
        double newMaxY = 0;
        double newMaxY2 = 0;
        double newMaxY3 = 0;
        double newMaxY4 = 0;
        double newMaxY5 = 0;

        foreach (var rsc in auctionTracker.book.Values)
        {
            //var values = rsc.changedProfession;
            var values = rsc.avgClearingPrice;
            var values2 = rsc.trades;
            var values3 = rsc.inventory;
            var values4 = rsc.cash;
            jobChart.DataSource.SetValue(rsc.name, rsc.numAgents);
            
            meanPriceGraph.DataSource.AddPointToCategoryRealtime(rsc.name, lastX, values[^1], SlideTime);
            tradeGraph.DataSource.AddPointToCategoryRealtime(rsc.name, lastX,values2[^1], SlideTime);
            inventoryGraph.DataSource.AddPointToCategoryRealtime(rsc.name, lastX,values3[^1], SlideTime);
            cashGraph.DataSource.AddPointToCategoryRealtime(rsc.name, lastX,values4[^1], SlideTime);

            newMaxY  = Math.Max(newMaxY,  values.TakeLast(TotalPoints+2).Max());
            newMaxY2 = Math.Max(newMaxY2, values2.TakeLast(TotalPoints+2).Max());
            newMaxY3 = Math.Max(newMaxY3, values3.TakeLast(TotalPoints+2).Max());
            newMaxY4 = Math.Max(newMaxY4, values4.TakeLast(TotalPoints+2).Max());
        }

        foreach (var agent in district.agents)
        {
            if (agent is Government)
                continue;
            foreach (var good in agent.inventory.Keys)
            {
                var value = agent.inventory[good].Quantity;
                var categoryName = agent.name + good;
                
                perAgentGraph.DataSource.AddPointToCategoryRealtime(categoryName, lastX, value, SlideTime);
                newMaxY5 = Math.Max(newMaxY5, value);
            }
        }
        values5.Add((float)newMaxY5);
        newMaxY5 = Math.Max(newMaxY5, values5.TakeLast(TotalPoints+2).Max());

        meanPriceGraph.DataSource.VerticalViewSize = nearestBracket(vaxisPriceGraph, newMaxY);
        tradeGraph.DataSource.VerticalViewSize = nearestBracket(vaxisTradeGraph, newMaxY2);
        inventoryGraph.DataSource.VerticalViewSize = nearestBracket(vaxisInventoryGraph, newMaxY3);
        cashGraph.DataSource.VerticalViewSize = nearestBracket(vaxisCashGraph, newMaxY4);
        perAgentGraph.DataSource.VerticalViewSize = nearestBracket(vaxisPerAgentGraph, newMaxY5);
        lastX += 1;
    }

    public bool EnableDynamicFit = false;
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
