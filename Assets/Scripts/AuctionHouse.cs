using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

 
public static class IListExtensions {
    /// <summary>
    /// Shuffles the element order of the specified list.
    /// </summary>
    public static void Shuffle<T>(this IList<T> ts) {
        var count = ts.Count;
        var last = count - 1;
        for (var i = 0; i < last; ++i) {
            var r = UnityEngine.Random.Range(i, count);
            var tmp = ts[i];
            ts[i] = ts[r];
            ts[r] = tmp;
        }
    }
}
public class Trade
{
	public Trade(string c, float p, float q, EconAgent a)
	{
		commodity = c;
		price = p;
		quantity = q;
		agent = a;
	}
	public float Reduce(float q)
	{
		quantity -= q;
		return quantity;
	}
	public void Print()
	{
		Debug.Log(commodity + " trade: " + price + ", " + quantity + ", " + agent.transform.gameObject.name);
	}
	public string commodity { get; private set; }
	public float price { get; private set; }
	public float quantity { get; private set; }
	public EconAgent agent{ get; private set; }
}
public class TradeSubmission : Dictionary<string, Trade> { }
public class Trades : List<Trade> { 
	public new void RemoveAt(int index)
	{
		int before = base.Count;
		base.RemoveAt(index);
        if (before != base.Count + 1) 
			Debug.Log("did not remove trade correctly! before: " 
			+ before + " after: " + base.Count);
    }
    public void Shuffle()
    {
        var count = base.Count;
        var last = count - 1;
        for (var i = 0; i < last; ++i) {
            var r = UnityEngine.Random.Range(i, count);
            var tmp = base[i];
            base[i] = base[r];
            base[r] = tmp;
        }
    }
	public void Print()
	{
		var enumerator = base.GetEnumerator();
        while (enumerator.MoveNext())
        {
            var item = enumerator.Current;
			item.Print();
		}
	}


}
//commodities["commodity"] = ordered list<price, quantity, seller>
public class TradeTable : Dictionary<string, Trades>
{
	public TradeTable() 
	{
        var com = Commodities.Instance.com;
        foreach (var c in com)
        {
            base.Add(c.Key, new Trades());
        }
    }
	public void Add(TradeSubmission ts)
	{
		foreach (var entry in ts)
		{
			var commodity = entry.Key;
			var trade = entry.Value;
			base[commodity].Add(trade);
		}
	}
}
public class AuctionHouse : MonoBehaviour {

	public float initCash = 500;
	public float initStock = 5;
	public float maxStock = 10;
	List<EconAgent> agents = new List<EconAgent>();
	TradeTable askTable, bidTable;
	//gmp = Graph Mean Price
	List<GraphMe> gMeanPrice = new List<GraphMe>();
	List<GraphMe> gUnitsExchanged = new List<GraphMe>();
	List<GraphMe> gProfessions = new List<GraphMe>();
	float lastTick;
	// Use this for initialization
	void Start () {
		lastTick = 0;
		int count = 0;
		var com = Commodities.Instance.com;
        gMeanPrice = new List<GraphMe>(com.Count);
        gUnitsExchanged = new List<GraphMe>(com.Count);
        gProfessions = new List<GraphMe>(com.Count);

		/* initialize graphs */
		var gmp = GameObject.Find("AvgPriceGraph");
		for (int i = 0; i < com.Count; i++) 
		{
			gMeanPrice.Add(gmp.transform.Find("line"+i).GetComponent<GraphMe>());
		}
		var gue = GameObject.Find("UnitsExchangedGraph");
		for (int i = 0; i < com.Count; i++) {
			gUnitsExchanged.Add(gue.transform.Find("line"+i).GetComponent<GraphMe>());
		}
		var gp = GameObject.Find("ProfessionsGraph");
		for (int i = 0; i < com.Count; i++) {
			gProfessions.Add(gp.transform.Find("line"+i).GetComponent<GraphMe>());
		}

		/* initialize agents */
		foreach (Transform tChild in transform)
		{
			GameObject child = tChild.gameObject;
			var agent = child.GetComponent<EconAgent>();

			string type = "invalid";
			int numPerType = 4;
			int typeNum = 1;
			if (count < numPerType*typeNum++) 		type = "Food";	//farmer
			else if (count < numPerType*typeNum++) 	type = "Wood";	//woodcutter
			else if (count < numPerType*typeNum++) 	type = "Ore";	//miner
			else if (count < numPerType*typeNum++) 	type = "Metal";	//refiner
			else if (count < numPerType*typeNum) 	type = "Tool";	//blacksmith
			else Debug.Log(count + " too many agents, not supported: " + typeNum);
			InitAgent(agent, type);
			agents.Add(agent);
			count++;
		}
		askTable = new TradeTable();
		bidTable = new TradeTable();

		//initialize agents
	}
	
