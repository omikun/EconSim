// Fiscal policy addresses taxation and government spending, and it is generally determined by government legislation.

using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using JetBrains.Annotations;
using UnityEditor.VersionControl;
using UnityEngine;
using UnityEditor;
using AYellowpaper.SerializedCollections;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using Sirenix.Serialization;
using UnityEngine.Assertions;

public class FiscalPolicy 
{
    protected AgentConfig config;
    protected AuctionStats auctionStats;
    public float taxed = 0;
    public Government gov;
    public FiscalPolicy(AgentConfig cfg, AuctionStats at, Government g)
    {
        config = cfg;
        auctionStats = at;
        gov = g;
    }
    public virtual void Tax(AuctionBook book, List<EconAgent> agents)
    {
        //tax wealth per round?
        //tax income?
        //tax profit?
        //exception?
        foreach (var agent in agents)
        {
			if (agent is Government)
				continue;
            ApplyTax(book, agent);
        }
    }
    public virtual void ApplyTax(AuctionBook book, EconAgent agents)
    {
        //do nothing
    }
}
[Serializable]
[HideLabel]
public class Range
{
    [HorizontalGroup("Range1")]
    [HideLabel]
    public float min = 0;

    [HorizontalGroup("Range1")]
    [HideLabel]
    public float max = 1;

    // Constructor for initializing Range values
    public Range(float min, float max)
    {
        this.min = min;
        this.max = max;
    }

    // Override ToString for better display in the dictionary keys
    public override string ToString()
    {
        return $"[{min}, {max}]";
    }
}

[Serializable]
public class FlatTaxPolicy : FiscalPolicy
{
    public FlatTaxPolicy(AgentConfig cfg, AuctionStats at, Government g) : base(cfg, at, g) {}
}

[Serializable]
public class ProgressivePolicy : FiscalPolicy 
{
    /*
    Lower tax rates: Especially for high-income earners and corporations, based on the belief that this stimulates economic growth.
Flatter tax structures: Reducing the number of tax brackets and the progressivity of the tax system.
Broader tax base: Eliminating many deductions and loopholes while lowering overall rates.
Consumption taxes: Greater reliance on sales taxes, VAT, or other consumption-based taxes rather than income taxes.
Capital gains tax reductions: Lower taxes on investment income to encourage savings and investment.
Corporate tax cuts: Reducing corporate tax rates to attract businesses and encourage economic activity.
Privatization: Selling state-owned enterprises and reducing government services, which can lower the need for tax revenue.
Reduction of social welfare spending: Cutting back on social programs, which allows for lower overall taxation.
    */
    // [InfoBox("Tax fraction of wealth per idle round", "@!EnableIdleWealthTax"),OnValueChanged(nameof(OnEnableIdleTax))]
    public bool EnableIdleWealthTax = false;
    // [InfoBox("Tax fraction of wealth per round", "@!EnableWealthTax"),OnValueChanged(nameof(OnEnableWealthTax))]
    public bool EnableWealthTax = true;

    private void OnEnableWealthTax()
    {
        if (EnableWealthTax)
            EnableIdleWealthTax = false;
    }
    private void OnEnableIdleTax()
    {
        if (EnableIdleWealthTax)
            EnableWealthTax = false;
    }
    //[ShowIf("@EnableWealthTax || EnableIdleWealthTax")]
    public float WealthTaxRate = .3f;
    public float MinWealthTaxExempt = 50f;

    [InfoBox("Marginal Income Tax", "@!EnableIncomeTax")]
    public bool EnableIncomeTax = true;

    [ShowInInspector, DictionaryDrawerSettings(DisplayMode = DictionaryDisplayOptions.OneLine, KeyLabel = "Income Bracket", ValueLabel = "Marginal Tax Rate")]
    [SerializedDictionary("Income Bracket", "Marginal Tax Rate")]
    public SerializedDictionary<Range, float> taxBracket = new();
    public ProgressivePolicy(AgentConfig cfg, AuctionStats at, Government g) : base(cfg, at, g)
    {
    }
    public override void Tax(AuctionBook book, List<EconAgent> agents)
    {
        foreach (var agent in agents)
        {
			if (agent is Government)
				continue;
            if (EnableIdleWealthTax) applyIdleWealthTax(book, agent);
            if (EnableWealthTax) applyWealthTax(book, agent);
            if (EnableIncomeTax) applyIncomeTax(book, agent);
        }
    }
    public override void ApplyTax(AuctionBook book, EconAgent agent)
    {
        base.ApplyTax(book, agent);
        applyIdleWealthTax(book, agent);
    }
    void applyWealthTax(AuctionBook book, EconAgent agent)
    {
        float wealthTax = agent.PayWealthTax(MinWealthTaxExempt, WealthTaxRate);
        Assert.IsTrue(wealthTax >= 0);
        gov.Pay(-wealthTax);
        taxed += wealthTax;
        //Utilities.TransferQuantity(idleTax, agent, irs);
        Debug.Log(auctionStats.round + " " + agent.name + " has "
            + agent.cash.ToString("c2") + "wealth taxed " + wealthTax.ToString("c2"));
    }
    void applyIdleWealthTax(AuctionBook book, EconAgent agent)
    {
        var numProduced = agent.numProducedThisRound;
        if (numProduced > 0) return;

        float idleTax = agent.PayWealthTax(MinWealthTaxExempt, WealthTaxRate);
        Assert.IsTrue(idleTax >= 0);
        gov.Pay(-idleTax);
        taxed += idleTax;
        //Utilities.TransferQuantity(idleTax, agent, irs);
        Debug.Log(auctionStats.round + " " + agent.name + " has "
            + agent.cash.ToString("c2") + " produced " + numProduced
            + " goods and idle taxed " + idleTax.ToString("c2"));
    }
    public float AddSalesTax(float quant, float price)
    {
        if (!config.EnableSalesTax)
            return 0;
        var salesTax = config.SalesTaxRate * quant * price;
        Assert.IsTrue(salesTax > 0);
        gov.Pay(-salesTax);
        taxed += salesTax;
        return salesTax;
    }
    void applyIncomeTax(AuctionBook book, EconAgent agent)
    {
        float tax = 0;
        var income = agent.Income();
        if (income < 0)
            return;
        float prevTaxRate = 0;
        foreach (var (bracket, taxRate) in taxBracket)
        {
            if (income < bracket.min)
                break;
            if (income < bracket.max)
            {
                tax += (income - bracket.min) * (taxRate - prevTaxRate);
                break;
            } else {
                tax += (bracket.max - bracket.min) * (taxRate - prevTaxRate);
                prevTaxRate = taxRate;
            }
        }

        var finalTaxRate = tax / income;
        //what is this?? tax += agent.PayTax(finalTaxRate);
        Assert.IsTrue(tax >= 0);
        agent.Pay(tax);
        gov.Pay(-tax);
        taxed += tax;
        Debug.Log(auctionStats.round + " " + agent.name + " has "
            + agent.cash.ToString("c2") + " income " + income.ToString("c2")
            + " taxed " + tax.ToString("c2") + " at rate of " + finalTaxRate.ToString("P2"));
    }

}