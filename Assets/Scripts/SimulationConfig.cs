using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
using AYellowpaper.SerializedCollections;
using Sirenix.OdinInspector;
using EconSim;
using UnityEngine.Serialization;

namespace EconSim
{
	public enum TradeResolutionType
	{
		XEven,
		OmiType,
		SimonType,
	}
    public enum OfferSortOrder
    {
        Ascending,
        Descending,
        Random
    }
    public enum OfferSortBy
    {
        OfferPrice
        //, QualityOfLife
    }
    public enum ResolveTradePrice
    {
        TakeAskPrice,
        TakeBidPrice,
        TakeAveragePrice,
    }

    public enum AgentType
    {
	    Default,
	    Simple,
	    Medium,
	    User,
    }
    public enum AgentProduction
    {
        FixedRate,
        DemandDriven,
        MaxedOut,
    }
    public enum AgentSellRate
    {
        FixedRate,
        DemandDriven,
        MaxedOut,
    }
    public enum AgentSellPrice
    {
        FixedPrice,
        AtCost,
        MarketAverage,
        FixedProfit,
        DemandBased,
    }

    public enum AgentConsumption
    {
        FixedRate,
        Gluttonous,
        MinimumSurvival,
        Opportunistic,
    }

    public enum AgentBuyPrice
    {
        MarketPrice,
        SupplyBased,
        QoLBased,
    }

    public enum ConsumerType
    {
	    Default,
	    SanityCheck,
	    QoLBased,
    }
}
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
	public bool EnableGovernment = true;
	[BoxGroup("Simulation Settings/Split/Right/Box C")]
	[LabelWidth(150)]
	public int seed = 42;
	[BoxGroup("Simulation Settings/Split/Right/Box C")]
	[LabelWidth(150)]
	public int maxRounds = 10;
	[BoxGroup("Simulation Settings/Split/Right/Box C")]
	[LabelWidth(150)]
	public int numRoundsNoTrade = 100;
	
	//init conditions
	[FormerlySerializedAs("bidSortType")]
	[TabGroup("Auction Trade")]
	public TradeResolutionType tradeResolution = TradeResolutionType.XEven;
	[TabGroup("Auction Trade")]
	public OfferSortOrder bidSortOrder = OfferSortOrder.Ascending;
	[TabGroup("Auction Trade")]
	public OfferSortBy bidSortBy = OfferSortBy.OfferPrice;
	[FormerlySerializedAs("askSortType")]
	[TabGroup("Auction Trade")]
	public OfferSortOrder askSortOrder = OfferSortOrder.Ascending;
	[TabGroup("Auction Trade")]
	public OfferSortBy askSortBy = OfferSortBy.OfferPrice;
	[TabGroup("Auction Trade")] 
	public ResolveTradePrice resolveTradePrice = ResolveTradePrice.TakeAveragePrice;
	
	
	[TabGroup("Taxes")]
    public float idleTaxRate = 0f;
	[TabGroup("Taxes")]
    public bool EnableSalesTax = false;
	[TabGroup("Taxes")]
    [ShowInInspector, DictionaryDrawerSettings(DisplayMode = DictionaryDisplayOptions.OneLine, KeyLabel = "Comm", ValueLabel = "TaxRate")]
    [SerializedDictionary("Comm", "TaxRate")]
    public SerializedDictionary<string, float> SalesTaxRate = new();

	[TabGroup("Taxes")] 
	public bool GovWelfare = true;
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
	public AgentType agentType = AgentType.Default;
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
	public int maxDaysStarving = 3;
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

	[TabGroup("tab2", "Agent Trade")] 
	public AgentProduction productionRate = AgentProduction.FixedRate;
	[TabGroup("tab2", "Agent Trade")] 
	public ConsumerType consumerType = ConsumerType.Default;
	[TabGroup("tab2", "Agent Trade")] 
	public AgentSellRate sellRate = AgentSellRate.FixedRate;
	[TabGroup("tab2", "Agent Trade")] 
	public AgentSellPrice sellPrice = AgentSellPrice.AtCost;
	[TabGroup("tab2", "Agent Trade")] 
	public AgentConsumption consumeRate = AgentConsumption.FixedRate;
	[TabGroup("tab2", "Agent Trade")] 
	public AgentBuyPrice buyPrice = AgentBuyPrice.MarketPrice;
	[InfoBox("Avg bid/ask price; offer price random delta around mkt price")]
	[TabGroup("tab2", "Agent Trade")] 
	public bool randomizeSellPrice = false;

	[TabGroup("tab2", "Agent Trade")] public bool sellPriceMinFoodExpense = true;
	[FormerlySerializedAs("baselineSellPriceMinCost")] [TabGroup("tab2", "Agent Trade")] 
	public bool sellPriceMinCost = false;

	[FormerlySerializedAs("baselineSellPriceDelta")] [TabGroup("tab2", "Agent Trade")]
	public bool minSellPrice = true;
	[InfoBox("priced to afford 1 of every other rsc after selling this many output")]
	[FormerlySerializedAs("baselineSellPriceDelta")] [TabGroup("tab2", "Agent Trade")]
	public float minSellToAffordOthers = 10f;
	[FormerlySerializedAs("baselineSellPriceDelta")] [TabGroup("tab2", "Agent Trade")]
	public float minItemRaiseBuyPrice = 3f;
	[FormerlySerializedAs("baselineSellPriceDelta")] [TabGroup("tab2", "Agent Trade")] 
	public float sellPriceDelta = 0.05f;
	[FormerlySerializedAs("baselineBuyPriceDelta")] [TabGroup("tab2", "Agent Trade")] 
	public float buyPriceDelta = 0.05f;
	[TabGroup("tab2", "Agent Trade")] 
    public float profitMarkup = 1.05f;
	[TabGroup("tab2", "Agent Trade")] 
	[InfoBox("Price and trade volume should remain constant")]
	public float sanityCheckTradeVolume = 1f;
	[TabGroup("tab2", "Agent Trade")] 
	public bool sanityCheckSellQuant = false; 
	[TabGroup("tab2", "Agent Trade")] 
	//[OnValueChanged(nameof(ResetSanityCheck))]
	[InfoBox("Buy quant varies with delta relative to historic average price")]
	public bool enablePriceFavorability = false;
	[TabGroup("tab2", "Agent Trade")] 
	public bool onlyBuyWhatsAffordable = false;
	

	public void start ()
	{
		//foodConsumption = foodConsumptionRate != 0.0f;
	}
}