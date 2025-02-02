using System;
using Sirenix.OdinInspector;
using UnityEngine;

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
    public bool defaulted { get; private set; }
    public int termInRounds{ get; }

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
        return Interest() + initialLoanAmount / termInRounds;
    }
    public float Interest()
    {
        return principle * interestRate;
    }

    public void Paid(float amount)
    {
        var interest = Interest();
        if (amount > interest)
        {
            principle -= amount - interest;
        }

        interestPaid += Mathf.Min(amount, interest);
        //TODO what happens if amount is less than interest? penalty??
    }
}