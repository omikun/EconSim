using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Assertions;

[Serializable]
public class BankRegulations
{
    public float fractionalReserveRatio { get; }
    public int termInRounds { get; }
    [ShowInInspector]
    public float interestRate { get; set; }
    public float maxMissedPayments { get; }
    public float maxPrinciple { get; }
    public int maxNumDefaults { get; }

    public BankRegulations(float ratio, int termsInRounds, float interest, int _maxMissedPayments, float _maxPrinciple, int _maxNumDefaults)
    {
        fractionalReserveRatio = ratio;
        termInRounds = termsInRounds;
        interestRate = interest;
        maxPrinciple = _maxPrinciple;
        maxMissedPayments = _maxMissedPayments;
        maxNumDefaults = _maxNumDefaults;
        Assert.IsTrue(maxMissedPayments > maxNumDefaults);
    }
}

public class LoanBook : Dictionary<EconAgent, Loans>
{
    public LoanBook() { }
    public LoanBook(LoanBook book) : base(book) { }
    
}

[Serializable]
public class Bank
{
    [ShowInInspector] 
    public bool Enable = true;
    
    [ShowInInspector]
    public float TotalDeposits { get; private set; }
    private string currency;
    [ShowInInspector]
    BankRegulations regulations;

    [ShowInInspector]
    public float liability { get; private set; }
    [ShowInInspector]
    public float Wealth { get; private set; }
    [ShowInInspector]
    private LoanBook book = new();
    public Dictionary<EconAgent, float> Deposits { get; private set; }

    public float Monies()
    {
        return TotalDeposits + Wealth;
    }
    public Bank(float initDeposits, string curr, BankRegulations reg)
    {
        currency = curr;
        TotalDeposits = initDeposits;
        regulations = reg;
        liability = 0;
        Wealth = 0;
        Deposits = new();
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
        var metric = (regulations.fractionalReserveRatio);
        Debug.Log(agent.name + " bank borrowed " + amount.ToString("c2") + " " + curr
        + " deposit/liability ratio: " + fraction + " reserve ratio: "+ metric.ToString("c2"));
        if (fraction < metric)
        {
            Debug.Log("unable to loan: " + fraction + " < " + metric);
            return null;
        }

        var account = book[agent] = book.GetValueOrDefault(agent, new Loans());
        var loan = new Loan(curr, amount, regulations.interestRate, regulations.termInRounds);
        account.Add(loan);
        
        agent.AddToCash(amount);
        liability += amount;
        return loan;
    }

    public bool CheckIfCanBorrow(EconAgent agent, float amount)
    {
        var account = book[agent] = book.GetValueOrDefault(agent, new Loans());
        var canBorrow = account.Principle + amount <= regulations.maxPrinciple || account.numDefaults > regulations.maxNumDefaults;
        Debug.Log(agent.name + " potential principle " + (account.Principle + amount).ToString("c2") + " / " + regulations.maxPrinciple.ToString("c2") + " defaults " + account.numDefaults + " / " + regulations.maxNumDefaults);
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
        return book.TryGetValue(agent, out var entry) ? entry.Principle : 0f;
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
        foreach (var (agent, loans) in new LoanBook(book))
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
        float agentMonies = agent.Cash + deposit;

        bool canBorrowMore = true;
        float interestPaidByAgent = 0f;
        float paymentByAgent = 0f;
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

            if (!enoughToPay && !canBorrowMore)
            {
                payment = 0;
                interest = 0;
            }
            
            loan.Paid(payment); //marks missed payment if 0

            Debug.Log(agent.name + " repaid " + payment.ToString("c2") + " interest " + interest.ToString("c2"));
            interestPaidByAgent += interest;
            paymentByAgent += payment;
                
            tempLiability -= payment - interest;
            DebugLiability(tempLiability);
            
            if (loan.missedPayments >= regulations.maxMissedPayments - loans.numDefaults)
                loan.defaulted = true; //NOTE numDefaults could be larger than maxNumDefaults
        }
        //pay off any loans
        foreach (var loan in new Loans(loans))
            if (loan.paidOff)
            {
                tempLiability -= loan.principle;
                liability -= loan.principle;
                book[agent].Remove(loan);
                Debug.Log("paid off a loan!");
            }
        DebugLiability(tempLiability);

        //if agent missing money, borrow more to cover shortfall
        if (agentMonies < paymentByAgent)
        {
            var borrowAmount = BorrowAmount(paymentByAgent, agentMonies);
            tempLiability += borrowAmount;
            Borrow(agent, borrowAmount, "cash");
            //what happens if agent defaults? can't borrow anymore
            //kill off agent? 
            Assert.IsTrue(agent.Cash >= paymentByAgent, " cash " + agent.Cash.ToString("c2") + " payment " + paymentByAgent.ToString("c2"));
        }
        DebugLiability(tempLiability);

        //agent now makes all outstanding payment
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
            
        var principle = paymentByAgent - interestPaidByAgent;
        Wealth += interestPaidByAgent;
        liability -= principle;
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
        collectiveLoan = book.Values.Sum(loans => loans.Principle);
        Debug.Log("check liability2: " + tempLiability + " vs " + collectiveLoan);
        Assert.AreEqual((int)collectiveLoan, (int)tempLiability);
    }
}