using System;
using System.Collections.Generic;
using UnityEngine;
using Michsky.MUIP;
using Sirenix.Serialization;
using UnityEngine.Assertions;
using UnityEngine.Events;

public class IncomeTaxControl : MonoBehaviour
{
	[SerializeField] private List<RangeSliderControl> brackets = new();
	[SerializeField] private List<SliderControl> amounts = new();
	private bool reset = true;

	void Awake()
	{
		
		var window = GameObject.Find("Income Tax Window");
		var content = window.transform.Find("Content").gameObject;
		
		InitBrackets(content);
		InitAmounts(content);
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

		brackets[0].InitBracket(0, 5, 0, 1);
		brackets[1].InitBracket(1, 10, 1, 5);
		brackets[2].InitBracket(5, 100, 5, 100);
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
			amount.GetSlider().onValueChanged.AddListener(UpdateAmounts);
	}

	protected void UpdateAmounts(float value)
	{
		Debug.Log(name + " Current value: " + value.ToString());
	}
	protected void UpdateBrackets(int order)
	{
		if (!reset)
			return;
		Debug.Log("UpdateBrackets " + order);
		Assert.IsTrue(order >= 0 && order < brackets.Count);
		
		reset = false;
		brackets[0].slider.minSlider.Refresh(0);
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
	}

	private void LateUpdate()
	{
		reset = true;
	}
}