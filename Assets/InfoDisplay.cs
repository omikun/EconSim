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
    public AuctionHouse district;
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
        if (AuctionStats.Instance.round == lastRound)
        {
            return;
        }
        UpdateHappiness();
        UpdateLowStock();
        UpdateNegativeProfit();
        UpdateGini();
        UpdateGDP();
        //Update
        lastRound = AuctionStats.Instance.round;
        UpdateText();
    }
    public float happiness = 0;
    void UpdateHappiness()
    {
        float newNum = district.GetHappiness();
        updateInfo |= happiness != newNum;
        happiness = newNum;
    }
    public float numLowStock = 0;
    void UpdateLowStock()
    {
        float newNum = district.GetLowStock();
        updateInfo |= (newNum != numLowStock);
        numLowStock = newNum;
    }
    public float losingMoney = 0;
    void UpdateNegativeProfit()
    {
        float newNum = district.GetNegativeProfit();
        updateInfo |= (newNum != numLowStock);
        losingMoney = newNum;
    }
    public float gini = 0;
    void UpdateGini()
    {
        var cashList = district.GetWealthOfAgents();
        cashList.Sort();
        // string msg = ListUtil.ListToString(cashList, "c2");
        // Debug.Log("cash: " + msg);
        int n = cashList.Count;
        if (n == 0) return;

        float totalWealth = cashList.Sum();
        Assert.IsTrue(totalWealth != 0);
        float cumulativeWealth = 0;
        float weightedSum = 0;
        for (int i = 0; i < n; i++)
        {
            cumulativeWealth += cashList[i];
            weightedSum += (i + 1) * cashList[i];
        }

        // Gini coefficient formula
        float newNum = (2.0f * weightedSum) / (n * totalWealth) - (n + 1.0f) / n;
        Assert.IsFalse(float.IsNaN(newNum));
        updateInfo |= (newNum != gini);
        gini = newNum;
    }
    public float gdp = 0f;
    void UpdateGDP()
    {
        float newNum = district.GetGDP();
        updateInfo |= (newNum != gdp);
        gdp = newNum;

    }
    public string GetLog(string header)
    {
		string msg = header + "approval, " + happiness.ToString("n2") + ", n/a\n";
		msg += header + "unproductive, " + numLowStock.ToString("F0") + ", n/a\n";
		msg += header + "-profit, " + losingMoney.ToString("n0") + ", n/a\n";
		msg += header + "gini, " + gini.ToString("n2") + ", n/a\n";
		msg += header + "gdp, " + gdp.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + ", n/a\n";
        return msg;
    }
    public void UpdateText()
    {
        if (true || updateInfo)
        {
            updateInfo = false;
            text.text = "Approval: " + happiness.ToString("P2");
            text.text += "\nUnproductive: " + numLowStock.ToString("n0");
            text.text += "\n-Profit: " + losingMoney.ToString("n0");
            text.text += "\nGini: " + gini.ToString("n2");
            text.text += "\nGDP: " + gdp.ToString("c2");
        }
        updateInfo = false;
    }
}
