using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Assertions;

public class BankRegulations
{
    public float fractionalReserveRatio { get; }
    public int termInRounds { get; }
    public float interestRate { get; }

    public BankRegulations(float ratio, int termsInRounds, float interest)
    {
        fractionalReserveRatio = ratio;
        termInRounds = termsInRounds;
        interestRate = interest;
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
    public float TotalDeposits { get; private set; }
    private string currency;
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
        Assert.IsTrue(amount > 0);
        var fraction = TotalDeposits / (amount + liability);
        var metric = (regulations.fractionalReserveRatio);
        Debug.Log(agent.name + " bank borrowed " + amount.ToString("c2") + " " + curr
        + " deposit/liability ratio: " + fraction + " reserve ratio: "+ metric.ToString("c2"));
        if (fraction < metric)
        {
            Assert.IsTrue(false, "unable to loan: " + fraction + " < " + metric);
            return null;
        }

        var loan = new Loan(curr, amount, regulations.interestRate, regulations.termInRounds);
        
        book[agent] = book.GetValueOrDefault(agent, new Loans());
        book[agent].Add(loan);
        
        agent.AddToCash(amount);
        liability += amount;
        return loan;
    }

    public void Deposit(EconAgent agent, float amount, string curr)
    {
        Assert.IsTrue(amount > 0);
        TotalDeposits += amount;
        Deposits[agent] = Deposits.GetValueOrDefault(agent) + amount;
        Debug.Log(agent.name + " deposited " + amount.ToString("c2") + " " + curr);
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
        //TODO automatic borrow if goes over?
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
            float interestPaidByAgent = 0f;
            float paymentByAgent = 0f;

            var collectiveLoan = book.Values.Sum(loans => loans.Principle);
            Debug.Log("check liability1: " + tempLiability.ToString("c2") + " vs " + collectiveLoan.ToString("c2"));
            List<Loan> loansToRemove = new();
            foreach (var loan in loans)
            {
                var interest = loan.Interest();
                var payment = loan.Payment();
                tempLiability -= payment - interest;
                
                Debug.Log(agent.name + " repaid " + payment.ToString("c2") + " interest " + interest.ToString("c2"));
                interestPaidByAgent += interest;
                paymentByAgent += payment;
                bool paidOff = loan.Paid(payment);
                
                collectiveLoan = book.Values.Sum(loans => loans.Principle);
                Debug.Log("check liability2: " + tempLiability.ToString("c2") + " vs " + collectiveLoan.ToString("c2"));
                if (paidOff)
                    loansToRemove.Add(loan);
            }
            
            foreach (var loan in loansToRemove)
                    book[agent].Remove(loan);

            var deposit = Deposits[agent] = Deposits.GetValueOrDefault(agent, 0);
            if (agent.Cash + deposit < paymentByAgent)
            {
                Borrow(agent, paymentByAgent * 1.2f, "cash");
                Assert.IsTrue(agent.Cash >= paymentByAgent);
            }

            // Debug.Log(agent.name + " repaid " + amount.ToString("c2"));
            if (deposit < paymentByAgent)
            {
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
        var collectiveLoans = book.Values.Sum(loans => loans.Principle);
        Debug.Log("check liability3: " + liability.ToString("c2") + " vs " + collectiveLoans.ToString("c2"));
        //Assert.AreEqual(collectiveLoans, liability);

        var collectedDebt = prevDebt - liability;
        var collectedInterest = Wealth - prevWealth;
        var totalCollected = collectedDebt + collectedInterest;
        Debug.Log("Collected total: " + totalCollected.ToString("c2") 
                  + " collected debt: " + collectedDebt.ToString("c2")
                  + " collected interest: " + collectedInterest.ToString("c2"));
    }
}