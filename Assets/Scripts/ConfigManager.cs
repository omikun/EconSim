using UnityEngine;
using UnityEngine.Serialization;

public static class GameConfigExtensions
{
	public static SimulationConfig GetConfig(this MonoBehaviour obj)
	{
		return ConfigManager.Config;
	}
}
public class ConfigManager : MonoBehaviour
{
	public static ConfigManager Instance { get; private set; }
	public static SimulationConfig Config => Instance.config;
	
	[SerializeField] private SimulationConfig config;
	
	private void Awake()
	{
		if (Instance == null)
		{
			Instance = this;
			DontDestroyOnLoad(gameObject);
		}
		else
		{
			Destroy(gameObject);
		}
	}
}