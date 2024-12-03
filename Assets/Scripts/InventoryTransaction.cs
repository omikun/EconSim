using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Assertions;

public class InventoryTransaction {
    public InventoryTransaction(float p, float q)
    {
        Assert.IsTrue(p > 0);
        Price = p;
        Quantity = q;
    }

    public float Price;
    public float Quantity;
}