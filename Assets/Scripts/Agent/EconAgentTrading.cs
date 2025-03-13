using UnityEngine;

public partial class EconAgent
{
    protected internal AskPriceStrategy askPriceStrategy;

    public virtual Offers CreateBids(AuctionBook book)
    {
        Debug.Log(name + " consuming");
        return consumer.CreateBids(book);
    }

    public virtual Offers CreateAsks()
    {
        return askPriceStrategy.CreateAsks();
    }
}