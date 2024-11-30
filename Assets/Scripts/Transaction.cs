using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Assertions;

public class Transaction {
    //public Transaction(EconAgent d, EconAgent c, float p, float q)
    public Transaction(float p, float q)
    {
        Assert.IsTrue(p > 0);
        Price = p;
        Quantity = q;
        // DebitAccount = d;
        // CreditAccount = c;
    }

    public EconAgent DebitAccount { get; }
    public EconAgent CreditAccount { get; }
    public float Price { get; set; }
    public float Quantity { get; set; }
}