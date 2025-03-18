using Michsky.MUIP;
using UnityEngine;

public class SubsidiesControl : MonoBehaviour
{
	[SerializeField] private SliderControl FoodControl;
	[SerializeField] private SliderControl WoodControl;
	[SerializeField] private SliderControl OreControl;
	[SerializeField] private SliderControl MetalControl;
	[SerializeField] private SliderControl ToolControl;

	private void Awake()
	{
		//find controls and init them
		var window = GameObject.Find("Subsidies Window");
		var content = window.transform.Find("Content").gameObject;

		FoodControl = InitController(content, "Food");
		WoodControl = InitController(content, "Wood");
		OreControl = InitController(content, "Ore");
		MetalControl = InitController(content, "Metal");
		ToolControl = InitController(content, "Tool");
	}

	private SliderControl InitController(GameObject go, string name)
	{
		var slider = go.transform.Find(name + "Controller")
			.GetComponent<SliderManager>();

		return new SliderControl(slider, name);
	}
}