
using System.Collections.Generic;
using Michsky.MUIP;
using UnityEngine;

public class ReserveControl : MonoBehaviour
{
	
	private Dictionary<string, SliderControl> controls = new();
	void Awake()
	{
		var window = GameObject.Find("Reserve Window");
		var content = window.transform.Find("Content").gameObject;
		string[] names = { "Food", "Wood", "Ore", "Metal", "Tool" };
		float[] amount = { 20, 2, 2, 2, 2 };
		foreach (var name in names)
		{
			controls[name] = InitController(content, name)
				.SetMinValue(0)
				.SetMaxValue(100f)
				.SetValue(2f)
				.SetPercent(false)
				.SetRoundValue(true)
				.SetWholeNumber(true);
			// controls[name].GetSlider().mainSlider.onValueChanged.AddListener(TestFunction);
		}
		SliderControl InitController(GameObject go, string name)
		{
			var slider = go.transform.Find(name+"Controller")
				.GetComponent<SliderManager>();
		
			return new SliderControl(slider, name);
		}
	}
}