using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
using AYellowpaper.SerializedCollections;

public class AuctionHouseTest : AuctionHouse {
	void Start() {
		Debug.unityLogger.logEnabled=config.EnableDebug;
		base.OpenFileForWrite();

		UnityEngine.Random.InitState(config.seed);
		lastTick = 0;
		var com = auctionTracker.book;
	
		config = GetComponent<SimulationConfig>();
		var prefab = Resources.Load("Agent");

		for (int i = transform.childCount; i < config.numAgents.Values.Sum(); i++)
		{
		    GameObject go = Instantiate(prefab) as GameObject;
			go.transform.parent = transform;
			go.name = "agent" + i.ToString();
		}
		
		int agentIndex = 0;
		var professions = config.numAgents.Keys;
		foreach (string profession in professions)
		{
			for (int i = 0; i < config.numAgents[profession]; ++i)
			{
				GameObject child = transform.GetChild(agentIndex).gameObject;
				var agent = child.GetComponent<EconAgent>();
				InitAgent(agent, profession);
				agents.Add(agent);
				++agentIndex;
			}
		}
		askTable = new OfferTable(com);
        bidTable = new OfferTable(com);

		foreach (var entry in com)
		{
			trackBids.Add(entry.Key, new Dictionary<string, float>());
            foreach (var item in com)
			{
				//allow tracking farmers buying food...
				trackBids[entry.Key].Add(item.Key, 0);
			}
		}
	}

	void InitAgent(EconAgent agent, string type)
	{
        List<string> buildables = new List<string>();
		buildables.Add(type);
		float initStock = config.initStock;
		float initCash = config.initCash;
		if (config.randomInitStock)
		{
			initStock = UnityEngine.Random.Range(config.initStock/2, config.initStock*2);
			initStock = Mathf.Floor(initStock);
		}

		// TODO: This may cause uneven maxStock between agents
		var maxStock = Mathf.Max(initStock, config.maxStock);

        agent.Init(config, auctionTracker, buildables, initStock, maxStock);
	}
}
