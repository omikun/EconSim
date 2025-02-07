using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Serialization;

[Serializable]
public class Loans : List<Loan>
{
    public int numDefaults => this.Sum(loan => loan.defaulted ? 1 : 0);
    public float Principle => this.Sum(loan => loan.principle);

    public Loans(Loans loans) : base(loans) { }

    public Loans() { }
}
[Serializable]
public class Loan
{
    public float initialLoanAmount { get; }
    public string currency { get; } //dollars, yen, euro, etc
    public float interestRate { get; } //per round, 1%?
    [ShowInInspector]
    public float principle { get; private set;  }
    [ShowInInspector]
    public float interestPaid { get; private set; }
    public bool defaulted { get; set; }
    public int termInRounds{ get; }
    public int missedPayments { get; private set; }
    public bool paidOff { get; private set; }

    public Loan(string curr, float loanAmount, float interest, int term)
    {
        currency = curr;
        initialLoanAmount = loanAmount;
        principle = loanAmount;
        interestRate = interest;
        termInRounds = term;
    }

    public float Payment()
    {
        var amount = Interest() + initialLoanAmount / termInRounds;
        Assert.IsTrue(amount > 0);
        return amount;
    }
    public float Interest()
    {
        return principle * interestRate;
    }

    //returns if loan is paid off
    public bool Paid(float amount)
    {
        var interest = Interest();
        if (amount < Payment())
        {
            missedPayments++;
        }
        interestPaid += Mathf.Min(amount, interest);
        
        principle -= Mathf.Max(0, amount - interest);
        Debug.Log("Paid() " + principle + " -= " + amount + " - " + interest);
        paidOff = (principle < 0.01f);
        return paidOff;
    }
}