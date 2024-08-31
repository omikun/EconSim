#define Graph_And_Chart_PRO
using UnityEngine;
using System.Collections;
using ChartAndGraph;
using UnityEngine.Assertions;
using Sirenix.OdinInspector;

public class StreamingGraph : MonoBehaviour
{
    [Required]
    public GraphChart Graph;
    public int TotalPoints = 500;
    float lastTime = 0f;
    float lastX = 0f;
    AuctionStats auctionTracker;

    void Start()
    {
		auctionTracker = AuctionStats.Instance;
        if (Graph == null) // the ChartGraph info is obtained via the inspector
        {
            Assert.IsFalse(Graph == null);
            return;
        }
        float x = 3f * TotalPoints;
        Graph.DataSource.StartBatch(); 
        Graph.DataSource.ClearCategory("Food");
        Graph.DataSource.ClearCategory("Wood");

        for (int i = 0; i < TotalPoints; i++)  //add random points to the graph
        {
            //TODO with AddPointToCategoryWithLabel in the future?
            Graph.DataSource.AddPointToCategory("Food",x,Random.value * 13f + 10f);
            Graph.DataSource.AddPointToCategory("Wood",x,Random.value * 10f);
            //Graph.DataSource.AddPointToCategory("Food", System.DateTime.Now - System.TimeSpan.FromSeconds(x), Random.value * 20f + 10f); // each time we call AddPointToCategory 
            x -= 1;
            lastX = x;
        }

        Graph.DataSource.EndBatch(); // finally we call EndBatch , this will cause the GraphChart to redraw itself
        lastTime = Time.time;
    }

    void Update()
    {
        return;
        float deltaTime = .01f;
        float time = Time.time;
        if (lastTime + deltaTime < time)
        {
            lastTime = time;
            lastX += deltaTime;
//            System.DateTime t = ChartDateUtility.ValueToDate(lastX);
            Graph.DataSource.AddPointToCategoryRealtime("Player 1", System.DateTime.Now, Random.value * 20f + 10f, deltaTime); // each time we call AddPointToCategory 
            Graph.DataSource.AddPointToCategoryRealtime("Player 2", System.DateTime.Now, Random.value * 10f, deltaTime); // each time we call AddPointToCategory
        }

    }
}
