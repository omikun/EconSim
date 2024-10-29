using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
using AYellowpaper.SerializedCollections;
using Sirenix.OdinInspector;

public class AgentConfig : MonoBehaviour{
	public int seed;
	
	//init conditions
	[TabGroup("Agent Initialization")]
	public float initCash = 100;
	[TabGroup("Agent Initialization")]
	public bool randomInitStock = false;
	[TabGroup("Agent Initialization")]
	public float initStock = 10;
	[TabGroup("Agent Initialization")]
	public float maxStock = 20;
	[TabGroup("Agent FoodConsumption")]
	public float starvationThreshold = 0.1f;
	[TabGroup("Agent FoodConsumption")]
	public bool foodConsumption = false;
	[TabGroup("Agent FoodConsumption")]
	public float foodConsumptionRate = 0.1f;
	[TabGroup("Agent FoodConsumption")]
	public bool useFoodConsumptionCurve = true;
	[Required]
	[TabGroup("Agent FoodConsumption")]
	public AnimationCurve foodConsumptionCurve;
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