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
	public bool clearInventory = false;
	public bool starvation = false;
	public bool foodConsumption = false;
	public float foodConsumptionRate = 0.1f;
    public float profitMarkup = 1.05f;
    public float idleTaxRate = 0f;
	public bool enablePriceFavorability = false;
	public bool onlyBuyWhatsAffordable = false;
	public int changeProfessionAfterNDays = 10;
	public bool earlyProfessionChange = false;
	[Tooltip("Use highest bid good vs most demand to supply good")]
	public int historySize = 10;
	public void start ()
	{
		//foodConsumption = foodConsumptionRate != 0.0f;
	}
}