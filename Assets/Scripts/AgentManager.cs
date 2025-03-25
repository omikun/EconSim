using System.Collections.Generic;
using System.Linq;
using EconSim;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Assertions;

public class AgentManager
{
    private AuctionHouse auctionHouse;

    [PropertyOrder(5)] [HorizontalGroup("KillAgent")]
    public int killIndex = 2;

    [HideInInspector]
    public List<EconAgent> agents = new();

    public AgentManager(AuctionHouse auctionHouse)
    {
        this.auctionHouse = auctionHouse;
    }

    public int NumUnemployed
    {
        get { return agents.Sum(agent => (agent.Profession == "Unemployed") ? 1 : 0); }
    }

    public void TickAgent()
    {
        var book = auctionHouse.district.book;
        var approval = 0f;
		
        Debug.Log(auctionHouse.district.round + " gov outputs: " + auctionHouse.gov.outputName);
        var newAgents = new List<EconAgent>();
        var deadAgents = new List<EconAgent>();
		
        foreach (var agent in agents)
        {
            if (agent.Alive == false)
                continue;
//	        Debug.Log("TickAgent() " + agent.name);
            if (agent is Government || agent is Bank)
            {
                if (agent is Government)
                    ((Government)agent).Tick(agents.Count);
                else if (agent is Bank)
                    ((Bank)agent).Tick();
                continue;
            }
            bool changedProfession = false;
            bool bankrupted = false;
            bool starving = false;
            string profession = agent.Profession;


            if (profession != "Unemployed")
            {
                book[profession].numAgents++;
                book[profession].numAgents += agent.NumEmployees;

                approval += agent.EvaluateHappiness();

                if (agent.Cash < 0.0f)
                    book[profession].numBankrupted++;

                if (agent.CalcMinProduction() < 1)
                    book[profession].numNoInput++;
			
                if (agent.Income < 0)
                    book[profession].numNegProfit++;

                book[profession].incomes[^1] += agent.Income;
            }
			
            var cash = agent.Tick(auctionHouse.gov, ref changedProfession, ref bankrupted, ref starving);
            if (agent.Alive == false)
            {
                agent.gameObject.SetActive(false);
                deadAgents.Add(agent);
                if (agent.Employer != null)
                    agent.Employer.EmployeeQuit(agent);
                if (auctionHouse.district.bank.QueryLoans(agent) > 0)
                    auctionHouse.district.bank.LiquidateInventory(agent.inventory);
                else
                    auctionHouse.gov.LiquidateInventory(agent.inventory);
                continue;
            } else if (cash > 0)
            {
                SpawnNewAgent(cash, newAgents, agent);
                // Debug.Log(auctionStats.round + " New agent " + gameObject.name + " uid: " + uid + " cash: " + Cash.ToString("c2") + " has " + inventory[buildable].Quantity + " " + buildable);
            }
            // gov.Pay(amount); //welfare?

            if (profession != "Unemployed" && profession != "Labor")
            {
                if (starving)
                {
                    book[profession].starving[^1]++;
                    book[profession].numStarving++;
                }
                if (bankrupted)
                    book[profession].bankrupted[^1]++;
                if (changedProfession)
                    book[profession].changedProfession[^1]++;
            }
            // Debug.Log(agent.name + " total cash line: " + agents.Sum(x => x.cash).ToString("c2") + amount.ToString("c2"));

            agent.ClearRoundStats();
        }

        foreach (var agent in deadAgents)
        {
            agents.Remove(agent);
        }
        foreach (var agent in newAgents)
            agents.Add(agent);

        float inflation = 0;
        auctionHouse.district.numStarving = agents.Sum(agent => (agent.DaysStarving > 0) ? 1 : 0);
        foreach (var rsc in book.Values)
        {
            rsc.happiness /= rsc.numAgents;
            rsc.gdp = rsc.trades[^1] * rsc.marketPrice;
            auctionHouse.district.gdp += rsc.gdp;

            auctionHouse.district.numBankrupted += rsc.numBankrupted;
            //district.numStarving += rsc.numStarving;
            auctionHouse.district.numNoInput += rsc.numNoInput;
            auctionHouse.district.numNegProfit += rsc.numNegProfit;
            rsc.numChangedProfession = (int)rsc.changedProfession[^1];
            auctionHouse.district.numChangedProfession += rsc.numChangedProfession;

            var prevPrice = rsc.avgClearingPrice[^2];
            var currPrice = rsc.avgClearingPrice[^1];
            if (prevPrice != 0)
                inflation += (currPrice - prevPrice) / prevPrice;
            Debug.Log("inflation current for " + rsc.name + " is " + inflation.ToString("p2"));
        }

        inflation /= 3f;//(float)book.Count;
        auctionHouse.district.inflation = (!float.IsNaN(inflation) && !float.IsInfinity(inflation)) ? inflation : 0;
        auctionHouse.district.happiness = approval / agents.Count;
        auctionHouse.district.approval = approval / agents.Count;
        auctionHouse.district.gini = GetGini(GetWealthOfAgents());
    }

