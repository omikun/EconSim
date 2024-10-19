using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using AYellowpaper.SerializedCollections;
using Sirenix.OdinInspector;
using Michsky.MUIP;
using UnityEngine.UI;
using TMPro;
using Sirenix.OdinInspector.Editor.ValueResolvers;

public class Player : MonoBehaviour
{
    [Required]
    public AuctionHouse selectedDistrict;
    public Government selectedAgent;
    [Required]
    public GameObject hSelector;
    TextMeshProUGUI text;
    // Start is called before the first frame update
    void Start()
    {
        selectedAgent = (Government)selectedDistrict.gov;
        InitTradeUI();
        Tick("Food");
    }

    void InitTradeUI()
    {
        //get prev, inject QueueOfferMinus into it
        var onClick = hSelector.transform.Find("Prev").GetComponent<ButtonManager>().onClick;
        onClick.RemoveAllListeners();
        onClick.AddListener(delegate{QueueOfferMinus("Food");});

        onClick = hSelector.transform.Find("Next").GetComponent<ButtonManager>().onClick;
        onClick.RemoveAllListeners();
        onClick.AddListener(delegate{QueueOfferPlus("Food");});


        text = hSelector.transform.Find("Main Content").transform.Find("Text").GetComponent<TextMeshProUGUI>();
        //ButtonManager
        //onClick
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
        text.text = entry.Quantity.ToString("n0") + " (" + queuedOffer.ToString("n0") + ")";
    }
    // Update is called once per frame
    void Update()
    {
        Tick("Food");
    }
}
