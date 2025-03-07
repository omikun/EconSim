using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;

public partial class Bank
{
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

        if (paymentByAgent <= 0)
            return;
        
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
    public float QueryLoans(EconAgent agent)
    {
        return loanBook.TryGetValue(agent, out var entry) ? entry.Principle : 0f;
    }
    private void DebugLiability(float tempLiability)
    {
        float collectiveLoan;
        collectiveLoan = loanBook.Values.Sum(loans => loans.Principle);
        Debug.Log("check liability2: " + tempLiability + " vs " + collectiveLoan);
        // Assert.AreEqual((int)collectiveLoan, (int)tempLiability);
    }
}
