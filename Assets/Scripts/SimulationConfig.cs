using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
using AYellowpaper.SerializedCollections;
using Sirenix.OdinInspector;
using EconSim;
using Sirenix.Serialization;
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
	[Tooltip("for looking into recent past on different metrics, like most profitable good")]
	public int historySize = 10;
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
	[TabGroup("multi row", "Auction Trade",  TextColor = "blue")]
	public TradeResolutionType tradeResolution = TradeResolutionType.XEven;
	[TabGroup("multi row", "Auction Trade",  TextColor = "blue")]
	public OfferSortOrder bidSortOrder = OfferSortOrder.Ascending;
	[TabGroup("multi row", "Auction Trade",  TextColor = "blue")]
	public OfferSortBy bidSortBy = OfferSortBy.OfferPrice;
	[TabGroup("multi row", "Auction Trade",  TextColor = "blue")]
	public OfferSortOrder askSortOrder = OfferSortOrder.Ascending;
	[TabGroup("multi row", "Auction Trade",  TextColor = "blue")]
	public OfferSortBy askSortBy = OfferSortBy.OfferPrice;
	[TabGroup("multi row", "Auction Trade",  TextColor = "blue")] 
	public ResolveTradePrice resolveTradePrice = ResolveTradePrice.TakeAveragePrice;
	
	
	[TabGroup("multi row", "Gov Controls",  TextColor = "blue")]
    public float idleTaxRate = 0f;
    
	[TabGroup("multi row", "Gov Controls",  TextColor = "blue")]
    public bool EnableSalesTax = false;
	[TabGroup("multi row", "Gov Controls",  TextColor = "blue")]
    [ShowInInspector, DictionaryDrawerSettings(DisplayMode = DictionaryDisplayOptions.OneLine, KeyLabel = "Comm", ValueLabel = "TaxRate")]
    [SerializedDictionary("Comm", "TaxRate")]
    public SerializedDictionary<string, float> SalesTaxRate = new();
    
	[TabGroup("multi row", "Gov Controls",  TextColor = "blue")]
    [InfoBox("Marginal Income Tax", "@!EnableIncomeTax")]
    public bool EnableIncomeTax = true;
	[TabGroup("multi row", "Gov Controls",  TextColor = "blue")]
    public List<TaxBracket> taxBrackets = new()
    {
	    { new(1, 5, .1f) },
	    { new(5, 10, .2f) },
	    { new(10, 100, .5f) }
    };
    
	[TabGroup("multi row", "Gov Controls",  TextColor = "blue")]
    public bool EnableSubsidies = false;
	[TabGroup("multi row", "Gov Controls",  TextColor = "blue")]
    public SerializedDictionary<string, float> SubsidiesRate = new();

	[TabGroup("multi row", "Gov Controls",  TextColor = "blue")] 
	public bool EnableReserve = true;
	[TabGroup("multi row", "Gov Controls",  TextColor = "blue")] 
	public SerializedDictionary<string, float> Reserves = new();

	[TabGroup("multi row", "Gov Controls",  TextColor = "blue")] 
	public bool GovWelfare = true;
	
    [TabGroup("multi row", "Respawn",  TextColor = "blue")]
	[InfoBox("Enable respawn on starvation")]
	public bool starvation = false;
	[TabGroup("multi row", "Respawn",  TextColor = "blue")]
	[InfoBox("clear inventory on changeProfession")]
	public bool clearInventory = false;
	[TabGroup("multi row", "Respawn",  TextColor = "blue")]
	public bool changeProfession = true;
	[TabGroup("multi row", "Respawn",  TextColor = "blue")]
	public bool earlyProfessionChange = false;
	[TabGroup("multi row", "Respawn",  TextColor = "blue")]
	public int changeProfessionAfterNDays = 10;
	[TabGroup("multi row", "Respawn",  TextColor = "blue")]
	public bool declareBankruptcy = true;

	[TabGroup("multi row", "Banking",  TextColor = "orange")] public float fractionalReserveRatio = 0.1f;
	[TabGroup("multi row", "Banking", TextColor = "orange")] public int termInRounds = 30;
	[TabGroup("multi row", "Banking", TextColor = "orange")] public float interestRate = 0.02f;
	[TabGroup("multi row", "Banking", TextColor = "orange")] public int maxMissedPayments = 5;
	[TabGroup("multi row", "Banking", TextColor = "orange")] public float maxPrinciple = 500f;
	[TabGroup("multi row", "Banking", TextColor = "orange")] public int maxNumDefaults = 5;
	
	[TabGroup("multi row", "Agent Initialization", TextColor = "orange")] public AgentType agentType = AgentType.Default;
	[TabGroup("multi row", "Agent Initialization", TextColor = "orange")] public float initCash = 100;
	[TabGroup("multi row", "Agent Initialization", TextColor = "orange")] public float initGovCash = 1000;
	[TabGroup("multi row", "Agent Initialization", TextColor = "orange")] public bool randomInitStock = false;
	[TabGroup("multi row", "Agent Initialization", TextColor = "orange")] public float initStock = 10;
	[TabGroup("multi row", "Agent Initialization", TextColor = "orange")] public float maxStock = 20;
	[TabGroup("multi row", "Agent Initialization", TextColor = "orange")]
	[SerializedDictionary("Comm", "numAgents")]
	public SerializedDictionary<string, int> numAgents = new()
	{
		{ "Food", 3 },
		{ "Wood", 3 },
		{ "Ore", 3 },
		{ "Metal", 4 },
		{ "Tool", 4 }
	};
	[TabGroup("multi row", "Agent Initialization", TextColor = "orange")]
	[SerializedDictionary("ID", "Recipe")]
	public SerializedDictionary<string, SerializedDictionary<string, float>> initialization = new();
	
	[TabGroup("multi row", "Agent FoodConsumption", TextColor = "orange")]
	public float starvationThreshold = 0.1f;
	[TabGroup("multi row", "Agent FoodConsumption", TextColor = "orange")]
	public int maxDaysStarving = 3;
	[TabGroup("multi row", "Agent FoodConsumption", TextColor = "orange")]
	public bool foodConsumption = false;
	[TabGroup("multi row", "Agent FoodConsumption", TextColor = "orange")]
	public float foodConsumptionRate = 0.1f;
	[TabGroup("multi row", "Agent FoodConsumption", TextColor = "orange")]
	public bool useFoodConsumptionCurve = true;
	[Required]
	[TabGroup("multi row", "Agent FoodConsumption", TextColor = "orange")]
	public AnimationCurve foodConsumptionCurve;
	[TabGroup("multi row", "Agent FoodConsumption", TextColor = "orange")]
	public float numFoodHappy = 10f;

	[TabGroup("multi row", "Agent Trade", TextColor = "orange")] 
	public AgentProduction productionRate = AgentProduction.FixedRate;
	[TabGroup("multi row", "Agent Trade", TextColor = "orange")] 
	public ConsumerType consumerType = ConsumerType.Default;
	[TabGroup("multi row", "Agent Trade", TextColor = "orange")] 
	public AgentSellRate sellRate = AgentSellRate.FixedRate;
	[TabGroup("multi row", "Agent Trade", TextColor = "orange")] 
	public AgentSellPrice sellPrice = AgentSellPrice.AtCost;
	[TabGroup("multi row", "Agent Trade", TextColor = "orange")] 
	public AgentConsumption consumeRate = AgentConsumption.FixedRate;
	[TabGroup("multi row", "Agent Trade", TextColor = "orange")] 
	public AgentBuyPrice buyPrice = AgentBuyPrice.MarketPrice;
	[InfoBox("Avg bid/ask price; offer price random delta around mkt price")]
	[TabGroup("multi row", "Agent Trade", TextColor = "orange")] 
	public bool randomizeSellPrice = false;

	[TabGroup("multi row", "Agent Trade", TextColor = "orange")] public bool sellPriceMinFoodExpense = true;
	[TabGroup("multi row", "Agent Trade", TextColor = "orange")] 
	public bool sellPriceMinCost = false;

	[TabGroup("multi row", "Agent Trade", TextColor = "orange")]
	public bool minSellPrice = true;
	[InfoBox("priced to afford 1 of every other rsc after selling this many output")]
	[TabGroup("multi row", "Agent Trade", TextColor = "orange")]
	public float minSellToAffordOthers = 10f;
	[TabGroup("multi row", "Agent Trade", TextColor = "orange")]
	public float minItemRaiseBuyPrice = 3f;
	[TabGroup("multi row", "Agent Trade", TextColor = "orange")] 
	public float sellPriceDelta = 0.05f;
	[TabGroup("multi row", "Agent Trade", TextColor = "orange")] 
	public float buyPriceDelta = 0.05f;
	[TabGroup("multi row", "Agent Trade", TextColor = "orange")] 
    public float profitMarkup = 1.05f;
	[TabGroup("multi row", "Agent Trade", TextColor = "orange")] 
	[InfoBox("Price and trade volume should remain constant")]
	public float sanityCheckTradeVolume = 1f;
	[TabGroup("multi row", "Agent Trade", TextColor = "orange")] 
	public bool sanityCheckSellQuant = false; 
	[TabGroup("multi row", "Agent Trade", TextColor = "orange")] 
	//[OnValueChanged(nameof(ResetSanityCheck))]
	[InfoBox("Buy quant varies with delta relative to historic average price")]
	public bool enablePriceFavorability = false;
	[TabGroup("multi row", "Agent Trade", TextColor = "orange")] 
	public bool onlyBuyWhatsAffordable = false;
	

	public void start ()
	{
		//foodConsumption = foodConsumptionRate != 0.0f;
	}
}