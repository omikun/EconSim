using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Assertions;


public class LoanBook : Dictionary<EconAgent, Loans>
{
    public LoanBook() { }
    public LoanBook(LoanBook book) : base(book) { }
    
}

[Serializable]
public partial class Bank : EconAgent
{
    [ShowInInspector]
    public float interestRate { get; set; }
    [ShowInInspector] [FoldoutGroup("Regulations")] public float fractionalReserveRatio { get; protected set; }
    [ShowInInspector] [FoldoutGroup("Regulations")] public int termInRounds { get; private set; } 
    [ShowInInspector] [FoldoutGroup("Regulations")] public float maxMissedPayments { get; private set; }
    [ShowInInspector] [FoldoutGroup("Regulations")] public float maxPrinciple { get; private set; }
    [ShowInInspector] [FoldoutGroup("Regulations")] public int maxNumDefaults { get; private set; }
 
    [ShowInInspector] 
    public bool Enable = true;
    
    [ShowInInspector]
    public float TotalDeposits { get; private set; }
    private string currency;

    [ShowInInspector]
    public float liability { get; private set; }
    [ShowInInspector]
    public float Wealth { get; private set; }
    [ShowInInspector]
    private LoanBook loanBook = new();
    public Dictionary<EconAgent, float> Deposits { get; private set; }

    public float Monies()
    {
        return TotalDeposits + Wealth;
    }
    public void BankInit(float initDeposits, string curr)
    {
        currency = curr;
        TotalDeposits = initDeposits;
        liability = 0;
        Wealth = 0;
        Deposits = new();
    }
    
    public void BankRegulations(float ratio, int termsInRounds, float interest, int _maxMissedPayments, float _maxPrinciple, int _maxNumDefaults) {
        fractionalReserveRatio = ratio;
        termInRounds = termsInRounds;
        interestRate = interest;
        maxPrinciple = _maxPrinciple;
        maxMissedPayments = _maxMissedPayments;
        maxNumDefaults = _maxNumDefaults;
        Assert.IsTrue(maxMissedPayments > maxNumDefaults);
    }

	public override void Init(SimulationConfig cfg, AuctionStats at, string b, float _initStock, float maxstock, float cash=-1f) {
		config = cfg;
		uid = uid_idx++;
		initStock = _initStock;
		maxStock = maxstock;
        Alive = true;

		book = at.book;
		auctionStats = at;
		//list of commodities self can produce
		//get initial stockpiles
		outputName = b;
        Cash = 0;
		prevCash = Cash;
		inputs.Clear();
        foreach(var good in book)
        {
            var name = good.Key;
            inputs.Add(name);
            AddToInventory(name, 0, maxstock, good.Value);
        }
        
        var com = "Labor";
        AddToInventory(com, 0, 1, book[com]);
    }
    

    public float Deposit(EconAgent agent, float amount, string curr)
    {
        if (!Enable)
            return 0;
        
        Assert.IsTrue(amount > 0);
        TotalDeposits += amount;
        Deposits[agent] = Deposits.GetValueOrDefault(agent) + amount;
        Debug.Log(agent.name + " deposited " + amount.ToString("c2") + " " + curr);
        return amount;
    }

    public float CheckAccountBalance(EconAgent agent)
    {
        Deposits[agent] = Deposits.GetValueOrDefault(agent, 0);
        return Deposits[agent];
    }


    public float Withdraw(EconAgent agent, float amount, string curr)
    {
        Assert.IsTrue(amount > 0);
        Deposits[agent] = Deposits.GetValueOrDefault(agent, 0);
        amount = Mathf.Min(Deposits[agent], amount);
        
        Debug.Log(agent.name + " withdrawing " + amount.ToString("c2") + " " + curr);
        Assert.IsTrue(TotalDeposits >= amount);
        TotalDeposits -= amount;
        Deposits[agent] -= amount;
        return amount;
    }


    public void LiquidateInventory(Inventory agentInventory)
    {
        foreach (var (good, item) in agentInventory)
        {
	        if (good == "Labor")
		        continue;
	        
            if (inventory.ContainsKey(good) == false)
                AddToInventory(good, item.Quantity, maxStock, item.rsc);
            else
                inventory[good].Increase(item.Quantity);
            item.Decrease(item.Quantity);
        }
    }

    private static float BorrowAmount(float paymentByAgent, float agentMonies)
    {
        var shortFall = paymentByAgent - agentMonies;
        var borrowAmount = Mathf.Max(30, shortFall * 1.2f);
        return borrowAmount;
    }


    public override Offers CreateBids(AuctionBook book)
    {
        return new Offers();
    }
    public override Offers CreateAsks()
    {
        var asks = new Offers();
        foreach (var (com, item) in inventory)
        {
            if (item.Quantity == 0)
                continue;
            item.offersThisRound = item.Quantity;
            var sellPrice = item.rsc.marketPrice * .9f; //based on supply and demand too?
            asks.Add(com, new Offer(com, sellPrice, item.Quantity, this));
        }

        return asks;
    }

    public override void Decide()
    {
    }
}