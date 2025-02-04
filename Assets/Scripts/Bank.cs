using System;
using System.Collections.Generic;
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
[Serializable]
public class Bank
{
    [ShowInInspector]
    public float TotalDeposits { get; private set; }
    private string currency;
    BankRegulations regulations;

    [ShowInInspector]
    public float outstandingDebt { get; private set; }
    [ShowInInspector]
    public float Wealth { get; private set; }
    [ShowInInspector]
    private Dictionary<EconAgent, Loans> book = new();
    private Dictionary<EconAgent, float> deposits;

    public float Monies()
    {
        return TotalDeposits + Wealth;
    }
    public Bank(float initDeposits, string curr, BankRegulations reg)
    {
        currency = curr;
        TotalDeposits = initDeposits;
        regulations = reg;
        outstandingDebt = 0;
        Wealth = 0;
    }

    public Loan Borrow(EconAgent agent, float amount, string curr)
    {
        Debug.Log(agent.name + " bank borrowed " + amount.ToString("c2") + " " + curr);
        if ((amount + outstandingDebt) / TotalDeposits > (1f - regulations.fractionalReserveRatio))
        {
            Assert.IsTrue(false);
            return null;
        }

        var loans = book.GetValueOrDefault(agent, new Loans());
        var loan = new Loan(curr, amount, regulations.interestRate, regulations.termInRounds);
        loans.Add(loan);
        agent.AddToCash(amount);
        outstandingDebt += amount;
        return loan;
    }

    public void Deposit(EconAgent agent, float amount, string curr)
    {
        TotalDeposits += amount;
        deposits[agent] = deposits.GetValueOrDefault(agent) + amount;
        Debug.Log(agent.name + " deposited " + amount.ToString("c2") + " " + curr);
    }

    public float Withdraw(EconAgent agent, float amount, string curr)
    {
        amount = Mathf.Min(deposits[agent]);
        //TODO automatic borrow if goes over?
        Debug.Log(agent.name + " withdrawing " + amount.ToString("c2") + " " + curr);
        Assert.IsTrue(TotalDeposits >= amount);
        TotalDeposits -= amount;
        deposits[agent] -= amount;
        return amount;
    }

    public void CollectPayments()
    {
        foreach (var (agent, loans) in book)
        {
            float interest = 0f;
            float amount = 0f;

            foreach (var loan in loans)
            {
                interest += loan.Interest();
                amount += loan.Payment();
                loan.Paid(amount);
            }

            if (agent.Cash < amount)
            {
                Borrow(agent, amount * 1.2f, "cash");
                Assert.IsTrue(agent.Cash >= amount);
            }

            Debug.Log(agent.name + " repaid " + amount.ToString("c2"));
            agent.Pay(amount);
            
            amount -= interest;
            Wealth += interest;
            outstandingDebt -= amount;
        }
    }
}