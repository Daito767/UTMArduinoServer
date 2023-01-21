using System.Net;

namespace ConnetToArduino.Data
{
	public class ArduinoClient
	{
		public IPEndPoint EndPoint;
		public string Id;
		public string Board;
		public string Platform;
		public Dictionary<string, string[]> Commands;
		public ConnectionState Status;
		public DateTime LastConnection;
		public DateTime FirstConnection;
	}

	public enum CommandTypes
	{
		ButtonTF,
		InputVariable,
		SimpleText
	}

	public enum ConnectionState
	{
		Online,
		Offline
	}
}
