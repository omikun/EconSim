using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
using AYellowpaper.SerializedCollections;
using Sirenix.OdinInspector;

public class SimulationConfig : MonoBehaviour{
	
	public bool autoNextRound = false;
	//[CustomValueDrawer("TickIntervalDrawer")]
	[Range(.001f, 2f)]
    public float tickInterval = .001f;
	[TitleGroup("Simulation Settings")]
	[HorizontalGroup("Simulation Settings/Split")]
	[VerticalGroup("Simulation Settings/Split/Left")]
	[BoxGroup("Simulation Settings/Split/Left/Box A", false)]
	[OnValueChanged(nameof(OnToggleEnableDebug))]
	[LabelWidth(150)]
	public bool EnableDebug = false;
	private void OnToggleEnableDebug()
	{
		Debug.unityLogger.logEnabled=EnableDebug;
	}
	[BoxGroup("Simulation Settings/Split/Left/Box A")]
	[LabelWidth(150)]
	public bool EnableLog = false;
	[BoxGroup("Simulation Settings/Split/Left/Box A")]
	[LabelWidth(150)]
	public bool appendTimeToLog = false;
	[BoxGroup("Simulation Settings/Split/Left/Box A")]
	[LabelWidth(150)]
	public bool exitAfterNoTrade = true;
	[VerticalGroup("Simulation Settings/Split/Right")]
	[BoxGroup("Simulation Settings/Split/Right/Box C", false)]
	[LabelWidth(150)]
	public int seed = 42;
	[BoxGroup("Simulation Settings/Split/Right/Box C")]
	[LabelWidth(150)]
	public int maxRounds = 10;
	[BoxGroup("Simulation Settings/Split/Right/Box C")]
	[LabelWidth(150)]
	public int numRoundsNoTrade = 100;
	
	//init conditions
	[TabGroup("Auction Trade")]
	[InfoBox("Avg bid/ask price; offer price random delta around mkt price")]
	public bool baselineAuction = false; 
	[TabGroup("Auction Trade")]
	public bool baselineSellPrice = false; 
	[TabGroup("Auction Trade")]
	public bool baselineSellPriceMinCost = false; 
	[TabGroup("Auction Trade")]
	public bool baselineBuyPrice = false; 
	[TabGroup("Auction Trade")]
	public float baselineSellPriceDelta = 0.05f;
	[TabGroup("Auction Trade")]
	public float baselineBuyPriceDelta = 0.05f;
	[TabGroup("Auction Trade")]
    public float profitMarkup = 1.05f;
	[TabGroup("Auction Trade")]
	[InfoBox("Price and trade volume should remain constant")]
	[OnValueChanged(nameof(OnToggleSanityCheck))]
	public bool sanityCheck = false; 
	[TabGroup("Auction Trade")]
	public float sanityCheckTradeVolume = 1f;
	[TabGroup("Auction Trade")]
	[OnValueChanged(nameof(ResetSanityCheck))]
	public bool sanityCheckSellQuant = false; 
	[TabGroup("Auction Trade")]
	[OnValueChanged(nameof(ResetSanityCheck))]
	public bool sanityCheckBuyQuant = false; 
	[TabGroup("Auction Trade")]
	[OnValueChanged(nameof(ResetSanityCheck))]
	public bool sanityCheckSellPrice = false; 
	[TabGroup("Auction Trade")]
	[OnValueChanged(nameof(ResetSanityCheck))]
	public bool sanityCheckBuyPrice = false; 
	[TabGroup("Auction Trade")]
	[InfoBox("Buy quant varies with delta relative to historic average price")]
	public bool enablePriceFavorability = false;
	[TabGroup("Auction Trade")]
	public bool onlyBuyWhatsAffordable = false;
	
	[TabGroup("Taxes")]
    public float idleTaxRate = 0f;
	[TabGroup("Taxes")]
    public bool EnableSalesTax = false;
	[TabGroup("Taxes")]
    [ShowInInspector, DictionaryDrawerSettings(DisplayMode = DictionaryDisplayOptions.OneLine, KeyLabel = "Comm", ValueLabel = "TaxRate")]
    [SerializedDictionary("Comm", "TaxRate")]
    public SerializedDictionary<string, float> SalesTaxRate = new();
	[TabGroup("Taxes")]
    [ShowInInspector, DictionaryDrawerSettings(DisplayMode = DictionaryDisplayOptions.OneLine, KeyLabel = "Comm", ValueLabel = "Subsidy")]
    [SerializedDictionary("Comm", "Subsidy")]
    public SerializedDictionary<string, float> Subsidy = new();
	
    [TabGroup("Respawn")]
	[InfoBox("Enable respawn on starvation")]
	public bool starvation = false;
	[TabGroup("Respawn")]
	[InfoBox("clear inventory on changeProfession")]
	public bool clearInventory = false;
	[TabGroup("Respawn")]
	public bool changeProfession = true;
	[TabGroup("Respawn")]
	public bool earlyProfessionChange = false;
	[TabGroup("Respawn")]
	public int changeProfessionAfterNDays = 10;
	[TabGroup("Respawn")]
	public bool declareBankruptcy = true;
	[Tooltip("for looking into recent past on different metrics, like most profitable good")]
	public int historySize = 10;
	
	[TabGroup("tab2", "Agent Initialization")]
	public float initCash = 100;
	[TabGroup("tab2", "Agent Initialization")]
	public float initGovCash = 1000;
	[TabGroup("tab2", "Agent Initialization")]
	public bool randomInitStock = false;
	[TabGroup("tab2", "Agent Initialization")]
	public float initStock = 10;
	[TabGroup("tab2", "Agent Initialization")]
	public float maxStock = 20;
	[TabGroup("tab2", "Agent Initialization")]
	[SerializedDictionary("Comm", "numAgents")]
	public SerializedDictionary<string, int> numAgents = new()
	{
		{ "Food", 3 },
		{ "Wood", 3 },
		{ "Ore", 3 },
		{ "Metal", 4 },
		{ "Tool", 4 }
	};
	[TabGroup("tab2", "Agent Initialization")]
	[SerializedDictionary("ID", "Recipe")]
	public SerializedDictionary<string, SerializedDictionary<string, float>> initialization = new();
	
	[TabGroup("tab2", "Agent FoodConsumption")]
	public float starvationThreshold = 0.1f;
	[TabGroup("tab2", "Agent FoodConsumption")]
	public bool foodConsumption = false;
	[TabGroup("tab2", "Agent FoodConsumption")]
	public float foodConsumptionRate = 0.1f;
	[TabGroup("tab2", "Agent FoodConsumption")]
	public bool useFoodConsumptionCurve = true;
	[Required]
	[TabGroup("tab2", "Agent FoodConsumption")]
	public AnimationCurve foodConsumptionCurve;
	[TabGroup("tab2", "Agent FoodConsumption")]
	public float numFoodHappy = 10f;
	
	private void OnToggleBaselineAuction()
	{
		if (baselineAuction)
		{
			sanityCheck = false;
			OnToggleSanityCheck();
		}
	}
	private void OnToggleSanityCheck()
	{
		sanityCheckSellQuant = sanityCheck;
		sanityCheckSellPrice = sanityCheck;
		sanityCheckBuyQuant = sanityCheck;
		sanityCheckBuyPrice = sanityCheck;
	}

	private void ResetSanityCheck()
	{
		sanityCheck = sanityCheckSellQuant && sanityCheckSellPrice && sanityCheckBuyQuant && sanityCheckBuyPrice;
	}
	public void start ()
	{
		//foodConsumption = foodConsumptionRate != 0.0f;
	}
}