using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using AYellowpaper.SerializedCollections;
using Sirenix.OdinInspector;
using Michsky.MUIP;
using UnityEngine.UI;
using TMPro;
using System;
using Sirenix.Serialization;
using Sirenix.OdinInspector.Editor.ValueResolvers;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;

[System.Serializable]
public class Player : MonoBehaviour
{
    [Required]
    public AuctionHouse selectedDistrict;
    public Government selectedAgent;

    // Start is called before the first frame update
    void Start()
    {
        selectedAgent = (Government)selectedDistrict.gov;
        // InitTradeUI();
        // Tick("Food");
    }

    public void QueueOffer(string com, float delta)
    {
        selectedAgent.UpdateTarget(com, delta);
        Debug.Log("QueueOffer! " + com + " " + delta);
        Tick(com);
    }
    void Tick(string com)
    {
        var entry = selectedAgent.inventory[com];
        var queuedOffer = entry.TargetQuantity;
    }
    // Update is called once per frame
    void Update()
    {
    }
}
