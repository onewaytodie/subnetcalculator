using System;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace SubnetCalculator.Chat.Services
{
	public class ChatClient : IDisposable
	{
		private TcpClient _client;
		private NetworkStream _stream;
		private bool _connected;

		public event Action<string> OnMessageReceived;
		public event Action<string> OnStatusChanged;

		public async Task ConnectAsync(string ip, int port)
		{
			try
			{
				_client = new TcpClient();
				await _client.ConnectAsync(ip, port);
				_stream = _client.GetStream();
				_connected = true;
				OnStatusChanged?.Invoke("connected");
				_ = ReceiveMessagesAsync();
			}
			catch (Exception ex)
			{
				OnStatusChanged?.Invoke($"error: {ex.Message}");
				throw;
			}
		}

		private async Task ReceiveMessagesAsync()
		{
			byte[] buffer = new byte[4096];
			try
			{
				while (_connected)
				{
					int bytes = await _stream.ReadAsync(buffer, 0, buffer.Length);
					if (bytes == 0) break;
					string msg = Encoding.UTF8.GetString(buffer, 0, bytes);
					OnMessageReceived?.Invoke(msg);
				}
			}
			catch (Exception ex)
			{
				OnMessageReceived?.Invoke($"[Ошибка] {ex.Message}");
			}
			finally
			{
				Disconnect();
			}
		}

		public async Task SendMessageAsync(string message)
		{
			if (!_connected) return;
			byte[] data = Encoding.UTF8.GetBytes(message);
			await _stream.WriteAsync(data, 0, data.Length);
		}

		public void Disconnect()
		{
			_connected = false;
			_stream?.Close();
			_client?.Close();
			OnStatusChanged?.Invoke("disconnected");
		}

		public void Dispose() => Disconnect();
	}
}
