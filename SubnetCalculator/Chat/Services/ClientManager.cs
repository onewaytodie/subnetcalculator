using System;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace SubnetCalculator.Chat.Services
{
	public class ClientManager : IDisposable
	{
		private TcpClient _client;
		private NetworkStream _stream;
		private bool _isConnected;
		private string _myNick;

		public event Action<string> MessageReceived;
		public event Action<string> StatusChanged;

		public bool IsConnected => _isConnected;
		public string MyNick => _myNick;

		public async Task<bool> ConnectAsync(string ip, int port, string nick)
		{
			try
			{
				_client = new TcpClient();
				await _client.ConnectAsync(ip, port);
				_stream = _client.GetStream();
				_myNick = nick;
				byte[] nickData = Encoding.UTF8.GetBytes($"/nick {nick}");
				await _stream.WriteAsync(nickData, 0, nickData.Length);

				byte[] buffer = new byte[4096];
				int bytes = await _stream.ReadAsync(buffer, 0, buffer.Length);
				string response = Encoding.UTF8.GetString(buffer, 0, bytes);
				if (response != "/nick ok")
				{
					StatusChanged?.Invoke($"error: {response}");
					return false;
				}

				_isConnected = true;
				StatusChanged?.Invoke("connected");
				_ = ReceiveMessagesAsync();
				return true;
			}
			catch (Exception ex)
			{
				StatusChanged?.Invoke($"error: {ex.Message}");
				return false;
			}
		}

		private async Task ReceiveMessagesAsync()
		{
			byte[] buffer = new byte[4096];
			try
			{
				while (_isConnected)
				{
					int bytes = await _stream.ReadAsync(buffer, 0, buffer.Length);
					if (bytes == 0) break;
					string msg = Encoding.UTF8.GetString(buffer, 0, bytes);
					MessageReceived?.Invoke(msg);
				}
			}
			catch (Exception ex)
			{
				MessageReceived?.Invoke($"[Ошибка] {ex.Message}");
			}
			finally
			{
				Disconnect();
			}
		}

		public async Task SendMessageAsync(string command)
		{
			if (!_isConnected) return;
			byte[] data = Encoding.UTF8.GetBytes(command);
			await _stream.WriteAsync(data, 0, data.Length);
		}

		public void Disconnect()
		{
			if (_isConnected)
			{
				_isConnected = false;
				_stream?.Close();
				_client?.Close();
				StatusChanged?.Invoke("disconnected");
			}
		}

		public void Dispose() => Disconnect();
	}
}
