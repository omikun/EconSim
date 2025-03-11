using UnityEngine;
using UnityEngine.Profiling;

public class FrameProfiler : MonoBehaviour
{
    private bool profiling = false;

    public void StartProfiling()
    {
        StartCoroutine(ProfileSingleFrame());
        Debug.Log("Profile enabled");
    }

    private System.Collections.IEnumerator ProfileSingleFrame()
    {
        Profiler.enableBinaryLog = true;
        Profiler.logFile = Application.persistentDataPath + "/profiledata.raw";
        Profiler.enabled = true;

        profiling = true;
        yield return new WaitForEndOfFrame(); // Wait until the frame is fully rendered

        Profiler.enabled = false;
        profiling = false;

        Debug.Log("Profile data saved at: " + Profiler.logFile);
    }
}