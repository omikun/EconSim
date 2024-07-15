using System.Collections;
using System.Collections.Generic;
using UnityEngine.Assertions;
using UnityEngine;
using System.Linq;

//using Dependency = System.Collections.Generic.Dictionary<string, float>;
//TODO replace dependencies with recipe
public class Recipe
{
	//support conditionals
	public class Pair {
		public string commodity;
		public float quantity;
	}
	public Dependency depWithTool, depWithoutTool;
	public float Produce(Dictionary<string, float> inputs)
	{
		float numProduce = float.MaxValue;
		Dependency dep = depWithoutTool;
		if (inputs["Tool"] > 0)
		{
			//production bonus
			dep = depWithTool;
			//TODO chance tools -= 1
		}
		foreach (var item in dep)
		{
			var commodity = item.Key;
			var unitsNeeded = item.Value;
			//number of units can be made = num units given
			var canMake = inputs[commodity] / unitsNeeded;
			numProduce = Mathf.Min(canMake, numProduce);
		}
		return numProduce;
	}
}
public class ESList : List<float>
{
    float avg;
    public float LastAverage(int history)
    {
		if (base.Count == 0)
		{
			return 0;
		}
        var skip = Mathf.Min(base.Count-1, Mathf.Max(0, base.Count - history));
		var end = Mathf.Min(base.Count-1, history);
        var newList = base.GetRange(skip, end);
        avg = newList.Average();
        return avg;
    }
}
public class Commodity
{
	const float defaultPrice = 1;
	public ESList bids, asks, prices, trades, profits; 
	
	float avgPrice = 1;
	bool firstAvgPrice = true;
	public float GetAvgPrice(int history)
	{
		// TODO why first? if (firstAvgPrice == true)
		{
            firstAvgPrice = false;
//            Debug.Log(name + " prices: " + prices.Count + " to skip: " + skip);
            var skip = Mathf.Max(0, prices.Count - history);
            avgPrice = prices.Skip(skip).Average();
        }
		return avgPrice;
	}
	public Commodity(string n, float p, Dependency d)
	{
		name = n;
		production = p;
		price = defaultPrice;
		dep = d;
		demand = 1;

		bids   = new ESList();
		bids.Add(1);
		asks   = new  ESList();
		asks.Add(1);
		trades = new  ESList();
		trades.Add(1);
		prices = new  ESList();
		prices.Add(1);
		profits = new ESList();
		profits.Add(1);
	}
	public void Update(float p, float dem)
	{
		firstAvgPrice = true;
		price = p;
		demand = dem;
	}
	int debug = 0;
	string name;
	public float price { get; private set; } //market price
	public float demand { get; private set; }
	public float production { get; private set; }
	public Dependency dep { get; private set; }
}
public class Dependency : Dictionary<string, float>
{
	Dictionary<string, Commodity> com;
	public Dependency() 
	{
		com = Commodities.Instance.com;
	}
	void Add(string name, float quantity)
	{
        //Debug.Assert(com.ContainsKey(name));
		if (!com.ContainsKey(name))
			Debug.Log("Can't find dependency in commodity");
		base.Add(name, quantity);
		Debug.Log("New Dependency: " + name + "-" + quantity);
	}
}
public class Commodities : MonoBehaviour
{
    public static Commodities Instance { get; private set; }

	public Dictionary<string, Commodity> com { get; private set; }
	
