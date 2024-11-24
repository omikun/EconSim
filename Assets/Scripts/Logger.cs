using System;
using System.Collections.Generic;
using System.IO;
public class Logger
{
	private SimulationConfig config;
	protected StreamWriter sw;

	public Logger(SimulationConfig cfg)
	{
		config = cfg;
	}
	public void OpenFileForWrite() {
		if (!config.EnableLog)
			return;
		var datepostfix = System.DateTime.Now.ToString(@"yyyy-MM-dd-h_mm_tt");
		if (config.appendTimeToLog)
		{
			sw = new StreamWriter("log_" + datepostfix + ".csv");
		} else {
			sw = new StreamWriter("log.csv");
		}
	}
	public void PrintToFile(string msg) {
		if (!config.EnableLog)
			return;
		sw.Write(msg);
	}

	public void CloseWriteFile() {
		if (!config.EnableLog)
			return;
		sw.Close();
	}
}
