using System;
using Michsky.MUIP;
using UnityEngine;
using UnityEngine.Events;

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
		slider.minSlider.minValue = min;
		slider.minSlider.maxValue = max;
		slider.maxSlider.minValue = min;
		slider.maxSlider.maxValue = max;
		slider.minSlider.Refresh(lowValue);
		slider.maxSlider.SetValue(highValue);
		Debug.Log("RangeSliderControl" + order + " init");
	}

	public void RegisterCallBacks(Action updateAction)
	{
		// slider.minSlider.onValueChanged.AddListener((value) => updateAction?.Invoke());
		// slider.maxSlider.onValueChanged.AddListener((value) => updateAction?.Invoke());
		slider.minSlider.onValueChanged.AddListener(UpdateBracket);
		slider.maxSlider.onValueChanged.AddListener(UpdateBracket);
	}

	public void UpdateBracket(float value)
	{
		Debug.Log(
			"RangeSliderControl" + order + " values: " + slider.CurrentLowerValue + "/" + slider.CurrentUpperValue);
		onValueChanged.Invoke(order);
	}
}