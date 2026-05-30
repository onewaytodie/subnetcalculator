using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;  // <-- добавить эту строку
using SubnetCalculator.Chat.Models;

namespace SubnetCalculator.Chat.Services
{
	public class ChatServer : IDisposable
	{
		private TcpListener _listener;
		private bool _isRunning;
		private readonly ConcurrentDictionary<TcpClient, string> _clients = new ConcurrentDictionary<TcpClient, string>();
		private readonly object _fileLock = new object();
		private readonly string _logFilePath;

		public ObservableCollection<string> ConnectedClients { get; private set; } = new ObservableCollection<string>();
		private readonly ConcurrentDictionary<string, ObservableCollection<ChatMessage>> _clientMessages = new ConcurrentDictionary<string, ObservableCollection<ChatMessage>>();

		public event Action<string> ClientConnected;
		public event Action<string> ClientDisconnected;
		public event Action<string, ChatMessage> ChatMessageReceived;

		public event Action<string> OnLog;
		public event Action<string> OnMessageReceived;

		public ChatServer(string logFilePath = "chat_log.txt")
		{
			_logFilePath = logFilePath;
		}

		public async Task StartAsync(int port)
		{
			if (_isRunning) return;
			try
			{
				_listener = new TcpListener(IPAddress.Any, port);
				_listener.Start();
				_isRunning = true;
				Log($"Сервер запущен на порту {port}");
				await AcceptClientsAsync();
			}
			catch (Exception ex)
			{
				Log($"Ошибка запуска: {ex.Message}");
				throw;
			}
		}

		public void Stop()
		{
			_isRunning = false;
			_listener?.Stop();
			foreach (var client in _clients.Keys)
				client.Close();
			_clients.Clear();
			ConnectedClients.Clear();
			_clientMessages.Clear();
			Log("Сервер остановлен.");
		}

		private async Task AcceptClientsAsync()
		{
			while (_isRunning)
			{
				try
				{
					TcpClient client = await _listener.AcceptTcpClientAsync();
					_ = Task.Run(() => HandleClientAsync(client));
				}
				catch (ObjectDisposedException) { break; }
				catch (Exception ex) { Log($"Ошибка Accept: {ex.Message}"); }
			}
		}

		private async Task HandleClientAsync(TcpClient client)
		{
			string endpoint = client.Client.RemoteEndPoint.ToString();
			System.IO.File.AppendAllText("debug_conn.txt", $"{DateTime.Now} Клиент {endpoint} подключился{Environment.NewLine}");

			string connectTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

			_clients[client] = endpoint;
			Application.Current.Dispatcher.Invoke(() => ConnectedClients.Add(endpoint));
			_clientMessages[endpoint] = new ObservableCollection<ChatMessage>();
			ClientConnected?.Invoke(endpoint);
			Log($"[{connectTime}] Клиент подключился: {endpoint}");

			NetworkStream stream = client.GetStream();
			byte[] buffer = new byte[4096];

			try
			{
				while (true)
				{
					int bytes = await stream.ReadAsync(buffer, 0, buffer.Length);
					if (bytes == 0) break;

					string msg = Encoding.UTF8.GetString(buffer, 0, bytes);
					System.IO.File.AppendAllText("debug_msgs.txt", $"{DateTime.Now} Получено от {endpoint}: {msg}{Environment.NewLine}");

					string time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
					string formatted = $"[{time}] {endpoint}: {msg}";
					Log(formatted);
					OnMessageReceived?.Invoke(formatted);

					var chatMsg = new ChatMessage
					{
						Author = endpoint,
						Text = msg,
						Timestamp = DateTime.Parse(time),
						IsOwn = false
					};
					if (_clientMessages.TryGetValue(endpoint, out var messages))
					{
						Application.Current.Dispatcher.Invoke(() => messages.Add(chatMsg));
					}
					await BroadcastAsync(formatted, client);
					ChatMessageReceived?.Invoke(endpoint, chatMsg);
				}
			}
			catch (Exception ex)
			{
				Log($"Ошибка с {endpoint}: {ex.Message}");
			}
			finally
			{
				_clients.TryRemove(client, out _);
				Application.Current.Dispatcher.Invoke(() => ConnectedClients.Remove(endpoint));
				ClientDisconnected?.Invoke(endpoint);
				Log($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Клиент отключился: {endpoint}");
				client.Close();
			}
		}

		private async Task BroadcastAsync(string message, TcpClient senderClient)
		{
			byte[] data = Encoding.UTF8.GetBytes(message);
			foreach (var client in _clients.Keys)
			{
				try
				{
					if (client.Connected)
						await client.GetStream().WriteAsync(data, 0, data.Length);
				}
				catch { }
			}
		}

		public async Task SendAdminMessageAsync(string message)
		{
			byte[] data = Encoding.UTF8.GetBytes(message);
			foreach (var client in _clients.Keys)
			{
				try
				{
					if (client.Connected)
						await client.GetStream().WriteAsync(data, 0, data.Length);
				}
				catch { }
			}
		}

		public ObservableCollection<ChatMessage> GetClientMessages(string endpoint)
		{
			return _clientMessages.TryGetValue(endpoint, out var messages) ? messages : null;
		}

		private void Log(string text)
		{
			OnLog?.Invoke(text);
			lock (_fileLock) { File.AppendAllText(_logFilePath, text + Environment.NewLine); }
		}

		public void Dispose()
		{
			Stop();
		}
	}
}