    private void SpawnNewAgent(float cash, List<EconAgent> newAgents, EconAgent agent)
    {
        if (cash == 1f)
            cash = 0;
        var prefab = GetAgentPrefab();
        var chance = UnityEngine.Random.Range(0f, 1f);
        // spawn new agent! if 1 spawn as wood worker or ore miner
        // else spawn as most profitable profession
        // var newAgent = NewAgent(prefab, agent.Profession, amount);
        GameObject go = Object.Instantiate(prefab) as GameObject;
        go.transform.parent = auctionHouse.transform;
			
        var newAgent = go.GetComponent<EconAgent>();
        // InitAgent(newAgent, profession, cash);
        newAgent.Init(auctionHouse.config, auctionHouse.district, "Unemployed", 0, 50, cash);  //available for hire
        go.name = "agent" + newAgent.uid.ToString(); //uid only initialized after agent.Init
        newAgents.Add(newAgent);
        Debug.Log(auctionHouse.district.round + " new agent: " + go.name + " uid: " + newAgent.uid.ToString());
        if (agent.Profession != "Unemployed") // && agent.Profession != "Labor")
        {
            // if (agent.Employer != null)
            // 	agent.Employer.Hire(newAgent, cash);
            // else 
            if (agent.Profession != "Labor")
                agent.Hire(newAgent, cash);
        }
    }

    [PropertyOrder(5)] [HorizontalGroup("KillAgent")]
    [Button(ButtonSizes.Large), GUIColor(1, 0.4f, 0.4f)]
    public void KillAgent()
    {
        var agent = agents[killIndex];
        if (auctionHouse.district.bank.QueryLoans(agent) > 0)
            auctionHouse.district.bank.LiquidateInventory(agent.inventory);
        else
            auctionHouse.gov.LiquidateInventory(agent.inventory);
        agents.Remove(agent);
    }

    private GameObject GetAgentPrefab()
    {
        GameObject prefab;
        if (auctionHouse.config.agentType == AgentType.Simple)
        {
            prefab = (GameObject)Resources.Load("SimpleAgent");
        } else if (auctionHouse.config.agentType == AgentType.Medium)
        {
            prefab = (GameObject)Resources.Load("MediumAgent");
        } else if (auctionHouse.config.agentType == AgentType.User)
        {
            prefab = (GameObject)Resources.Load("UserAgent");
        } else
        {
            prefab = (GameObject)Resources.Load("Agent");
        }

        return prefab;
    }

    public void InitAgents()
    {
        GameObject prefab = GetAgentPrefab();
        var professions = auctionHouse.config.numAgents.Keys;
        foreach (string profession in professions)
        {
            for (int i = 0; i < auctionHouse.config.numAgents[profession]; ++i)
            {
                var agent = NewAgent(prefab, profession);
                agents.Add(agent);
            }
        }
    }

    private EconAgent NewAgent(GameObject prefab, string profession, float cash=-1f)
    {
        GameObject go = Object.Instantiate(prefab) as GameObject;
        go.transform.parent = auctionHouse.transform;
			
        var agent = go.GetComponent<EconAgent>();
        InitAgent(agent, profession, cash);
        go.name = "agent" + agent.uid; //uid only initialized after agent.Init
        agent.name = go.name;
        return agent;

    }

    private void InitAgent(EconAgent agent, string type, float cash=-1f)
    {
        string buildable = type;
        float initStock = auctionHouse.config.initStock;
        if (auctionHouse.config.randomInitStock)
        {
            initStock = Random.Range((int)(auctionHouse.config.initStock/2), (int)(auctionHouse.config.initStock*2));
            initStock = Mathf.Floor(initStock);
        }

        // This may cause uneven maxStock between agents
        var maxStock = Mathf.Max(initStock, auctionHouse.config.maxStock);

        agent.Init(auctionHouse.config, auctionHouse.district, buildable, initStock, maxStock, cash);
    }
    public List<float> GetWealthOfAgents()
    {
        return agents.Where(x => x is not Government).Select(x => x.Cash).ToList();
    }
    public float GetGini(List<float> values)
    {
        values.Sort();
        // string msg = ListUtil.ListToString(cashList, "c2");
        // Debug.Log("cash: " + msg);
        int n = values.Count;
        if (n == 0) return auctionHouse.district.gini;

        float totalWealth = values.Sum();
        if (totalWealth == 0)
	        return 0;
        
        float cumulativeWealth = 0;
        float weightedSum = 0;
        for (int i = 0; i < n; i++)
        {
            cumulativeWealth += values[i];
            weightedSum += (i + 1) * values[i];
        }

        // Gini coefficient formula
        float gini = (2.0f * weightedSum) / (n * totalWealth) - (n + 1.0f) / n;
        Assert.IsFalse(float.IsNaN(gini));
        return gini;
    }
}