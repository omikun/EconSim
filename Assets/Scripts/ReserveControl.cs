
using System.Collections.Generic;
using System.Linq;
using Michsky.MUIP;
using UnityEngine;

public class ReserveControl : MonoBehaviour
{
	public GameObject window;
	private SwitchManager enable;
	private Dictionary<string, SliderControl> controls = new();
	void Awake()
	{
		// var window = GameObject.Find("Reserve Window");
		var content = window.transform.Find("Content").gameObject;
		string[] names = { "Food", "Wood", "Ore", "Metal", "Tool" };
		float[] amounts = { 20, 2, 2, 2, 2 };
		
		enable = content.transform.Find("Enable")
			.GetComponent<SwitchManager>();
		enable.onValueChanged.AddListener(UpdateEnable);
		
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
		//init reserve amounts in government inventory
	}
	SliderControl InitController(GameObject go, string name)
	{
		var slider = go.transform.Find(name+"Controller")
			.GetComponent<SliderManager>();
		
		var sc = new SliderControl(slider, name);
		sc.GetSliderEvent().AddListener(UpdateControls);
		return sc;
	}
	void UpdateEnable(bool value)
	{
		this.GetConfig().EnableReserve = value;
	}
	void UpdateControls(float value)
	{
		Debug.Log(name + " Current value: " + value.ToString());
		if (value > 0)
			enable.SetOn();
		else if (value == 0)
		{
			var sum = controls.Values.Sum(c => c.value);
			if (sum == 0)
				enable.SetOff();
		}
		foreach (var (name, control) in controls)
		{
			this.GetConfig().Reserves[name] = control.value;
		}
	}
}