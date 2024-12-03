using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;

public class GenericTransaction {
    public GenericTransaction(EconAgent d, EconAgent c, string com, float q)
    {
        Assert.IsTrue(q > 0);
        Quantity = q;
        DebitAccount = d;
        CreditAccount = c;
        Commodity = com;
    }

    public string ToString(string header)
    {
        var debit = new string(DebitAccount.name.Where(char.IsDigit).ToArray());
        var credit = new string(CreditAccount.name.Where(char.IsDigit).ToArray());
        return header + debit
                      + ", " + DebitAccount.outputName
               + ", " + Commodity +
               ", transaction"
               + ", " + Quantity.ToString("n2")
               + ", " + credit
               + "\n";
    }

    public EconAgent DebitAccount { get; }
    public EconAgent CreditAccount { get; }
    public string Commodity { get; }
    public float Quantity { get; set; }
}
