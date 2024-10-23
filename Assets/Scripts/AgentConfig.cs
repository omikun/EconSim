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
	
	public float initCash = 100;
	public bool randomInitStock = false;
	public float initStock = 10;
	public float maxStock = 20;
	public bool clearInventory = false;
	public bool starvation = false;
	public bool foodConsumption = false;
	public float foodConsumptionRate = 0.1f;
	[InfoBox("Avg bid/ask price; offer price random delta around mkt price")]
	public bool baselineAuction = false; 
	[OnValueChanged(nameof(OnToggleSanityCheck))]
	public bool sanityCheck = false; 
	public bool sanityCheckSellQuant = false; 
	public bool sanityCheckSellPrice = false; 
	public bool sanityCheckBuyQuant = false; 
	public bool sanityCheckBuyPrice = false; 

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
	public bool useFoodConsumptionCurve = true;
	[Required]
	public AnimationCurve foodConsumptionCurve;
    public float profitMarkup = 1.05f;
    public float idleTaxRate = 0f;
    public bool EnableSalesTax = false;
    public float SalesTaxRate = .1f;
	public bool enablePriceFavorability = false;
	public bool onlyBuyWhatsAffordable = false;
	public bool changeProfession = true;
	public int changeProfessionAfterNDays = 10;
	public bool earlyProfessionChange = false;
	public bool declareBankruptcy = true;
	[Tooltip("Use highest bid good vs most demand to supply good")]
	public int historySize = 10;
	public void start ()
	{
		//foodConsumption = foodConsumptionRate != 0.0f;
	}
}