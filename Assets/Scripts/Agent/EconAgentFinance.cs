using UnityEngine;
using UnityEngine.Assertions;

public partial class EconAgent
{
    protected float prevCash;
    protected internal float foodExpense = 0;
    public float Income { get; protected set; }
    public float TaxableProfit { get; protected set; }
    private float taxesPaidThisRound = 0;

    public float PayWealthTax(float amountExempt, float taxRate)
    {
        var taxableAmount = Cash - amountExempt;
        if (taxableAmount <= 0)
            return 0f;
        var tax = taxableAmount * taxRate;
        Cash -= tax;
        taxesPaidThisRound = tax;
        return tax;
    }

    public void Pay(float amount)
    {
        Cash -= amount;
        Assert.IsTrue(Cash >= 0, name + " has minus cash " + Cash.ToString("c2"));
    }

    public void Collect(float amount)
    {
        Cash += amount;
    }

    public float TaxProfit(float taxRate)
    {
        if (TaxableProfit <= 0)
            return 0;
        var taxAmt = TaxableProfit * taxRate;
        Cash -= taxAmt;
        return taxAmt;
    }

    private float losses = 0;

    public void CalculateProfit()
    {
        var prevLosses = losses;
        var delta = Cash - prevCash;
        var prevCash2 = prevCash;
        prevCash = Cash;
        Income = delta;
        var cumDelta = delta + prevLosses;
        losses = Mathf.Min(0, cumDelta);
        TaxableProfit = Mathf.Max(0, cumDelta);
        Debug.Log(auctionStats.round + " income " + name + " prevCash " + prevCash2 + " has " + Cash + " profit this round: " + delta 
                  + " cumulative losses: " + losses
                  + " taxable profit: " + TaxableProfit);
    }

    public void UpdatePrevCash()
    {
        prevCash = Cash;
    }

    public void AddToCash(float quant)
    {
        Cash += quant;
    }
}