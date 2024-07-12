using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class Utilities : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public static void TransferQuantity(float quantity, EconAgent from, EconAgent to)
    {
        from.modify_cash(-quantity);
        to.modify_cash(quantity);
    }
}

