using Michsky.MUIP; // MUIP namespace
using UnityEngine;

public class SliderControl 
{
	[SerializeField] private SliderManager slider;
	[SerializeField] private string name;

	public SliderControl(SliderManager slider, string n)
	{
		name = n;
		this.slider = slider;
		this.slider.mainSlider.minValue = 0; // Change min value
		this.slider.mainSlider.maxValue = .4f; // Change max value
		this.slider.mainSlider.value = .1f; // Change current slider value
		this.slider.usePercent = true; // Enabling/disabling percent
		this.slider.useRoundValue = false; // Show simplifed value
		this.slider.mainSlider.onValueChanged.AddListener(TestFunction); // Add new onValueChanged event
	}

	public SliderControl SetRoundValue(bool value)
	{
		slider.useRoundValue = value;
		return this;
	}

	public SliderControl SetMinValue(float value)
	{
		slider.mainSlider.minValue = value;
		return this;
	}
	public SliderControl SetMaxValue(float value)
	{
		slider.mainSlider.maxValue = value;
		return this;
	}
	public SliderControl SetValue(float value)
	{
		slider.mainSlider.value = value;
		return this;
	}

	public SliderManager GetSlider()
	{
		return slider;
	}

	void TestFunction(float value)
	{
		Debug.Log(name + " Current value: " + value.ToString());
	}
}