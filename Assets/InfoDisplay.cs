using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;
using TMPro;
public class InfoDisplay : MonoBehaviour
{
    //happiness level
    [Required]
    public AuctionHouse district;
    TextMeshProUGUI text;
    float happiness = 0;
    float GetHappiness()
    {
        return district.GetHappiness();
    }
    // Start is called before the first frame update
    void Start()
    {
        text = GetComponent<TextMeshProUGUI>();   
    }

    // Update is called once per frame
    int lastRound = -1;
    void Update()
    {
        if (AuctionStats.Instance.round == lastRound)
        {
            return;
        }
        lastRound = AuctionStats.Instance.round;
        happiness = GetHappiness();
        text.text = "Approval: " + happiness.ToString("P2");

    }
}
