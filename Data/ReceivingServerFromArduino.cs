using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace ConnetToArduino.Data
{
	public class ReceivingServerFromArduino
	{
		public string IpHost { get { return ipHost; } }

		private string ipHost = "0.0.0.0";
		public const int PortHost = 8080;
		private const int SecondsToOffline = 10; //Peste cate secunde clientul se socoate offline.
		private const int SecondsToDeleleteClient = 60; //Peste cat timp clientul va fi strs din lista.

		public List<ArduinoClient> clients = new List<ArduinoClient>();
		private IPEndPoint recivedClientIP;
		private UdpClient server;

		private TimeZoneInfo cstZone = TimeZoneInfo.FindSystemTimeZoneById("GTB Standard Time");

		public ReceivingServerFromArduino()
		{
			ipHost = GetLocalIPv4(NetworkInterfaceType.Wireless80211);
			Console.WriteLine(ipHost);

			server = new UdpClient(PortHost);

			Task.Run(() => RecieveMessages());
			Task.Run(() => DetectOfflienClients());
		}

		public void SendCmdToClient(ArduinoClient client, string valName, string vlaue)
		{
			SendMsg($"{valName}:{vlaue};", client.EndPoint);
		}

		private void RecieveMessages()
		{
			while (true)
			{
				try
				{
					byte[] data = server.Receive(ref recivedClientIP);
					string message = Encoding.UTF8.GetString(data);

					if (!IsRegistred(recivedClientIP))
					{
						if (TryToRegister(recivedClientIP, message))
						{
							PrintClientLogs("Inregistrare cu succes", recivedClientIP);
							SendMsg("Succes", recivedClientIP);
						}
						else
						{
							PrintClientLogs("Esuarea inregistrarii", recivedClientIP);
							SendMsg("Error", recivedClientIP);
						}

						continue;
					}

					UpdateClient(recivedClientIP, message);
				}
				catch (Exception ex)
				{
					Console.WriteLine(ex.Message);
				}
			}
		}

		private void PrintClientLogs(string msg, ArduinoClient client)
		{
			Console.WriteLine($"{msg}: " + client.Id + "; ip: " + client.EndPoint.Address.ToString() + " port: " + client.EndPoint.Port);
		}

		private void PrintClientLogs(string msg, IPEndPoint clientEndPoint)
		{
			Console.WriteLine($"{msg}: " + " Ip: " + clientEndPoint.Address.ToString() + " port: " + clientEndPoint.Port);
		}

		private void DetectOfflienClients()
		{
			//Aceasta functie detecteaza si sterge clientii ofline

			while (true)
			{
				try
				{
					for (int i = 0; i < clients.Count; i++)
					{
						ArduinoClient cc = clients[i]; // Nu stiu dc, daca fac fara variabila data da eroare
						TimeSpan interval = GetDTNow() - cc.LastConnection; // <- ^^^ aici
						if (interval.TotalSeconds > SecondsToDeleleteClient)
						{
							PrintClientLogs("Client sters", clients[i]);
							clients.RemoveAt(i);
						}
						else if (interval.TotalSeconds > SecondsToOffline && cc.Status == ConnectionState.Online)
						{
							UpdateClientStatus(i, ConnectionState.Offline);
							PrintClientLogs("Client offline", clients[i]);

						}
					}

					Thread.Sleep(1000);
				}
				catch (Exception ex)
				{
					Console.WriteLine(ex.Message);
				}
			}
		}

		private void UpdateClient(IPEndPoint client, string msg)
		{
			//Functia raspunde de gasirea si actualizarea statutului (offline sau online) al clientului.

			for (int i = 0; i < clients.Count; i++)
			{
				if (clients[i].EndPoint.Address.ToString() == client.Address.ToString() && clients[i].EndPoint.Port == client.Port)
				{
					if (clients[i].Status == ConnectionState.Offline)
					{
						PrintClientLogs("Client online", clients[i]);
					}

					Dictionary<string, string[]> cmds = ParseMsg(msg);

					clients[i].Commands = cmds;
					clients[i].Status = ConnectionState.Online;
					clients[i].LastConnection = GetDTNow();

					return;
				}
			}
		}

		private void UpdateClientStatus(int index, ConnectionState status)
		{

			if (index < clients.Count)
			{
				clients[index].Status = status;
			}
		}

		private int SendMsg(string text, IPEndPoint client)
		{
			byte[] byteMsg = Encoding.UTF8.GetBytes(text);
			return server.Send(byteMsg, byteMsg.Length, client);
		}

		private bool IsRegistred(IPEndPoint aClient)
		{
			foreach (ArduinoClient item in clients)
			{
				if (item.EndPoint.Address.ToString() == aClient.Address.ToString() && item.EndPoint.Port == aClient.Port)
				{
					return true;
				}
			}
			return false;
		}

		private bool TryToRegister(IPEndPoint aClient, string msg)
		{

			Dictionary<string, string[]> cmds = ParseMsg(msg);
			ArduinoClient client = new ArduinoClient();
			client.EndPoint = aClient;
			if (cmds.Count > 0 && cmds.ContainsKey("Id") && cmds.ContainsKey("Board") && cmds.ContainsKey("Platform"))
			{
				client.Id = cmds["Id"][0];
				client.Board = cmds["Board"][0];
				client.Platform = cmds["Platform"][0];
				client.Commands = cmds;
				client.Status = ConnectionState.Online;
				client.FirstConnection = GetDTNow();
				client.LastConnection = client.FirstConnection;

				clients.Add(client);

				return true;
			}

			return false;
		}

		private Dictionary<string, string[]> ParseMsg(string msg)
		{
			//Aceasta functie imparte in parti mesajul primit in foma de text.

			Dictionary<string, string[]> Cmds = new Dictionary<string, string[]>();

			string[] splitedCmds = msg.Split(';');
			foreach (string cmd in splitedCmds)
			{
				string[] splitedCmd = cmd.Split(':');
				if (splitedCmd.Length > 1)
				{
					Cmds.Add(splitedCmd[0], splitedCmd[1].Split(','));
				}
			}

			return Cmds;
		}

		private DateTime GetDTNow()
		{
			return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, cstZone);
		}

		private string GetLocalIPv4(NetworkInterfaceType _type)
		{
			string output = "";
			foreach (NetworkInterface item in NetworkInterface.GetAllNetworkInterfaces())
			{
				if (item.NetworkInterfaceType == _type && item.OperationalStatus == OperationalStatus.Up)
				{
					foreach (UnicastIPAddressInformation ip in item.GetIPProperties().UnicastAddresses)
					{
						if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
						{
							output = ip.Address.ToString();
						}
					}
				}
			}
			return output;
		}
	}
}
