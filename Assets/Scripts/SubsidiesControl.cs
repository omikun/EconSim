using System.Collections.Generic;
using System.Linq;
using Michsky.MUIP;
using UnityEngine;

public class SubsidiesControl : MonoBehaviour
{
	private SwitchManager enable;
	private Dictionary<string, SliderControl> controls = new();

	private void Awake()
	{
		//find controls and init them
		var window = GameObject.Find("Subsidies Window");
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
				.SetRoundValue(true);
		}
	}

	void UpdateEnable(bool value)
	{
		this.GetConfig().EnableSubsidies = value;
	}
	private SliderControl InitController(GameObject go, string name)
	{
		var slider = go.transform.Find(name + "Controller")
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
			this.GetConfig().SubsidiesRate[name] = control.value;
		}
	}
}