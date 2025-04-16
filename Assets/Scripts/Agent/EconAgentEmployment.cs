using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;

public partial class EconAgent
{
    public Dictionary<EconAgent, float> Employees { get; protected set; }
    public EconAgent Employer { get; protected set; }

    public int NumEmployees
    {
        get { return (Employees != null) ? Employees.Count : 0; }
    }

    public void Hire(EconAgent agent, float wage)
    {
        if (Employees == null)
            Employees = new();
        Assert.IsFalse(Profession == "Labor" || Profession == "Unemployed");
        Assert.IsTrue(Employer == null, 
            name + Profession 
                 + " can't be hiring if already has an employer agent already has " 
                 + Employees.Count + " employees");
        Employees[agent] = wage;
        agent.SetEmployed();
        agent.inventory["Labor"].Decrease(1);
        Assert.IsTrue(agent.inventory["Labor"].Quantity == 0);
        agent.Employer = this;
        //pay them to keep them alive!
        var firstPaycheck = Mathf.Min(Cash, book["Food"].marketPrice * .8f);
        firstPaycheck = Mathf.Max(0, firstPaycheck);
        agent.Collect(firstPaycheck);
    }

    public void EmployeeQuit(EconAgent employee)
    {
        Employees.Remove(employee);
        employee.Quit();
    }

    public void Quit()
    {
        var exEmployer = Employer;
        Employer = null;
        exEmployer?.EmployeeQuit(this);
		outputName = "Unemployed";
		inventory["Labor"].Set(1);
		inventory["Labor"].priceBelief = Mathf.Min(book["Labor"].marketPrice, book["Food"].marketPrice / 2f);
		if (NumEmployees > 0)
		{
			Disband();
		}
    }

    public void BecomesUnemployed()
	{
	}

    public void Disband()
    {
        if (Employees == null)
            return;
        foreach (var employee in Employees.Keys.ToList())
            EmployeeQuit(employee);
    }
	public void SetEmployed()
	{
		outputName = "Labor";
	}
	
	public virtual void ChangeProfession(Government gov, bool bankrupted = true)
	{
		string bestGood = auctionStats.GetHottestGood();
		float profit = 0f;
		string mostDemand = auctionStats.GetMostProfitableProfession(ref profit, Profession);

		if (bestGood != "invalid")
		{
			mostDemand = bestGood;
		}

		Debug.Log(auctionStats.round + " " + name + " changing from " + Profession + " to " + mostDemand +
		          " --  bestGood: " + bestGood + " bestProfession: " + mostDemand);

		string b = "";
		var lastInProfession = Profession != "Unemployed" && book[Profession].numAgents == 1;
		if (mostDemand != "invalid" && !lastInProfession)
			b = mostDemand;
		else
			b = Profession;

		if (config.clearInventory)
		{
			inventory.Clear();
		}

		Respawn(bankrupted, b, gov);
	}

    public string Profession
    {
        get { return outputName; }
    }
    
    public virtual void HandleDeath()
    {
        var quants = inventory.Values.Select(item => item.Quantity);
        //var msg = string.Join(",", quants);
        var msg = $"{string.Join(",", inventory.Keys)}--{string.Join(",", inventory.Values.Select(item => item.Quantity))}";
        //var msg = string.Join(",", inventory.SelectMany(t => t.Key, (t, i) => t.Key + ", " + t.Value.Quantity ));

        Debug.Log(auctionStats.round + " " + name + " has died with " + msg);
        Alive = false;
        outputName = "Dead";
        Quit();
            
        if (auctionStats.bank.QueryLoans(this) > 0f)
            auctionStats.bank.LiquidateInventory(inventory);
        else
            auctionStats.gov.LiquidateInventory(inventory);
    }
}