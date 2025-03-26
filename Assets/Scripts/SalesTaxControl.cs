using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Michsky.MUIP;
using Sirenix.OdinInspector; // MUIP namespace

public class SalesTaxControl : MonoBehaviour
{
	private SwitchManager enable;
	[ShowInInspector]
	private Dictionary<string, SliderControl> controls = new();

	public GameObject window;
	void Start()
	{
		//find controls and init them
		// var window = GameObject.Find("Sales Tax Window");
		var content = window.transform.Find("Content").gameObject;
		
		enable = content.transform.Find("Enable")
			.GetComponent<SwitchManager>();
		enable.onValueChanged.AddListener(UpdateEnable);

		string[] names = { "Food", "Wood", "Ore", "Metal", "Tool" };
		foreach (var name in names)
		{
			controls[name] = InitController(content, name)
				.SetMinValue(0)
				.SetMaxValue(1f)
				.SetValue(.0f)
				.SetPercent(true)
				.SetRoundValue(false);
		}
	}

	void UpdateEnable(bool value)
	{
		this.GetConfig().EnableSalesTax = value;
	}
	SliderControl InitController(GameObject go, string name)
	{
		var slider = go.transform.Find(name+"Controller")
							 .GetComponent<SliderManager>();
		
		var sc = new SliderControl(slider, name);
		sc.GetSliderEvent().AddListener(UpdateControls);
		return sc;
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
			this.GetConfig().SalesTaxRate[name] = control.value;
		}
	}

}