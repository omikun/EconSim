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
    private float totalDeposits;
    private string currency;
    BankRegulations regulations;

    [ShowInInspector]
    public float outstandingDebt { get; private set; }
    [ShowInInspector]
    public float income { get; private set; }
    [ShowInInspector]
    private Dictionary<EconAgent, Loan> loans = new();
    private Dictionary<EconAgent, float> deposits;

    public Bank(float initDeposits, string curr, BankRegulations reg)
    {
        currency = curr;
        totalDeposits = initDeposits;
        regulations = reg;
        outstandingDebt = 0;
        income = 0;
    }

    public Loan Borrow(EconAgent agent, float amount, string curr)
    {
        Debug.Log(agent.name + " borrowed " + amount.ToString("c2") + " " + curr);
        if ((amount + outstandingDebt) / totalDeposits > (1f - regulations.fractionalReserveRatio))
        {
            Assert.IsTrue(false);
            return null;
        }
        var loan = new Loan(curr, amount, regulations.interestRate, regulations.termInRounds);
        loans.Add(agent, loan);
        agent.AddToCash(amount);
        outstandingDebt += amount;
        return loan;
    }

    public void Deposit(EconAgent agent, float amount, string curr)
    {
        totalDeposits += amount;
        deposits[agent] = deposits.GetValueOrDefault(agent) + amount;
        Debug.Log(agent.name + " deposited " + amount.ToString("c2") + " " + curr);
    }

    public float Withdraw(EconAgent agent, float amount, string curr)
    {
        amount = Mathf.Min(deposits[agent]);
        //TODO automatic borrow if goes over?
        Debug.Log(agent.name + " withdrawing " + amount.ToString("c2") + " " + curr);
        Assert.IsTrue(totalDeposits >= amount);
        totalDeposits -= amount;
        deposits[agent] -= amount;
        return amount;
    }

    public void CollectPayments()
    {
        foreach (var (agent, loan) in loans)
        {
            var interest = loan.Interest();
            var amount = loan.Payment();
            if (agent.Cash < amount)
            {
                Borrow(agent, amount, "cash");
                Assert.IsTrue(agent.Cash > amount);
            }

            Debug.Log(agent.name + " repaid " + amount.ToString("c2"));
            amount -= interest;
            income += interest;
            agent.Pay(amount);
            loan.Paid(amount);
            outstandingDebt -= amount;
        }
    }
}