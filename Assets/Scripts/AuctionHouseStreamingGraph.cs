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

public class ESStreamingGraph : MonoBehaviour
{
    [Required]
    public AuctionHouse district;
    [Required]
    public GraphChart meanPriceGraph;
    [Required]
    public GraphChart InventoryGraph;
    [Required]
    public PieChart jobChart;
    public int TotalPoints = 20;
    float lastTime = 0f;
    float lastX = 0f;
    AuctionStats auctionTracker;
    VerticalAxis vaxisPriceGraph;
    VerticalAxis vaxisTradeGraph;
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
       // /*
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
        for (int i = 0; i < TotalPoints; i++)  //add random points to the graph
        {
            //TODO with AddPointToCategoryWithLabel in the future?
            //graph.DataSource.AddPointToCategory("Food",x,UnityEngine.Random.value);
        }
        chart.DataSource.EndBatch(); // finally we call EndBatch , this will cause the GraphChart to redraw itself

        lastX = 0;
        //*/
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
            Debug.Log("init line material for " + good);
            index++;
        }
        for (int i = 0; i < TotalPoints; i++)  //add random points to the graph
        {
            //TODO with AddPointToCategoryWithLabel in the future?
            //graph.DataSource.AddPointToCategory("Food",x,UnityEngine.Random.value);
        }
        graph.DataSource.EndBatch(); // finally we call EndBatch , this will cause the GraphChart to redraw itself

        lastX = 0;
    }
    void Start()
    {
		auctionTracker = AuctionStats.Instance;
        vaxisPriceGraph = meanPriceGraph.transform.GetComponent<VerticalAxis>();
        vaxisTradeGraph = InventoryGraph.transform.GetComponent<VerticalAxis>();
        Assert.IsFalse(vaxisPriceGraph == null);

        InitGraph(meanPriceGraph, "Changed Profession -test");
        InitGraph(InventoryGraph, "Trades");
        //InitPieChart(jobChart);

        lastX = 0;//TotalPoints;
        lastTime = Time.time;
    }

    public float SlideTime = -1f;//.5f; //-1 will update y axis?
    List<float> starvValues = new();
    public void UpdateGraph()
    {
        double newMaxY = 0;
        double newMaxY2 = 0;

        foreach (var rsc in auctionTracker.book.Values)
        {
            var values = rsc.changedProfession;
            var values2 = rsc.trades;
            //values = rsc.avgClearingPrice;
            jobChart.DataSource.SetValue(rsc.name, rsc.numAgents);
            meanPriceGraph.DataSource.AddPointToCategoryRealtime(rsc.name, lastX, values[^1], SlideTime);
            InventoryGraph.DataSource.AddPointToCategoryRealtime(rsc.name, lastX,values2[^1], SlideTime);

            //newMaxY = Math.Max(newMaxY, rsc.avgClearingPrice.TakeLast(TotalPoints+2).Max());
            newMaxY  = Math.Max(newMaxY,  values.TakeLast(TotalPoints+2).Max());
            newMaxY2 = Math.Max(newMaxY2, values2.TakeLast(TotalPoints+2).Max());
        }
        meanPriceGraph.DataSource.VerticalViewSize = nearestBracket(vaxisPriceGraph, newMaxY);
        InventoryGraph.DataSource.VerticalViewSize = nearestBracket(vaxisTradeGraph, newMaxY2);
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
