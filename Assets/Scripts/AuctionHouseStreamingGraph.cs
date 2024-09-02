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
    public GraphChart Graph;
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

    void AddCategory(string name, int lineStyle)
    {
        var s = template;
        Graph.DataSource.AddCategory(name, 
            s.lineMaterials[lineStyle], 
            s.lineThickness,
            s.lineTiling,
            s.innerFill,
            s.strechFill,
            s.pointMaterial,
            s.pointSize,
            s.maskPoints);
        Graph.DataSource.Set2DCategoryPrefabs(name, s.lineHover, s.pointHover);
    }
    void Start()
    {
		auctionTracker = AuctionStats.Instance;
        if (Graph == null) // the ChartGraph info is obtained via the inspector
        {
            Assert.IsFalse(Graph == null);
            return;
        }
        float x = 1f * TotalPoints;
        Graph.DataSource.StartBatch(); 
        Graph.DataSource.RenameCategory("Player 1", "Metal");
        Graph.DataSource.RenameCategory("Player 2", "Tool");
        Graph.DataSource.ClearCategory("Metal");
        Graph.DataSource.ClearCategory("Tool");
        AddCategory("Ore", 3);
        AddCategory("Wood", 4);
        AddCategory("Food", 5);

        for (int i = 0; i < TotalPoints; i++)  //add random points to the graph
        {
            break;
            //TODO with AddPointToCategoryWithLabel in the future?
            Graph.DataSource.AddPointToCategory("Food",x,UnityEngine.Random.value);
            Graph.DataSource.AddPointToCategory("Wood",x,UnityEngine.Random.value);
            //Graph.DataSource.AddPointToCategory("Food", System.DateTime.Now - System.TimeSpan.FromSeconds(x), Random.value * 20f + 10f); // each time we call AddPointToCategory 
            x -= 1;
            lastX = x;
        }

        Graph.DataSource.EndBatch(); // finally we call EndBatch , this will cause the GraphChart to redraw itself
        lastX = TotalPoints;
        lastTime = Time.time;
    }

    public float SlideTime = .5f;
    public void UpdateGraph()
    {
        //            System.DateTime t = ChartDateUtility.ValueToDate(lastX);
        // Graph.DataSource.AddPointToCategoryRealtime("Player 1", System.DateTime.Now, Random.value * 20f + 10f, deltaTime); // each time we call AddPointToCategory 
        // Graph.DataSource.AddPointToCategoryRealtime("Player 2", System.DateTime.Now, Random.value * 10f, deltaTime); // each time we call AddPointToCategory
        List<string> goods = new List<string> { "Food", "Wood", "Ore", "Metal", "Tool" };
        foreach (var good in goods)
        {
            var price = auctionTracker.book[good].avgClearingPrice[^1];
            Graph.DataSource.AddPointToCategoryRealtime(good, lastX, price, SlideTime);
        }
        lastX += SlideTime;
            //Graph.DataSource.AddPointToCategoryRealtime("Wood",lastX,Random.value * 10f, deltaTime);

    }
}
