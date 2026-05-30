using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
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
		private readonly ConcurrentDictionary<string, TcpClient> _endpointToClient = new ConcurrentDictionary<string, TcpClient>();

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
			_endpointToClient.Clear();
			Application.Current.Dispatcher.Invoke(() => ConnectedClients.Clear());
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
			string connectTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

			_clients[client] = endpoint;
			_endpointToClient[endpoint] = client;

			// Добавляем в UI список (через Dispatcher)
			Application.Current.Dispatcher.Invoke(() => ConnectedClients.Add(endpoint));

			var messages = new ObservableCollection<ChatMessage>();
			_clientMessages[endpoint] = messages;

			var connectMsg = new ChatMessage
			{
				Author = "Система",
				Text = $"Клиент подключился: {endpoint} в {connectTime}",
				Timestamp = DateTime.Parse(connectTime),
				IsOwn = false
			};
			messages.Add(connectMsg);
			ChatMessageReceived?.Invoke(endpoint, connectMsg);
			Log($"[{connectTime}] Клиент подключился: {endpoint}");
			ClientConnected?.Invoke(endpoint);

			// Отправляем новому клиенту список всех подключённых (кроме него самого)
			await SendUserListToClient(client, endpoint);

			// Уведомляем всех остальных клиентов об обновлении списка
			await BroadcastUserList();

			NetworkStream stream = client.GetStream();
			byte[] buffer = new byte[4096];

			try
			{
				while (true)
				{
					int bytes = await stream.ReadAsync(buffer, 0, buffer.Length);
					if (bytes == 0) break;

					string rawMsg = Encoding.UTF8.GetString(buffer, 0, bytes);
					string time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

					// Обработка приватных сообщений
					if (rawMsg.StartsWith("/msg "))
					{
						// Формат: /msg получатель сообщение
						var parts = rawMsg.Substring(5).Split(new[] { ' ' }, 2);
						if (parts.Length == 2)
						{
							string recipient = parts[0];
							string message = parts[1];
							await SendPrivateMessage(endpoint, recipient, message, time);
						}
						else
						{
							await SendPrivateMessage(endpoint, endpoint, "Неверный формат. Используйте: /msg ник сообщение", time);
						}
						continue;
					}

					// Обычное сообщение (broadcast)
					string formatted = $"[{time}] {endpoint}: {rawMsg}";
					Log(formatted);
					OnMessageReceived?.Invoke(formatted);

					var chatMsg = new ChatMessage
					{
						Author = endpoint,
						Text = rawMsg,
						Timestamp = DateTime.Parse(time),
						IsOwn = false
					};
					messages.Add(chatMsg);
					ChatMessageReceived?.Invoke(endpoint, chatMsg);
					await BroadcastAsync(formatted, client);
				}
			}
			catch (Exception ex)
			{
				Log($"Ошибка с {endpoint}: {ex.Message}");
			}
			finally
			{
				_clients.TryRemove(client, out _);
				_endpointToClient.TryRemove(endpoint, out _);
				Application.Current.Dispatcher.Invoke(() => ConnectedClients.Remove(endpoint));
				string disconnectTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
				var disconnectMsg = new ChatMessage
				{
					Author = "Система",
					Text = $"Клиент отключился: {endpoint} в {disconnectTime}",
					Timestamp = DateTime.Parse(disconnectTime),
					IsOwn = false
				};
				if (_clientMessages.TryGetValue(endpoint, out var clientMsgs))
					clientMsgs.Add(disconnectMsg);
				ChatMessageReceived?.Invoke(endpoint, disconnectMsg);
				Log($"[{disconnectTime}] Клиент отключился: {endpoint}");
				ClientDisconnected?.Invoke(endpoint);
				await BroadcastUserList(); // уведомляем остальных об изменении списка
				client.Close();
			}
		}

		private async Task SendUserListToClient(TcpClient client, string selfEndpoint)
		{
			var list = new StringBuilder();
			list.Append("/users ");
			foreach (var ep in _endpointToClient.Keys)
			{
				if (ep != selfEndpoint)
					list.Append(ep).Append(",");
			}
			string userListMsg = list.ToString().TrimEnd(',');
			byte[] data = Encoding.UTF8.GetBytes(userListMsg);
			await client.GetStream().WriteAsync(data, 0, data.Length);
		}

		private async Task BroadcastUserList()
		{
			var list = new StringBuilder();
			list.Append("/users ");
			foreach (var ep in _endpointToClient.Keys)
				list.Append(ep).Append(",");
			string userListMsg = list.ToString().TrimEnd(',');
			byte[] data = Encoding.UTF8.GetBytes(userListMsg);
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

		private async Task SendPrivateMessage(string sender, string recipient, string message, string time)
		{
			if (!_endpointToClient.TryGetValue(recipient, out var recipientClient))
			{
				// отправить отправителю сообщение об ошибке
				if (_endpointToClient.TryGetValue(sender, out var senderClient2))
				{
					string errorMsg = $"[Система] Пользователь {recipient} не найден.";
					byte[] data = Encoding.UTF8.GetBytes(errorMsg);
					await senderClient2.GetStream().WriteAsync(data, 0, data.Length);
				}
				return;
			}

			string formatted = $"[{time}] Приватно от {sender}: {message}";
			// Сохраняем в историю отправителя и получателя
			var senderMsg = new ChatMessage
			{
				Author = sender,
				Text = $"Приватно для {recipient}: {message}",
				Timestamp = DateTime.Parse(time),
				IsOwn = true
			};
			var recipientMsg = new ChatMessage
			{
				Author = sender,
				Text = message,
				Timestamp = DateTime.Parse(time),
				IsOwn = false
			};

			if (_clientMessages.TryGetValue(sender, out var senderMsgs))
				senderMsgs.Add(senderMsg);
			if (_clientMessages.TryGetValue(recipient, out var recipientMsgs))
				recipientMsgs.Add(recipientMsg);

			ChatMessageReceived?.Invoke(sender, senderMsg);
			ChatMessageReceived?.Invoke(recipient, recipientMsg);

			// Отправляем получателю
			byte[] dataToRecipient = Encoding.UTF8.GetBytes($"[{time}] {sender}: {message}");
			await recipientClient.GetStream().WriteAsync(dataToRecipient, 0, dataToRecipient.Length);

			// Подтверждение отправителю (опционально)
			if (_endpointToClient.TryGetValue(sender, out var senderClient))
			{
				string confirmation = $"[{time}] Вы отправили {recipient}: {message}";
				byte[] dataToSender = Encoding.UTF8.GetBytes(confirmation);
				await senderClient.GetStream().WriteAsync(dataToSender, 0, dataToSender.Length);
			}

			Log($"Приватное сообщение от {sender} к {recipient}: {message}");
		}

		private async Task BroadcastAsync(string message, TcpClient skipClient)
		{
			byte[] data = Encoding.UTF8.GetBytes(message);
			foreach (var client in _clients.Keys)
			{
				if (client == skipClient) continue;
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
			lock (_fileLock)
			{
				File.AppendAllText(_logFilePath, text + Environment.NewLine);
			}
		}

		public void Dispose() => Stop();
	}
}