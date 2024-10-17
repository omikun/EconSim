using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using AYellowpaper.SerializedCollections;
using Sirenix.OdinInspector;
using Michsky.MUIP;
using UnityEngine.UI;

public class Player : MonoBehaviour
{
    [Required]
    public AuctionHouse selectedDistrict;
    public Government selectedAgent;
    [Required]
    public GameObject hSelector;
    // Start is called before the first frame update
    void Start()
    {
        selectedAgent = (Government)selectedDistrict.gov;
        InitTradeUI();
    }

    void InitTradeUI()
    {
        //get prev, inject QueueOfferMinus into it
        var onClick = hSelector.transform.Find("Prev").GetComponent<ButtonManager>().onClick;
        onClick.RemoveAllListeners();
        onClick.AddListener(delegate{QueueOfferMinus("Food");});

        onClick = hSelector.transform.Find("Prev").GetComponent<ButtonManager>().onClick;
        onClick.RemoveAllListeners();
        onClick.AddListener(delegate{QueueOfferPlus("Food");});


        //ButtonManager
        //onClick
    }
    public void QueueOfferMinus(string com)
    {
        selectedAgent.QueueOffer(com, -5f);
        Debug.Log("QueueOfferMinus!");
    }
    public void QueueOfferPlus(string com)
    {
        selectedAgent.QueueOffer(com, 5f);
        Debug.Log("QueueOfferPlus!");
    }
    // Update is called once per frame
    void Update()
    {
        
    }
}
