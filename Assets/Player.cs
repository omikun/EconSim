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
public class Control
{
    [Required]
    public GameObject go;
    public TextMeshProUGUI text;
}
public class Player : MonoBehaviour
{
    [Required]
    public AuctionHouse selectedDistrict;
    public Government selectedAgent;

    [ShowInInspector, DictionaryDrawerSettings(DisplayMode = DictionaryDisplayOptions.OneLine, KeyLabel = "Comm", ValueLabel = "Control")]
    [SerializedDictionary("Comm", "Control")]
    public SerializedDictionary<string, Control> comControls = new();
    // Start is called before the first frame update
    void Start()
    {
        selectedAgent = (Government)selectedDistrict.gov;
        InitTradeUI();
        Tick("Food");
    }

    void InitTradeUI()
    {
        foreach (var (com, control) in comControls)
        {
            InitCommodityControl(com, control);
        }
    }

    void InitCommodityControl(string com, Control control)
    {
        //get prev, inject QueueOfferMinus into it
        var onClick = control.go.transform.Find("Prev").GetComponent<ButtonManager>().onClick;
        onClick.RemoveAllListeners();
        onClick.AddListener(delegate{QueueOfferMinus(com);});

        onClick = control.go.transform.Find("Next").GetComponent<ButtonManager>().onClick;
        onClick.RemoveAllListeners();
        onClick.AddListener(delegate{QueueOfferPlus(com);});


        control.text = control.go.transform.Find("Main Content").transform.Find("Text").GetComponent<TextMeshProUGUI>();
    }
    public void QueueOfferMinus(string com)
    {
        selectedAgent.QueueOffer(com, -5f);
        Debug.Log("QueueOfferMinus! " + com);
        Tick(com);
    }
    public void QueueOfferPlus(string com)
    {
        selectedAgent.QueueOffer(com, 5f);
        Debug.Log("QueueOfferPlus! " + com);
        Tick(com);
    }
    void Tick(string com)
    {
        var entry = selectedAgent.inventory[com];
        var queuedOffer = entry.OfferQuantity;
        comControls[com].text.text = entry.Quantity.ToString("n0") + " (" + queuedOffer.ToString("n0") + ")";
    }
    // Update is called once per frame
    void Update()
    {
        foreach (var key in comControls.Keys)
        {
            Tick(key);
        }
    }
}
