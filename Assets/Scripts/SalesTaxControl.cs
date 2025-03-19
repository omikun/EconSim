using System.Collections.Generic;
using UnityEngine;
using Michsky.MUIP; // MUIP namespace

public class SalesTaxControl : MonoBehaviour
{
	private Dictionary<string, SliderControl> controls = new();
	void Awake()
	{
		//find controls and init them
		var window = GameObject.Find("Sales Tax Window");
		var content = window.transform.Find("Content").gameObject;

		string[] names = { "Food", "Wood", "Ore", "Metal", "Tool" };
		foreach (var name in names)
		{
			controls[name] = InitController(content, name)
				.SetMinValue(0)
				.SetMaxValue(.4f)
				.SetValue(.1f)
				.SetPercent(true)
				.SetRoundValue(false);
			// controls[name].GetSlider().mainSlider.onValueChanged.AddListener(TestFunction);
		}
	}

	SliderControl InitController(GameObject go, string name)
	{
		var slider = go.transform.Find(name+"Controller")
							 .GetComponent<SliderManager>();
		
	 	return new SliderControl(slider, name);
	}

}