using System;
using System.Linq;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

public class CommodityInfo : MonoBehaviour
{
    [Required] 
    public AuctionStats district;
    [Required] 
    public AuctionHouse auctionHouse;

    private TextMeshProUGUI text;
    
    void Start()
    {
        text = GetComponent<TextMeshProUGUI>();   
    }
    int lastRound = -1;
    void LateUpdate()
    {
        if (district.round == lastRound)
        {
            return;
        }
        lastRound = district.round;
        UpdateText();
    }

    void UpdateText()
    {
        string msg = "";
        maxLength = 0;
        maxLength2 = 0;
        maxLength3 = 0;
        foreach (var (com, rsc) in district.book)
        {
            var price = rsc.marketPriceString;
            var asks = rsc.asks.Last();
            var bids = rsc.bids.Last();
            var trades = rsc.trades.Last();
            var tmp = postPad(com + ":", ref maxLength) + postPad(price, ref maxLength2) + asks + "/" + postPad(bids.ToString(), ref maxLength3) + trades + "\n";
        }
        foreach (var (com, rsc) in district.book)
        {
            var price = rsc.marketPriceString;
            var asks = rsc.asks.Last();
            var bids = rsc.bids.Last();
            var trades = rsc.trades.Last();
            msg += postPad(com + ":", ref maxLength) + postPad(price, ref maxLength2) + asks + "/" + postPad(bids.ToString(), ref maxLength3) + trades + "\n";
        }

        text.text = msg;
    }

    public int maxLength = 0;
    public int maxLength2 = 0;
    public int maxLength3 = 0;

    string postPad(string s, ref int maxLength)
    {
        int slength = s.Length - 1;
        maxLength = (maxLength > slength) ? maxLength : slength;
        var space = " ";
        for (int i = 0; i < maxLength - slength; i++)
        {
            space += space;
        }

        return s + space + "\t";
    }

    string pad(float value, ref int max)
    {
        return pad(value.ToString(), ref max);
    }
    string pad(string s, ref int max)
    {
        return " \t" + s;
    }
}