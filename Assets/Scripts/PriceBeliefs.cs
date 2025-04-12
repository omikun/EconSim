public class PriceBeliefs
{
    public float priceBelief;
    public EconAgent agent;
    public string itemName;
    public void Update(string agentName, in Offer trade, in ResourceController rsc)
    {
        if (trade.offerQuantity == 0)
            return;

        // if (trade.sell)
        //     UpdateBuyer(agentName, trade, rsc);
        // else
        //     UpdateSeller(agentName, trade, rsc);
    }

    private void UpdateBuyer(string agentName, in Offer trade, in ResourceController rsc)
    {
        priceBelief = trade.clearingPrice;
    }

    private void UpdateSeller(string agentName, in Offer trade, in ResourceController rsc)
    {
        priceBelief = trade.clearingPrice;
    }
}
