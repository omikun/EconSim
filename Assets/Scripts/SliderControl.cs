using Michsky.MUIP; // MUIP namespace
using UnityEngine;
using UnityEngine.UI;

public class SliderControl 
{
	[SerializeField] private SliderManager slider;
	[SerializeField] private string name;
	[SerializeField] public float SavedValue;

	public SliderControl(SliderManager slider, string n)
	{
		name = n;
		this.slider = slider;
		this.slider.mainSlider.onValueChanged.AddListener(TestFunction); // Add new onValueChanged event
	}

	public SliderControl SetPercent(bool value)
	{
		slider.usePercent = value;
		return this;
	}

	public SliderControl SetWholeNumber(bool value)
	{
		slider.mainSlider.wholeNumbers = value;
		return this;
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
	public float value { get { return slider.mainSlider.value; } }

	public SliderManager GetSlider()
	{
		return slider;
	}

	public Slider.SliderEvent GetSliderEvent()
	{
		return slider.mainSlider.onValueChanged;
	}

	void TestFunction(float value)
	{
		Debug.Log(name + " Current value: " + value.ToString());
	}
}