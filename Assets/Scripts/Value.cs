using System;
using UnityEngine;
using UnityEngine.Assertions;

public class Value
{
    protected float _value;
    protected EconAgent agent;
    protected InventoryItem item;


    public virtual float ComputeValue(float in_value)
    {
        return _value;
    }
    public virtual float Get
    {
        get { return ComputeValue(item.Quantity); }
        private set 
        { 
            _value = value;
        }
    }

    public Value(float initialValue, EconAgent agent, InventoryItem item) 
    {
        _value = initialValue;
        this.agent = agent;
        this.item = item;
    }
}

public class ValueNecessary : Value
{
    public ValueNecessary(float initialValue, EconAgent agent, InventoryItem item) : base(initialValue, agent, item)
    {
    }

    public override float ComputeValue(float in_value)
    {
        _value = 2f * Mathf.Pow(MathF.E, 10 / (in_value + 7f)) - 3f;
        return _value;
    }
}
public class ValueEveryday : Value
{
    public ValueEveryday(float initialValue, EconAgent agent, InventoryItem item) : base(initialValue, agent, item)
    {
    }

    public override float ComputeValue(float in_value)
    {
        _value = 2 - in_value * .4f;
        return _value;
    }
}
public class ValueLuxury : Value
{
    public ValueLuxury(float initialValue, EconAgent agent, InventoryItem item) : base(initialValue, agent, item)
    {
    }

    public override float ComputeValue(float in_value)
    {
        _value = Mathf.Pow(MathF.E, 30 / (in_value + 15f)); 
        return _value;
    }
}

public class ValueCash : Value
{
    public ValueCash(float initialValue, EconAgent agent, InventoryItem item) : base(initialValue, agent, item)
    {
    }
    
    public override float ComputeValue(float in_value)
    {
        return 1f;
        var interestRate = agent.auctionStats.bank.interestRate;
        var bankDeposit = agent.auctionStats.bank.CheckAccountBalance(agent);
        var compoundedInterest = Mathf.Pow(1 + interestRate, 30);
        var futureDeposit = bankDeposit * compoundedInterest;
        return Mathf.Pow(MathF.E, -0.01f * in_value) * (futureDeposit / bankDeposit);
    }
    public virtual float Get
    {
        get { return ComputeValue(agent.Wealth); }
        private set 
        { 
            _value = value;
        }
    }
}

public class ValueInput : Value
{
    public ValueInput(float initialValue, EconAgent agent, InventoryItem item) : base(initialValue, agent, item)
    {
    }

    public override float ComputeValue(float in_value)
    {
        var rsc = agent.book[agent.outputName];
        var outputItem = agent.inventory[agent.outputName];
        var recipe = rsc.recipe;
        var batchSize = rsc.productionPerBatch;

        Assert.IsTrue(recipe != null && recipe.ContainsKey(item.name));
        // Calculate minimum number of batches possible from all inputs
        float minBatches = float.MaxValue;
        foreach (var ingredient in recipe)
        {
            var inputItem = agent.inventory[ingredient.Key];
            var chance = agent.book[ingredient.Key].breakdown_chance;
            float batchesFromThisInput = inputItem.Quantity / ingredient.Value / chance;
            minBatches = Mathf.Min(minBatches, batchesFromThisInput);
        }

        // Add current output inventory to get total batches
        // Account for batch size (multiple output items per batch)
        float totalBatches = Mathf.Floor(outputItem.Quantity / batchSize) + minBatches;

        return Mathf.Max(0.1f, 5f - Mathf.Floor(totalBatches));
    }
}
