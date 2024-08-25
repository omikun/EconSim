using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
using AYellowpaper.SerializedCollections;

public class AgentConfig : MonoBehaviour{
	public int seed;
	
	public float initCash = 100;
	public bool randomInitStock = false;
	public float initStock = 10;
	public float maxStock = 20;
	public bool starvation = false;
	public bool foodConsumption = false;
	public bool simpleTradeAmountDet = false;
	public bool onlyBuyWhatsAffordable = false;
	[Tooltip("Use highest bid good vs most demand to supply good")]
	public int historySize = 10;
}