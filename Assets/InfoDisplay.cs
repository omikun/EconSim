using System.Collections;
using System.Collections.Generic;
using UnityEngine;
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
        //Update
        lastRound = AuctionStats.Instance.round;
        UpdateText();
    }
    float happiness = 0;
    void UpdateHappiness()
    {
        float newNum = district.GetHappiness();
        updateInfo = happiness != newNum;
        happiness = newNum;
    }
    float numLowStock = 0;
    void UpdateLowStock()
    {
        float newNum = district.GetLowStock();
        updateInfo = (newNum != numLowStock);
        numLowStock = newNum;
    }
    float losingMoney = 0;
    void UpdateNegativeProfit()
    {
        float newNum = district.GetNegativeProfit();
        updateInfo = (newNum != numLowStock);
        losingMoney = newNum;
    }
    float gini = 0;
    void UpdateGini()
    {
        var cashList = district.GetWealthOfAgents();
        cashList.Sort();
        // string msg = ListUtil.ListToString(cashList, "c2");
        // Debug.Log("cash: " + msg);
        int n = cashList.Count;
        if (n == 0) return;

        float totalWealth = cashList.Sum();
        // float meanWealth = sortedWealth.Average();
        // double sumOfDifferences = 0;
        // for (int i = 0; i < n; i++)
        // {
        //     for (int j = 0; j < n; j++)
        //     {
        //         sumOfDifferences += Math.Abs(sortedWealth[i] - sortedWealth[j]);
        //     }
        // }

        // // Calculate Gini coefficient
        // double gini = sumOfDifferences / (2 * n * n * meanWealth);
        // return gini;
        float cumulativeWealth = 0;
        float weightedSum = 0;
        for (int i = 0; i < n; i++)
        {
            cumulativeWealth += cashList[i];
            weightedSum += (i + 1) * cashList[i];
        }

        // Gini coefficient formula
        float newNum = (2.0f * weightedSum) / (n * totalWealth) - (n + 1.0f) / n;
        updateInfo = (newNum != numLowStock);
        gini = newNum;
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
        }
    }
}
