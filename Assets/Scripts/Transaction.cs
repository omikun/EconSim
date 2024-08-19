using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Assertions;

public class Transaction {
    public Transaction(float p, float q)
    {
        price = p;
        quantity = q;
    }
    public float price;
    public float quantity;
}