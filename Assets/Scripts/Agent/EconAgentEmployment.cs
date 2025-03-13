using System.Collections.Generic;
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
        Employees[agent] = wage;
        agent.SetEmployed();
        agent.inventory["Labor"].Decrease(1);
        Assert.IsTrue(agent.inventory["Labor"].Quantity == 0);
        agent.Employer = this;
        //pay them to keep them alive!
        var firstPaycheck = Mathf.Min(Cash, book["Food"].marketPrice * .8f);
        firstPaycheck = Mathf.Max(0, firstPaycheck);
        agent.Earn(firstPaycheck);
    }

    public void EmployeeQuit(EconAgent employee)
    {
        Employees.Remove(employee);
    }

    public string Profession
    {
        get { return outputName; }
    }
}