	void InitAgent(EconAgent agent, string type)
	{
        agent.debug++;
        List<string> buildables = new List<string>();
		buildables.Add(type);
        agent.Init(initCash, buildables, initStock, maxStock);
	}
	// Update is called once per frame
	void FixedUpdate () {
		//wait 1s before update
		float tickInterval = .5f;
		if (Time.time - lastTick > tickInterval)
		{
            Tick();
			lastTick = Time.time;
		}
	}

	Random rnd = new Random();
	void Tick()
	{
		var com = Commodities.Instance.com;
		//get all agents asks
		//get all agents bids
		foreach (var agent in agents)
		{
			askTable.Add(agent.Produce(com));
			bidTable.Add(agent.Consume(com));
		}
		//resolve prices
		foreach (var entry in com)
		{
			float moneyExchanged = 0;
			float goodsExchanged = 0;
			var commodity = entry.Key;
			var asks = askTable[commodity];
            var bids = bidTable[commodity];
			var demand = bids.Count / Mathf.Max(.1f, (float)asks.Count);
            
			asks.Shuffle();
			bids.Shuffle();
			
			asks.Sort((x, y) => x.price.CompareTo(y.price)); //inc
			bids.Sort((x, y) => y.price.CompareTo(x.price)); //dec

			//Debug.Log(commodity + " asks sorted: "); asks.Print();
			//Debug.Log(commodity + " bids sorted: "); bids.Print();
            while (asks.Count > 0 && bids.Count > 0)
            {
                //get highest bid and lowest ask
				int askIndex = 0;
				var ask = asks[askIndex];
				int bidIndex = 0;
				var bid = bids[bidIndex];
				//set price
				var clearingPrice = (bid.price + ask.price) / 2;
				//trade
				var tradeQuantity = Mathf.Min(bid.quantity, ask.quantity);
				ask.agent.Sell(commodity, tradeQuantity, clearingPrice);
				bid.agent.Buy(commodity, tradeQuantity, clearingPrice);
                //remove ask/bid if fullfilled
                if (ask.Reduce(tradeQuantity) == 0) { asks.RemoveAt(askIndex); }
				if (bid.Reduce(tradeQuantity) == 0) { bids.RemoveAt(bidIndex); }
				moneyExchanged += clearingPrice * tradeQuantity;
				goodsExchanged += tradeQuantity;
            }
			if (goodsExchanged == 0)
			{
				goodsExchanged = 1;
			} else if (goodsExchanged < 0)
			{
				Debug.Log("ERROR " + commodity + " had negative exchanges!?!?!");
			}
			
			var averagePrice = moneyExchanged/goodsExchanged;
			if (float.IsNaN(averagePrice))
			{
				Debug.Log(commodity + ": average price is nan");
			}
			SetGraph(gMeanPrice, commodity, averagePrice);
			SetGraph(gUnitsExchanged, commodity, goodsExchanged);
            //reject the rest
            foreach (var ask in asks)
			{
				ask.agent.RejectAsk(commodity, averagePrice);
			}
			asks.Clear();
			foreach (var bid in bids)
			{
				bid.agent.RejectBid(commodity, averagePrice);
			}
			bids.Clear();

			//calculate supply/demand
			//var excessDemand = asks.Sum(ask => ask.quantity);
			//var excessSupply = bids.Sum(bid => bid.quantity);
			//var demand = (goodsExchanged + excessDemand) 
			//					 / (goodsExchanged + excessSupply);

            entry.Value.Update(averagePrice, demand);
		}
        foreach (var agent in agents)
        {
            agent.Tick();
        }
		CountProfessions();
	}
	void CountProfessions()
	{
		var com = Commodities.Instance.com;
		//count number of agents per professions
		Dictionary<string, float> professions = new Dictionary<string, float>();
		//initialize professions
		foreach (var item in com)
		{
			var commodity = item.Key;
			professions.Add(commodity, 0);
		}
		//bin professions
        foreach (var agent in agents)
        {
			professions[agent.buildables[0]] += 1;
        }

		foreach (var entry in professions)
		{
			Debug.Log("Profession: " + entry.Key + ": " + entry.Value);
			SetGraph(gProfessions, entry.Key, entry.Value);
		}
	}
	void SetGraph(List<GraphMe> graphs, string commodity, float input)
	{
        if (commodity == "Food") graphs[0].Tick(input);
        if (commodity == "Wood") graphs[1].Tick(input);
        if (commodity == "Ore") graphs[2].Tick(input);
        if (commodity == "Metal") graphs[3].Tick(input);
        if (commodity == "Tool") graphs[4].Tick(input);
    }
}
