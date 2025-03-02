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
public class Bank : EconAgent
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
    public Loan Borrow(EconAgent agent, float amount, string curr)
    {
        if (!Enable) 
            return null;
        
        Assert.IsTrue(amount > 0);
        
        if (!CheckIfCanBorrow(agent, amount))
        {
            Debug.Log("can't borrow no more");
            return null;
        }
        
        var fraction = TotalDeposits / (amount + liability);
        var metric = (fractionalReserveRatio);
        Debug.Log(agent.name + " bank borrowed " + amount.ToString("c2") + " " + curr
        + " deposit/liability ratio: " + fraction + " reserve ratio: "+ metric.ToString("c2"));
        if (fraction < metric)
        {
            Debug.Log("unable to loan: " + fraction + " < " + metric);
            return null;
        }

        var account = loanBook[agent] = loanBook.GetValueOrDefault(agent, new Loans());
        var loan = new Loan(curr, amount, interestRate, termInRounds);
        account.Add(loan);
        
        agent.AddToCash(amount);
        liability += amount;
        return loan;
    }

    public bool CheckIfCanBorrow(EconAgent agent, float amount)
    {
        var account = loanBook[agent] = loanBook.GetValueOrDefault(agent, new Loans());
        var canBorrow = (account.Principle + amount) <= maxPrinciple && account.numDefaults < maxNumDefaults;
        Debug.Log(agent.name + " potential principle " + (account.Principle + amount).ToString("c2") + " / " + maxPrinciple.ToString("c2") + " defaults " + account.numDefaults + " / " + maxNumDefaults);
        return canBorrow;
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

    public float QueryLoans(EconAgent agent)
    {
        return loanBook.TryGetValue(agent, out var entry) ? entry.Principle : 0f;
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

    public void CollectPayments()
    {
        var prevDebt = liability;
        var prevWealth = Wealth;
        var tempLiability = liability;
        foreach (var (agent, loans) in new LoanBook(loanBook))
        {
            CollectPaymentsFromAgent(agent, ref tempLiability, loans);
        }
        DebugLiability(tempLiability);

        var collectedDebt = prevDebt - liability;
        var collectedInterest = Wealth - prevWealth;
        var totalCollected = collectedDebt + collectedInterest;
        Debug.Log("Collected total: " + totalCollected.ToString("c2") 
                  + " collected debt: " + collectedDebt.ToString("c2")
                  + " collected interest: " + collectedInterest.ToString("c2"));
    }

    private void CollectPaymentsFromAgent(EconAgent agent, ref float tempLiability, Loans loans)
    {
        var deposit = Deposits[agent] = Deposits.GetValueOrDefault(agent, 0);
        
        //pay off loans to the extent possible with additional predicted potential borrows
        var agentMonies = PayOffLoans(agent, ref tempLiability, loans, deposit, out var interestPaidByAgent, out var paymentByAgent);
        
        //mark any paid off loans, wipe off from the books
        MarkOffPaidLoans(agent, ref tempLiability, loans);
        DebugLiability(tempLiability);

        //if agent missing money, borrow more to cover shortfall
        if (agentMonies < paymentByAgent)
        {
            AgentBorrowsShortfall(agent, ref tempLiability, paymentByAgent, agentMonies);
        }
        DebugLiability(tempLiability);

        //agent now makes all outstanding payment
        AgentPays(agent, paymentByAgent, deposit, interestPaidByAgent);

        var principle = paymentByAgent - interestPaidByAgent;
        Wealth += interestPaidByAgent;
        liability -= principle;

        if (loans.numDefaults > maxNumDefaults)
        {
            //bank gets to own all assets, agent becomes unemployed
            agent.BecomesUnemployed();
            LiquidateInventory(agent.inventory);
        }
    }

    private void AgentBorrowsShortfall(EconAgent agent, ref float tempLiability, float paymentByAgent, float agentMonies)
    {
        var borrowAmount = BorrowAmount(paymentByAgent, agentMonies);
        tempLiability += borrowAmount;
        Borrow(agent, borrowAmount, "Cash");
        //what happens if agent defaults? can't borrow anymore
        //kill off agent? 
        Assert.IsTrue(agent.Cash >= paymentByAgent, " cash " + agent.Cash.ToString("c2") + " payment " + paymentByAgent.ToString("c2"));
    }

    private void AgentPays(EconAgent agent, float paymentByAgent, float deposit, float interestPaidByAgent)
    {
        if (Deposits[agent] < paymentByAgent)
        {
            Debug.Log(agent.name + " repaid " + paymentByAgent.ToString("c2") 
                      + " with deposits " + deposit.ToString("c2")
                      + " and cash " + agent.Cash.ToString("c2"));
            Deposits[agent] = 0;
            agent.Pay(paymentByAgent - deposit);
        }
        else
        {
            Deposits[agent] -= paymentByAgent;
        }

        Debug.Log("liability: " + liability + " " + agent.name + " repaid " + paymentByAgent.ToString("c2") + " interest " + interestPaidByAgent.ToString("c2"));
    }

    private void MarkOffPaidLoans(EconAgent agent, ref float tempLiability, Loans loans)
    {
        foreach (var loan in new Loans(loans))
            if (loan.paidOff)
            {
                tempLiability -= loan.principle;
                liability -= loan.principle;
                loanBook[agent].Remove(loan);
                Debug.Log("paid off a loan!");
            }
    }

    private float PayOffLoans(EconAgent agent, ref float tempLiability, Loans loans, float deposit,
        out float interestPaidByAgent, out float paymentByAgent)
    {
        float agentMonies = agent.Cash + deposit;

        bool canBorrowMore = true;
        interestPaidByAgent = 0f;
        paymentByAgent = 0f;
        DebugLiability(tempLiability);
        //accumulate total payment, borrow if short
        foreach (var loan in loans)
        {
            var interest = loan.Interest();
            var payment = loan.Payment();

            //for each loan, if enough money, mark as paid.
            //if not enough money, can't pay no more.
            var enoughToPay = agentMonies >= paymentByAgent + payment;
            if (!enoughToPay && canBorrowMore)
            {
                var borrowAmount = BorrowAmount(paymentByAgent + payment, agentMonies);
                canBorrowMore = CheckIfCanBorrow(agent, borrowAmount);
            }

            if (!enoughToPay && !canBorrowMore) //missed payment
            {
                Debug.Log(agent.name + " unable to repay " + payment.ToString("c2") + " interest " + interest.ToString("c2"));
                loan.Paid(0); //marks missed payment if 0

                if (loan.missedPayments >= maxMissedPayments - loans.numDefaults)
                {
                    loan.defaulted = true; //NOTE numDefaults could be larger than maxNumDefaults
                    if (loans.numDefaults > maxNumDefaults)
                    {
                        agent.BecomesUnemployed();
                        LiquidateInventory(agent.inventory);
                    }
                }
                continue;
            }
            
            loan.Paid(payment); //marks missed payment if 0

            Debug.Log(agent.name + " repaid " + payment.ToString("c2") + " interest " + interest.ToString("c2"));
            interestPaidByAgent += interest;
            paymentByAgent += payment;
                
            tempLiability -= payment - interest;
            DebugLiability(tempLiability);
        }

        return agentMonies;
    }

    public void LiquidateInventory(Inventory agentInventory)
    {
        foreach (var (good, item) in agentInventory)
        {
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

    private void DebugLiability(float tempLiability)
    {
        float collectiveLoan;
        collectiveLoan = loanBook.Values.Sum(loans => loans.Principle);
        Debug.Log("check liability2: " + tempLiability + " vs " + collectiveLoan);
        // Assert.AreEqual((int)collectiveLoan, (int)tempLiability);
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