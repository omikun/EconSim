using System.Collections;
using System.Collections.Generic;
using UnityEngine.Assertions;
using UnityEngine;

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
public class Commodity
{
	public Commodity(float p, Dependency d)
	{
		price = p;
		dep = d;
		demand = 1;
	}
	public void Update(float p, float dem)
	{
		price = p;
		demand = dem;
	}
	int debug = 0;
	public float price { get; private set; } //market price
	public float demand { get; private set; }
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

	bool Add(string name, float price, Dependency dep)
	{
		if (com.ContainsKey(name)) { return false; }
		Assert.IsNotNull(dep);

		com.Add(name, new Commodity(price, dep));
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
		//replicate paper
		Dependency foodDep = new Dependency();
		foodDep.Add("Wood", 1);
		Add("Food", 4, foodDep);

		Dependency woodDep = new Dependency();
		woodDep.Add("Food", 2);
		woodDep.Add("Tool", 2);
		Add("Wood", 3, woodDep);

		Dependency oreDep = new Dependency();
		oreDep.Add("Food", 4);
		Add("Ore", 2, oreDep);

		Dependency metalDep = new Dependency();
		metalDep.Add("Food", 4);
		Add("Metal", 2, metalDep);

		Dependency toolDep = new Dependency();
		toolDep.Add("Food", 4);
		Add("Tool", 5, toolDep);

		PrintStat();
		return;
#if false
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
