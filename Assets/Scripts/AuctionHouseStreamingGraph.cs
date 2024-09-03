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
        graph.DataSource.StartBatch(); 

        int index = 0;
        foreach (var good in auctionTracker.book.Keys)
        {
            graph.DataSource.ClearCategory(good);
            graph.DataSource.SetCategoryLine(good, template.lineMaterials[index], template.lineThickness, template.lineTiling);
            graph.DataSource.SetCategoryFill(good, null, false);
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

        InitGraph(meanPriceGraph);
        InitGraph(InventoryGraph);

        lastX = 0;//TotalPoints;
        lastTime = Time.time;
    }

    public float SlideTime = -1f;//.5f; //-1 will update y axis?
    public void UpdateGraph()
    {
        updateGraph(meanPriceGraph);
        updateGraph(InventoryGraph);
        lastX += 1;
    }
    void updateGraph(GraphChart graph)
    {
        foreach (var good in auctionTracker.book.Keys)
        {
            var price = auctionTracker.book[good].bids[^1];
            graph.DataSource.AddPointToCategoryRealtime(good, lastX, price, SlideTime);
        }
    }
}
