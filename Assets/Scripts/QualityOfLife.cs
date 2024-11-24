using UnityEngine;

public class QualityOfLife
{
    //compute quality of life based on the next incremental unit of x
    //quality of life = log10(food)
    //impacts how much to pay for food (or sell food if farmer)
    public static float GetQualityOfLife(float quant)
    {
        //want the delta in quality of life for 1 additional unit
        return Mathf.Log10((quant + 1) / quant);
    }
    //want number of units to get to certain quality of life
    public static float AdditionalQualityOfLife(float qol, float baseQuant)
    {
        //find x where target_qol = Log10((baseQuant + x)/baseQuant)
        //x = b(e^q-1)
        return baseQuant * (Mathf.Exp(qol)-1);
    }
    //how to check if the price of x is better than price of y?
    //get qol per unit / price of each
    //how to compare qol of different commodities? Convert them all to food equivalent?
    //than is qol the sum of all food equivalent?
        //in that case we always buy the cheapest food equivalent
    //if we sum qol of each food equivalent
        //we want to know the cost of all additional inputs to produce one additional output
        //but how to price those additional inputs?
        //if smith needs 1 food and 2 metal to make 1 tool, 
            //using food decreases qol, if all the smith needs is 1 additional metal,
            //tool = 1 food + 2 metal
            //tool = 1 food + 1 metal + buying additional metal
            //-> tool * mktprice = 1 food * mktprice + metal*cost/foodPrice + metal*mktprice/foodprice
            //-> 10  = 1 + 3
            //so upper limit of metal marketprice is < 10-1-3 = 6
            //realistically no more than 5 (want integral profit?)
            
    //depends on how trading works
    //if asker sets the price, then the bidder needs to know about the price, decides how much if any to buy
    //how to pick which bidder out of many? Select the bidder with the highest bid price
    //but what price do they pick from then? 
        //average of bid and ask: bidder then chooses quantity after getting the final clearing price?
        //or maybe bidder lists bid price and quantity, thus total cash bidder is willing to spend
        //when final price is set (could be higher than bid price) then auction adjusts quantity so total falls below 
        //initial bid price volume
        //so bid.price should be as high as it could go if it isn't buying as much as it wants to mn mņ.
        
    //for farmer who grows food, it won't sell food for less than market price unless it has pleanty
    //integrate foodToPrice(quant) from quant to threshold(10)
    //so if quant is 14, sellprice = Max(sum(foodToPrice(i) for range(5,14))/ (14-5), cost+profit)
    
    //what if sells more than 1 thing? how to price non outputs? remember their cost?
    
    //foodSellPrice = foodToPrice(foodQuant)
    //decide to produce, ex. tool, means only produce tool if
    //  mktprice of inputs is > mktprice of tool
    // but if sell price is cost + profit, doesn't matter so much
            
    //sell price is cost + profit
        //incr profit if sold all resources
        //decr profit if not all sold (decrease proportional to what isn't sold?
        //  or decrease relative to marketprice?
        //  or decrease relative to supply and demand? 
    //sell as much as you can when demand is high and price is high
    //need to know how much was sold for last price to estimate price for max profit
    //
    //for an additional output,
    public static float GetMarginalOutput(EconAgent agent)
    {
        return 0f;
    }
    //how to price how much to buy an input commodity?
}