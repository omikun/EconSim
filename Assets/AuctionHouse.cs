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
	public Trade(float p, float q, EconAgent a)
	{
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
		Debug.Log("Trade: " + price + ", " + quantity + ", ");
	}
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
	// Use this for initialization
	void Start () {
		int count = 0;
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
	void Update () {
		Tick();
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
            //shuffle trades
            //var result = asks.OrderBy(item => Random.value);
			asks.Shuffle();
			bids.Shuffle();
			//order trades
			asks.Sort((x, y) => y.price.CompareTo(x.price));
			bids.Sort((x, y) => -y.price.CompareTo(x.price));
            while (asks.Count > 0 && bids.Count > 0)
            {
                //get highest bid and lowest ask
				int askIndex = 0;
				var ask = asks[askIndex];
				int bidIndex = bids.Count - 1;
				var bid = bids[bidIndex];
				//set price
				var sellPrice = (bid.price + ask.price) / 2;
				//trade
				var tradeQuantity = Mathf.Min(bid.quantity, ask.quantity);
				ask.agent.Sell(commodity, tradeQuantity, sellPrice);
				bid.agent.Buy(commodity, tradeQuantity, sellPrice);
                //remove ask/bid if fullfilled
                if (ask.Reduce(tradeQuantity) == 0) { asks.RemoveAt(askIndex); }
				if (bid.Reduce(tradeQuantity) == 0) { bids.RemoveAt(bidIndex); }
				moneyExchanged += sellPrice * tradeQuantity;
				goodsExchanged += tradeQuantity;
            }

			var averagePrice = moneyExchanged/goodsExchanged;
			//calculate supply/demand
			var excessDemand = asks.Sum(ask => ask.quantity);
			var excessSupply = bids.Sum(bid => bid.quantity);
			var demand = (goodsExchanged + excessDemand) 
								 / (goodsExchanged + excessSupply);

            entry.Value.Update(averagePrice, demand);
		}
		//record average prices, volume traded, demand for each commodity
	}
}
