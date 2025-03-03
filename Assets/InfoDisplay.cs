using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using Sirenix.OdinInspector;
using System.Linq;
using TMPro;
public class InfoDisplay : MonoBehaviour
{
    //happiness level
    [Required]
    public AuctionStats district;
    [Required]
    public AuctionHouse auctionHouse;
    TextMeshProUGUI text;
    bool updateInfo = false;
    // Start is called before the first frame update
    void Start()
    {
        text = GetComponent<TextMeshProUGUI>();   
    }

    // Update is called once per frame
    int lastRound = -1;
    void LateUpdate()
    {
        if (district.round == lastRound)
        {
            return;
        }
        UpdateValue<float>(ref inflation, district.inflation);
        UpdateValue<float>(ref happiness, district.happiness);
        UpdateValue<int>(ref numLowStock, district.numNoInput);
        UpdateValue<int>(ref numNegProfit, district.numNegProfit);
        UpdateValue<float>(ref gini, district.gini);
        UpdateValue<float>(ref gdp, district.gdp);

        UpdateValue<int>(ref numStarving, district.numStarving);
        UpdateValue<int>(ref numBankrupt, district.numBankrupted);
        UpdateValue<float>(ref govDebt, auctionHouse.gov.Cash);
        //Update
        lastRound = district.round;
        UpdateText();
    }
    void UpdateValue<T>(ref T old, T newNum)
    {
        updateInfo |= old.Equals(newNum);
        old = newNum;
    }

    public float inflation = 0;
    public float happiness = 0;
    public int numStarving = 0;
    int numBankrupt = 0;
    public int numLowStock = 0;
    public int numNegProfit = 0;
    public float gini = 0;
    public float gdp = 0f;
    public float govDebt = 0f;
    public string GetLog(string header)
    {
		string msg = header + "approval, " + happiness.ToString("n2") + ", n/a\n";
		msg += header + "inflation, " + inflation.ToString("n2") + ", n/a\n";
		msg += header + "starving, " + numStarving.ToString("n0") + ", n/a\n";
		msg += header + "unproductive, " + numLowStock.ToString("F0") + ", n/a\n";
		msg += header + "-profit, " + numNegProfit.ToString("n0") + ", n/a\n";
		msg += header + "gini, " + gini.ToString("n2") + ", n/a\n";
		msg += header + "gdp, " + gdp.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + ", n/a\n";
		msg += header + "gov, " + govDebt.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + ", n/a\n";
        return msg;
    }
    public void UpdateText()
    {
        if (true || updateInfo)
        {
            updateInfo = false;
            text.text = "Approval: " + happiness.ToString("P2");
            text.text += "\nInflation: " + inflation.ToString("P2");
            text.text += "\nStarving: " + numStarving.ToString("n0");
            text.text += "\nUnproductive: " + numLowStock.ToString("n0");
            text.text += "\n-Profit: " + numNegProfit.ToString("n0");
            text.text += "\nGini: " + gini.ToString("n2");
            text.text += "\nGDP: " + gdp.ToString("c2");
            text.text += "\nGov: " + govDebt.ToString("c2");
        }
        updateInfo = false;
    }
}
