
using System.Collections.Generic;
using System.Linq;
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
		float[] amounts = { 20, 2, 2, 2, 2 };
		foreach (var (name, amount) in names.Zip(amounts, (a, b) => (a, b)))
		{
			controls[name] = InitController(content, name)
				.SetMinValue(0)
				.SetMaxValue(100f)
				.SetValue(amount)
				.SetPercent(false)
				.SetRoundValue(true)
				.SetWholeNumber(true);
		}
		SliderControl InitController(GameObject go, string name)
		{
			var slider = go.transform.Find(name+"Controller")
				.GetComponent<SliderManager>();
		
			return new SliderControl(slider, name);
		}
	}
}