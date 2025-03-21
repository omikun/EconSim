using System;
using System.Collections.Generic;
using UnityEngine;
using Michsky.MUIP;
using Sirenix.Serialization;
using UnityEngine.Assertions;
using UnityEngine.Events;

public class IncomeTaxControl : MonoBehaviour
{
	private SwitchManager enable;
	private List<RangeSliderControl> brackets = new();
	private List<SliderControl> amounts = new();
	private bool reset = true;

	void Awake()
	{
		
		var window = GameObject.Find("Income Tax Window");
		var content = window.transform.Find("Content").gameObject;
		
		enable = content.transform.Find("Enable")
			.GetComponent<SwitchManager>();
		enable.onValueChanged.AddListener(UpdateEnable);
		InitBrackets(content);
		InitAmounts(content);
	}
	void UpdateEnable(bool value)
	{
		this.GetConfig().EnableIncomeTax = value;
	}

	protected RangeSlider FindRangeSlider(GameObject go, string name)
	{
		var slider = go.transform.Find(name)
			.GetComponent<RangeSlider>();
		return slider;
	}
	protected SliderManager FindSlider(GameObject go, string name)
	{
		return go.transform.Find(name)
			.GetComponent<SliderManager>();
	}

	protected SliderControl InitSliderControl(GameObject go, string name)
	{
		var slider = FindSlider(go, name);
		return new(slider, name);
	}

	protected void InitBrackets(GameObject content)
	{
		brackets.Add(new (0, FindRangeSlider(content, "TaxBracket1")));
		brackets.Add(new (1, FindRangeSlider(content, "TaxBracket2")));
		brackets.Add(new (2, FindRangeSlider(content, "TaxBracket3")));

		brackets[0].InitBracket(0, 5, 1, 5);
		brackets[1].InitBracket(1, 10, 5, 10);
		brackets[2].InitBracket(5, 100, 10, 100);
		reset = false;

		// for (int i = 0; i < brackets.Count; i++)
			// brackets[i].RegisterCallBacks(() => UpdateBrackets(i));
		foreach (var bracket in brackets)
			bracket.onValueChanged.AddListener(UpdateBrackets); //updates all brackets
		foreach (var bracket in brackets)
			bracket.RegisterCallBacks(null); //this call back will call UpdateBrackets
	}

	protected void InitAmounts(GameObject content)
	{
		string[] names = { "TaxAmount1", "TaxAmount2", "TaxAmount3" };
		float[] values = { .1f, .2f, .4f };
		for (int i = 0; i < names.Length; i++)
		{
			var sc = InitSliderControl(content, names[i])
				.SetMaxValue(1)
				.SetMinValue(0)
				.SetValue(values[i])
				.SetRoundValue(false);
			amounts.Add(sc);
		}
		
		foreach (var amount in amounts)
			amount.GetSliderEvent().AddListener(UpdateAmounts);
	}

	protected void UpdateAmounts(float value)
	{
		string msg = " Current value: ";
		for (int i = 0; i < amounts.Count; i++)
		{
			this.GetConfig().taxBrackets[i].taxRate = amounts[i].value;
			msg += name + ": " + amounts[i].value + " ";
		}
		Debug.Log(msg);
	}
	protected void UpdateBrackets(int order)
	{
		if (!reset) //prevent infinite update callbacks
			return;
		Debug.Log("UpdateBrackets " + order);
		Assert.IsTrue(order >= 0 && order < brackets.Count);
		
		reset = false;
		brackets[2].slider.maxSlider.SetValue(100);
		if (order > 0)
		{
			var changedValue = brackets[order].slider.CurrentLowerValue;
			brackets[order - 1].slider.maxSlider.SetValue(changedValue);
		}
		
		if (order < 2)
		{
			brackets[order + 1].slider.minSlider.Refresh(brackets[order].slider.CurrentUpperValue);
		}

		for (int i = 0; i < brackets.Count; i++)
		{
			this.GetConfig().taxBrackets[i].min = brackets[i].slider.CurrentLowerValue;
			this.GetConfig().taxBrackets[i].max = brackets[i].slider.CurrentUpperValue;
		}
	}

	private void LateUpdate()
	{
		reset = true;
	}
}