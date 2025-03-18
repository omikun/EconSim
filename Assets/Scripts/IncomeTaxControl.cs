using System;
using System.Collections.Generic;
using UnityEngine;
using Michsky.MUIP;
using Sirenix.Serialization;
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

		brackets[1].InitBracket(1, 10, 1, 5);
		brackets[0].InitBracket(0, 3, 0, 1);
		brackets[2].InitBracket(5, 100, 5, 100);
		reset = false;
		
		foreach (var bracket in brackets)
			bracket.onValueChanged.AddListener(UpdateBrackets);
		foreach (var bracket in brackets)
			bracket.InitBracketCallBacks();
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
		
		Debug.Log("UpdateBrackets");
		reset = false;
		brackets[0].slider.minSlider.value = 0;
		brackets[2].slider.maxSlider.value = 100;
		if (order > 0)
		{
			var changedValue = brackets[order].slider.CurrentLowerValue;
			var preValue = brackets[order - 1].slider.CurrentUpperValue;
			var maxValue = brackets[order - 1].slider.maxValue;
			// brackets[order - 1].slider.maxSlider.Refresh(brackets[order].slider.CurrentLowerValue);
			brackets[order - 1].slider.maxSlider.Refresh(maxValue - changedValue);
			var postValue = brackets[order - 1].slider.CurrentUpperValue;
			Debug.Log("UpdateBrackets changed brackets[" + order + "].lowerValue=" + changedValue 
			          + " => brackets[" + (order - 1) + "].upperValue=" + preValue + " = " + postValue);
		}

		if (order < 2)
		{
			var changedValue = brackets[order].slider.CurrentUpperValue;
			var preValue = brackets[order + 1].slider.CurrentLowerValue;
			Debug.Log("UpdateBrackets changed brackets[" + order + "].lowerValue=" + changedValue 
			          + " => brackets[" + (order + 1) + "].upperValue=" + preValue);
			brackets[order + 1].slider.minSlider.Refresh(brackets[order].slider.CurrentUpperValue);
		}
	}

	private void LateUpdate()
	{
		reset = true;
	}
}

public class RangeSliderControl
{
	public RangeSlider slider;
	protected int order;
	public UnityEvent<int> onValueChanged = new();

	public RangeSliderControl(int i, RangeSlider rs)
	{
		slider = rs;
		order = i;
	}
	
	public void InitBracket(float min, float max, float lowValue, float highValue)
	{
		slider.minValue = min;
		slider.maxValue = max;
		slider.minSlider.Refresh(lowValue);
		slider.maxSlider.Refresh(highValue);
		// slider.onValueChanged.AddListener(Update);
		Debug.Log("RangeSliderControl" + order + " init");
	}

	public void InitBracketCallBacks()
	{
		slider.minSlider.onValueChanged.AddListener(UpdateBracket);
		slider.maxSlider.onValueChanged.AddListener(UpdateBracket);
	}

	public void UpdateBracket(float value)
	{
		Debug.Log("RangeSliderControl" + order + " values: " + slider.CurrentLowerValue + "/" + slider.CurrentUpperValue);
		onValueChanged.Invoke(order);

	}
}