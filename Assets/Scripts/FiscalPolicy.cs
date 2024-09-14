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

public class FiscalPolicy 
{
    protected AgentConfig config;
    protected float debt = 0;
    public FiscalPolicy(AgentConfig cfg)
    {
        config = cfg;
    }
    public virtual void Tax(AuctionBook book, List<EconAgent> agents)
    {
        //tax wealth per round?
        //tax income?
        //tax profit?
        //exception?
        foreach (var agent in agents)
        {
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
    public FlatTaxPolicy(AgentConfig cfg) : base(cfg) {}
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
    [InfoBox("Tax fraction of wealth per idle round", "@!EnableIdleTax")]
    public bool EnableIdleTax = true;

    [ShowIf("EnableIdleTax")]
    public float IdleTaxRate = .1f;

    [InfoBox("Marginal Income Tax", "@!EnableIncomeTax")]
    public bool EnableIncomeTax = true;

    [ShowIf("EnableIncomeTax")]
    [ShowInInspector, DictionaryDrawerSettings(DisplayMode = DictionaryDisplayOptions.OneLine, KeyLabel = "Income Bracket", ValueLabel = "Marginal Tax Rate")]
    [SerializedDictionary("Income Bracket", "Marginal Tax Rate")]
    public SerializedDictionary<Range, float> taxBracket = new()
    {
        {new Range(0, 1), 0.1f}
        //{1f, 0.1f}
    };
    public ProgressivePolicy(AgentConfig cfg) : base(cfg)
    {
    }
    public override void Tax(AuctionBook book, List<EconAgent> agents)
    {
        foreach (var agent in agents)
        {
            if (!EnableIdleTax) applyIdleTax(book, agent);
            if (EnableIncomeTax) applyIncomeTax(book, agent);
        }
    }
    public override void ApplyTax(AuctionBook book, EconAgent agent)
    {
        base.ApplyTax(book, agent);
        applyIdleTax(book, agent);
    }
    void applyIdleTax(AuctionBook book, EconAgent agent)
    {
        var numProduced = agent.numProducedThisRound;
        if (numProduced > 0) return;

        float idleTax = agent.PayTax(IdleTaxRate);
        debt -= idleTax;
        //Utilities.TransferQuantity(idleTax, agent, irs);
        Debug.Log(AuctionStats.Instance.round + " " + agent.name + " has "
            + agent.cash.ToString("c2") + " produced " + numProduced
            + " goods and idle taxed " + idleTax.ToString("c2"));

    }
    void applyIncomeTax(AuctionBook book, EconAgent agent)
    {
        float tax = 0;
        var income = agent.Income();
        float prevTaxRate = 0;
        foreach (var (bracket, taxRate) in taxBracket)
        {
            if (income < bracket.min)
                break;
            if (income <= bracket.max)
            {
                tax += (bracket.max - bracket.min) * (taxRate - prevTaxRate);
                break;
            } else {
                tax += (income - bracket.min) * (taxRate - prevTaxRate);
                prevTaxRate = taxRate;
            }
        }

        var finalTaxRate = tax / income;
        tax += agent.PayTax(finalTaxRate);
        debt -= tax;
        Debug.Log(AuctionStats.Instance.round + " " + agent.name + " has "
            + agent.cash.ToString("c2") + " income " + income.ToString("c2")
            + " taxed " + tax.ToString("c2") + " at rate of " + finalTaxRate.ToString("n2"));
    }

}