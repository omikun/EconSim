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
    public GameObject tax;
    public GameObject subsidy;
    [HideInInspector]
    public TextMeshProUGUI text;
    [HideInInspector]
    public TextMeshProUGUI taxText;
    [HideInInspector]
    public TextMeshProUGUI subsidyText;
}
public class Player : MonoBehaviour
{
    [Required]
    public AuctionHouse selectedDistrict;
    public Government selectedAgent;

    [ShowInInspector, DictionaryDrawerSettings(DisplayMode = DictionaryDisplayOptions.OneLine, KeyLabel = "Comm", ValueLabel = "Control")]
    [SerializedDictionary("Comm", "Control")]
    public SerializedDictionary<string, Control> comControls = new();
    [ShowInInspector, DictionaryDrawerSettings(DisplayMode = DictionaryDisplayOptions.OneLine, KeyLabel = "Comm", ValueLabel = "Tax")]
    [SerializedDictionary("Comm", "Tax")]
    public SerializedDictionary<string, Control> taxControls = new();
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
        onClick.AddListener(delegate{QueueOffer(com, -5f);});

        onClick = control.go.transform.Find("Next").GetComponent<ButtonManager>().onClick;
        onClick.RemoveAllListeners();
        onClick.AddListener(delegate{QueueOffer(com, 5f);});

        onClick = control.tax.transform.Find("Prev").GetComponent<ButtonManager>().onClick;
        onClick.RemoveAllListeners();
        onClick.AddListener(delegate{SetSalesTax(com, -0.01f);});

        onClick = control.tax.transform.Find("Next").GetComponent<ButtonManager>().onClick;
        onClick.RemoveAllListeners();
        onClick.AddListener(delegate{SetSalesTax(com, 0.01f);});

        onClick = control.subsidy.transform.Find("Prev").GetComponent<ButtonManager>().onClick;
        onClick.RemoveAllListeners();
        onClick.AddListener(delegate{SetSubsidy(com, -1f);});

        onClick = control.subsidy.transform.Find("Next").GetComponent<ButtonManager>().onClick;
        onClick.RemoveAllListeners();
        onClick.AddListener(delegate{SetSubsidy(com, 1f);});

        control.text = control.go.transform.Find("Main Content").transform.Find("Text").GetComponent<TextMeshProUGUI>();
        control.taxText = control.tax.transform.Find("Main Content").transform.Find("Text").GetComponent<TextMeshProUGUI>();
        control.subsidyText = control.subsidy.transform.Find("Main Content").transform.Find("Text").GetComponent<TextMeshProUGUI>();
    }
    public void QueueOffer(string com, float delta)
    {
        selectedAgent.UpdateTarget(com, delta);
        Debug.Log("QueueOffer! " + com + " " + delta);
        Tick(com);
    }
    public void SetSalesTax(string com, float delta)
    {
        selectedDistrict.config.SalesTaxRate[com] += delta;
    }
    public void SetSubsidy(string com, float delta)
    {
        selectedDistrict.config.Subsidy[com] += delta;
    }
    void Tick(string com)
    {
        var entry = selectedAgent.inventory[com];
        var queuedOffer = entry.TargetQuantity;
        comControls[com].text.text = entry.Quantity.ToString("n0") + " (" + queuedOffer.ToString("n0") + ")";
        comControls[com].taxText.text = ((selectedDistrict.config.SalesTaxRate[com])).ToString("P0");
        comControls[com].subsidyText.text = selectedDistrict.config.Subsidy[com].ToString("n0");
    }
    // Update is called once per frame
    void Update()
    {
        foreach (var key in comControls.Keys)
        {
            //Tick(key);
        }
    }
}