    private void Awake()
    {
        Instance = this;
		com = new Dictionary<string, Commodity>(); //names, market price
		Init();
    }
    public string GetMostProfitableProfession(int history = 10)
	{
		string prof = "invalid";
		float most = 0;

		foreach (var entry in com)
		{
			var commodity = entry.Key;
			var profitHistory = entry.Value.profits;
			//WARNING this history refers to the last # agents' profits, not last # rounds... short history if popular profession...
			var profit = profitHistory.LastAverage(history);
			if (profit > most)
			{
				prof = commodity;
			}
		}
		return prof;
	}
	//get price of good
	float GetRelativeDemand(Commodity c, int history=10)
	{
        var averagePrice = c.prices.LastAverage(history);
        var minPrice = c.prices.Min();
		var price = c.prices[c.prices.Count-1];
		var relativeDemand = (price - minPrice) / (averagePrice - minPrice);
		//Debug.Log("avgPrice: " + averagePrice.ToString("c2") + " min: " + minPrice.ToString("c2") + " curr: " + price.ToString("c2"));
		//var relativeDemand = Mathf.InverseLerp(minPrice, averagePrice, price);
		return relativeDemand;
	}
	public string GetHottestGood(int history=10)
	{
		var rand = new System.Random();
		//string mostDemand = com.ElementAt(rand.Next(0, com.Count)).Key;
		string mostDemand = "invalid";
		float max = 1.1f;
		string mostRDDemand = "invalid";
		float maxRD = max;
		foreach (var c in com)
		{
			var asks = c.Value.asks.LastAverage(history);
			var bids = c.Value.bids.LastAverage(history);
            asks = Mathf.Max(.5f, asks);
			var ratio = bids / asks;
			var relDemand = GetRelativeDemand(c.Value, history);

			if ( maxRD < relDemand)
			{
				mostRDDemand = c.Key;
				maxRD = relDemand;
			}
			if (max < ratio)
			{
				max = ratio;
				mostDemand = c.Key;
			}
			//Debug.Log(c.Key + " Ratio: " + ratio.ToString("n2") + " relative demand: " + relDemand);
		}
		Debug.Log("Most in demand: " + mostDemand + ": " + max);
		//Debug.Log("Most in rel demand: " + mostRDDemand + ": " + maxRD);
		return mostDemand;
	}
	bool Add(string name, float production, Dependency dep)
	{
		if (com.ContainsKey(name)) { return false; }
		Assert.IsNotNull(dep);

		com.Add(name, new Commodity(name, production, dep));
		return true;
	}
    void PrintStat()
    {
		foreach (var item in com)
		{
			Debug.Log(item.Key + ": " + item.Value.price);
			if (item.Value.dep != null)
			{
				Debug.Log("Dependencies: " );
				foreach (var depItem in item.Value.dep)
				{
                    Debug.Log(" -> " + depItem.Key + ": " + depItem.Value);
				}
			}
		}
	}
    // Use this for initialization
    void Init () {
#if true
		Debug.Log("Initializing commodities");
		//replicate paper
		Dependency foodDep = new Dependency();
		foodDep.Add("Wood", 2);
		Add("Food", 5, foodDep);

		Dependency woodDep = new Dependency();
		woodDep.Add("Food", 1);
		woodDep.Add("Tool", .1f);
		Add("Wood", 3, woodDep);

		Dependency oreDep = new Dependency();
		oreDep.Add("Food", 2f);
		Add("Ore", 2, oreDep);

		Dependency metalDep = new Dependency();
		metalDep.Add("Food", 2);
		metalDep.Add("Ore", 2);
		Add("Metal", 1, metalDep);

		Dependency toolDep = new Dependency();
		toolDep.Add("Food", 2.3f);
		toolDep.Add("Metal", 2);
		Add("Tool", 1, toolDep);

		//PrintStat();
		return;
#else
		Add("Food", 4);
		Add("Water", 1);
		Add("Oil", 6);

		Dependency oreDep = new Dependency();
		oreDep.Add("Oil", 10);
		Add("Ore", 5, oreDep);

		Dependency energyDep = new Dependency();
		energyDep.Add("Oil", .1f);
		Add("Energy", .2f, energyDep);

		Dependency compDep = new Dependency();
		compDep.Add("Oil", .3f);
		compDep.Add("Ore", .6f);
		Add("Computer", 300, compDep);

		Dependency rocketDep = new Dependency();
		rocketDep.Add("Oil", 30000f);
		rocketDep.Add("Water", 200000f);
		rocketDep.Add("Ore", 6000f);
		Add("Rocket", 400000f, rocketDep);

		Dependency ftDep = new Dependency();
		ftDep.Add("Oil", 20000f);
		ftDep.Add("Water", 100000f);
		ftDep.Add("Ore", 1000f);
		Add("Fuel Tank", 100000f, ftDep);

		Dependency f9Dep = new Dependency();
		f9Dep.Add("Rocket", 10); //9 for 1st stage, 1 for 2nd stage
		f9Dep.Add("Fuel Tank", 2); //1 for each stage
		Add("Falcon 9", 60000000f, f9Dep);

		Dependency fhDep = new Dependency();
		fhDep.Add("Falcon 9", 3); //2 boosters, 1 core
		f9Dep.Add("Rocket", 1); //2nd stage on core
		fhDep.Add("Fuel Tank", 1);
		Add("Falcon Heavy", 60000000f, f9Dep);

		PrintStat();
#endif
    }
	
	// Update is called once per frame
	void Update () {
		
	}
}
