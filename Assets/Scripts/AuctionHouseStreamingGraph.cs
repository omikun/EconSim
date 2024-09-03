#define Graph_And_Chart_PRO
using UnityEngine;
using System.Collections;
using ChartAndGraph;
using UnityEngine.Assertions;
using Sirenix.OdinInspector;
using System.Collections.Generic;
using System;

public class ESStreamingGraph : MonoBehaviour
{
    [Required]
    public GraphChart meanPriceGraph;
    [Required]
    public GraphChart InventoryGraph;
    public int TotalPoints = 500;
    float lastTime = 0f;
    float lastX = 0f;
    AuctionStats auctionTracker;
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
    void InitGraph(GraphChart graph)
    {
        if (graph == null) // the ChartGraph info is obtained via the inspector
        {
            Assert.IsFalse(graph == null);
            return;
        }
        float x = 1f * TotalPoints;
        graph.DataSource.StartBatch(); 
        graph.DataSource.ClearCategory("Food");
        graph.DataSource.ClearCategory("Wood");
        graph.DataSource.ClearCategory("Ore");
        graph.DataSource.ClearCategory("Metal");
        graph.DataSource.ClearCategory("Tool");

        for (int i = 0; i < TotalPoints; i++)  //add random points to the graph
        {
            break;
            //TODO with AddPointToCategoryWithLabel in the future?
            graph.DataSource.AddPointToCategory("Food",x,UnityEngine.Random.value);
            graph.DataSource.AddPointToCategory("Wood",x,UnityEngine.Random.value);
            //Graph.DataSource.AddPointToCategory("Food", System.DateTime.Now - System.TimeSpan.FromSeconds(x), Random.value * 20f + 10f); // each time we call AddPointToCategory 
            x -= 1;
            lastX = x;
        }

        graph.DataSource.EndBatch(); // finally we call EndBatch , this will cause the GraphChart to redraw itself
    }
    void Start()
    {
		auctionTracker = AuctionStats.Instance;

        InitGraph(meanPriceGraph);
        InitGraph(InventoryGraph);

        lastX = 0;//TotalPoints;
        lastTime = Time.time;
    }

    public float SlideTime = -1f;//.5f;
    public void UpdateGraph()
    {
        updateGraph(meanPriceGraph);
        updateGraph(InventoryGraph);
    }
    void updateGraph(GraphChart graph)
    {
        //            System.DateTime t = ChartDateUtility.ValueToDate(lastX);
        // Graph.DataSource.AddPointToCategoryRealtime("Player 1", System.DateTime.Now, Random.value * 20f + 10f, deltaTime); // each time we call AddPointToCategory 
        // Graph.DataSource.AddPointToCategoryRealtime("Player 2", System.DateTime.Now, Random.value * 10f, deltaTime); // each time we call AddPointToCategory
        List<string> goods = new List<string> { "Food", "Wood", "Ore", "Metal", "Tool" };
        foreach (var good in auctionTracker.book.Keys)
        //foreach (var good in goods)
        {
            var price = auctionTracker.book[good].bids[^1];
            graph.DataSource.AddPointToCategoryRealtime(good, lastX, price, SlideTime);
        }
        lastX += 1;
            //Graph.DataSource.AddPointToCategoryRealtime("Wood",lastX,Random.value * 10f, deltaTime);

    }
}
