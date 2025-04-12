using UnityEngine;

public class Value
{
    protected float _value;
    protected EconAgent agent;
    protected InventoryItem item;
    

    public virtual float ComputeValue
    {
        get { return _value; }
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

    public override float ComputeValue
    {
        get
        {
            _value = 2f * Mathf.Pow(float.Epsilon, 10 / (item.Quantity + 7f)) - 3f;
            return _value;
        }
    }
}
public class ValueEveryday : Value
{
    public ValueEveryday(float initialValue, EconAgent agent, InventoryItem item) : base(initialValue, agent, item)
    {
    }

    public override float ComputeValue
    {
        get
        {
            _value = 2 - item.Quantity * .4f;
            return _value;
        }
    }
}
public class ValueLuxury : Value
{
    public ValueLuxury(float initialValue, EconAgent agent, InventoryItem item) : base(initialValue, agent, item)
    {
    }

    public override float ComputeValue
    {
        get
        {
            _value = Mathf.Pow(float.Epsilon, 30 / (item.Quantity + 15f)); 
            return _value;
        }
    }
}

