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
    public GraphChart askChart;
    [Required]
    public GraphChart bidChart;

    private GraphObject meanPriceChartObject;
    private GraphObject tradeChartObject;
    private GraphObject inventoryChartObject;
    private GraphObject cashChartObject;
    private GraphObject askChartObject;
    private GraphObject bidChartObject;
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
        
        meanPriceChartObject = new(meanPriceGraph);
        tradeChartObject = new(tradeGraph);
        inventoryChartObject = new(inventoryGraph);
        cashChartObject = new(cashGraph);
        askChartObject = new(askChart);
        bidChartObject = new(bidChart);
        Assert.IsFalse(vaxisPriceGraph == null);

        InitGraph(meanPriceChartObject.chart, "Changed Profession -test");
        InitGraph(tradeChartObject.chart, "Trades");
        InitGraph(inventoryChartObject.chart, "Inventory");
        InitGraph(cashChartObject.chart, "Cash");
        InitGraph(askChartObject.chart, "Asks");
        InitGraph(bidChartObject.chart, "Bids");
        
        InitPerAgentGraph(perAgentGraph, "Per Agent Inventory");
        //InitPieChart(jobChart);
        // jobChart.DataSource.StartBatch();
        //     jobChart.DataSource.RemoveCategory("Ore");
        //     jobChart.DataSource.RemoveCategory("Metal");
        // jobChart.DataSource.EndBatch(); // finally we call EndBatch , this will cause the GraphChart to redraw itself

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
    private ESList perAgentValues = new();

    public void UpdateGraphs()
    {
        meanPriceChartObject.Plot(auctionTracker.book, rsc => rsc.avgClearingPrice, lastX, SlideTime);
        tradeChartObject.Plot(auctionTracker.book, rsc => rsc.trades, lastX, SlideTime);
        inventoryChartObject.Plot(auctionTracker.book, rsc => rsc.inventory, lastX, SlideTime);
        cashChartObject.Plot(auctionTracker.book, rsc => rsc.cash, lastX, SlideTime);
        askChartObject.Plot(auctionTracker.book, rsc => rsc.asks, lastX, SlideTime);
        bidChartObject.Plot(auctionTracker.book, rsc => rsc.bids, lastX, SlideTime);

        foreach (var rsc in auctionTracker.book.Values)
        {
            jobChart.DataSource.SetValue(rsc.name, rsc.numAgents);
        }

        UpdatePerAgentInventoryGraph();
        lastX += 1;
    }
    private void UpdatePerAgentInventoryGraph()
    {
        if (!perAgentGraph.gameObject.activeSelf)
            return;
        
        double newMaxY = 0;
        foreach (var agent in district.agents)
        {
            if (agent is Government)
                continue;
            foreach (var good in agent.inventory.Keys)
            {
                var value = agent.inventory[good].Quantity;
                var categoryName = agent.name + good;
                
                perAgentGraph.DataSource.AddPointToCategoryRealtime(categoryName, lastX, value, SlideTime);
                newMaxY = Math.Max(newMaxY, value);
            }
        }
        perAgentValues.Add((float)newMaxY);
        newMaxY = Math.Max(newMaxY, perAgentValues.TakeLast(TotalPoints+1).Max());

        perAgentGraph.DataSource.VerticalViewSize = nearestBracket(vaxisPerAgentGraph, newMaxY);
        
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
