using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
using AYellowpaper.SerializedCollections;

public class AuctionHouseTest : AuctionHouse {
	void Awake()
	{
		district = GetComponent<AuctionStats>();
		config = GetComponent<SimulationConfig>();
		district.config = config;
		district.Init();
	}
	void Start() {
		Debug.unityLogger.logEnabled=config.EnableDebug;
		logger = new Logger(config);
		logger.OpenFileForWrite();

		UnityEngine.Random.InitState(config.seed);
		lastTick = 0;
		var com = district.book;
	
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

	}

	void InitAgent(EconAgent agent, string type)
	{
		string buildable = type;
		float initStock = config.initStock;
		float initCash = config.initCash;
		if (config.randomInitStock)
		{
			initStock = UnityEngine.Random.Range(config.initStock/2, config.initStock*2);
			initStock = Mathf.Floor(initStock);
		}

		// TODO: This may cause uneven maxStock between agents
		var maxStock = Mathf.Max(initStock, config.maxStock);

        agent.Init(config, district, buildable, initStock, maxStock);
	}
}
