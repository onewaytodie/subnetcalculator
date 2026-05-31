using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
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
		private TcpListener listener;
		private bool isRunning;
		private readonly ConcurrentDictionary<TcpClient, string> clientNick = new ConcurrentDictionary<TcpClient, string>();
		private readonly ConcurrentDictionary<string, TcpClient> nickToClient = new ConcurrentDictionary<string, TcpClient>();
		private readonly object fileLock = new object();
		private readonly string logFilePath;

		public ObservableCollection<string> ConnectedClients { get; private set; } = new ObservableCollection<string>();
		private readonly ConcurrentDictionary<string, ObservableCollection<ChatMessage>> dialogs = new ConcurrentDictionary<string, ObservableCollection<ChatMessage>>();
		private ObservableCollection<ChatMessage> broadcastMessages = new ObservableCollection<ChatMessage>();

		public event Action<string> ClientConnected;
		public event Action<string> ClientDisconnected;
		public event Action<string, ChatMessage> DialogMessageReceived;
		public event Action<ChatMessage> BroadcastMessageReceived;

		public event Action<string> OnLog;
		public event Action<string> OnMessageReceived;

		public ChatServer(string logFilePath = "chat_log.txt")
		{
			this.logFilePath = logFilePath;
		}

		public async Task StartAsync(int port)
		{
			if (isRunning) return;
			try
			{
				listener = new TcpListener(IPAddress.Any, port);
				listener.Start();
				isRunning = true;
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
			isRunning = false;
			listener?.Stop();
			foreach (TcpClient client in clientNick.Keys)
				client.Close();
			clientNick.Clear();
			nickToClient.Clear();
			ConnectedClients.Clear();
			dialogs.Clear();
			broadcastMessages.Clear();
			Log("Сервер остановлен.");
		}

		private async Task AcceptClientsAsync()
		{
			while (isRunning)
			{
				try
				{
					TcpClient client = await listener.AcceptTcpClientAsync();
					_ = Task.Run(() => HandleClientAsync(client));
				}
				catch (ObjectDisposedException) { break; }
				catch (Exception ex) { Log($"Ошибка Accept: {ex.Message}"); }
			}
		}

		private async Task HandleClientAsync(TcpClient client)
		{
			string endpoint = client.Client.RemoteEndPoint.ToString();
			Log($"Новый клиент {endpoint} пытается подключиться.");

			NetworkStream stream = client.GetStream();
			byte[] buffer = new byte[4096];

			int bytes = await stream.ReadAsync(buffer, 0, buffer.Length);
			if (bytes == 0) return;
			string firstMsg = Encoding.UTF8.GetString(buffer, 0, bytes).Trim();

			if (!firstMsg.StartsWith("/nick "))
			{
				await SendErrorMessage(client, "Необходимо указать ник: /nick ВашНик");
				client.Close();
				return;
			}

			string requestedNick = firstMsg.Substring(6).Trim();
			if (string.IsNullOrEmpty(requestedNick))
			{
				await SendErrorMessage(client, "Ник не может быть пустым.");
				client.Close();
				return;
			}

			if (nickToClient.Count >= 10)
			{
				await SendErrorMessage(client, "Достигнуто максимальное количество клиентов (10).");
				client.Close();
				return;
			}

			if (nickToClient.ContainsKey(requestedNick))
			{
				await SendErrorMessage(client, $"Ник '{requestedNick}' уже занят.");
				client.Close();
				return;
			}

			clientNick[client] = requestedNick;
			nickToClient[requestedNick] = client;

			string adminDialogKey = GetDialogKey("Админ", requestedNick);
			dialogs.GetOrAdd(adminDialogKey, new ObservableCollection<ChatMessage>());

			Application.Current.Dispatcher.Invoke(() => ConnectedClients.Add(requestedNick));
			Log($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} Клиент {requestedNick} ({endpoint}) подключился.");

			byte[] success = Encoding.UTF8.GetBytes("/nick ok");
			await stream.WriteAsync(success, 0, success.Length);

			await SendUserListToClient(client, requestedNick);
			await BroadcastUserList(client);
			ClientConnected?.Invoke(requestedNick);

			try
			{
				while (true)
				{
					bytes = await stream.ReadAsync(buffer, 0, buffer.Length);
					if (bytes == 0) break;

					string msg = Encoding.UTF8.GetString(buffer, 0, bytes);
					string time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

					if (msg.StartsWith("/msg "))
					{
						string[] parts = msg.Substring(5).Split(new[] { ' ' }, 2);
						if (parts.Length == 2)
						{
							string recipient = parts[0];
							string privateMsg = parts[1];
							await SendPrivateMessage(requestedNick, recipient, privateMsg, time);
						}
						else
						{
							await SendPrivateMessage(requestedNick, requestedNick, "Неверный формат. Используйте /msg ник сообщение", time);
						}
					}
					else if (msg.StartsWith("/all "))
					{
						string broadcastMsg = msg.Substring(5);
						string formatted = $"[{time}] {requestedNick}: {broadcastMsg}";
						Log(formatted);
						OnMessageReceived?.Invoke(formatted);
						await BroadcastAsync(formatted, requestedNick);
					}
					else
					{
						string errorMsg = $"Используйте /msg ник сообщение или /all сообщение";
						byte[] errorData = Encoding.UTF8.GetBytes(errorMsg);
						await stream.WriteAsync(errorData, 0, errorData.Length);
					}
				}
			}
			catch (Exception ex)
			{
				Log($"Ошибка с {requestedNick}: {ex.Message}");
			}
			finally
			{
				clientNick.TryRemove(client, out _);
				nickToClient.TryRemove(requestedNick, out _);
				Application.Current.Dispatcher.Invoke(() => ConnectedClients.Remove(requestedNick));
				ClientDisconnected?.Invoke(requestedNick);
				Log($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} Клиент {requestedNick} отключился.");
				await BroadcastUserList(null);
				client.Close();
			}
		}

		private async Task SendUserListToClient(TcpClient client, string selfNick)
		{
			string list = string.Join(",", nickToClient.Keys.Where(n => n != selfNick));
			string userListMsg = $"/users {list}";
			byte[] data = Encoding.UTF8.GetBytes(userListMsg);
			await client.GetStream().WriteAsync(data, 0, data.Length);
		}

		private async Task BroadcastUserList(TcpClient excludeClient)
		{
			string list = string.Join(",", nickToClient.Keys);
			string userListMsg = $"/users {list}";
			byte[] data = Encoding.UTF8.GetBytes(userListMsg);
			foreach (TcpClient client in nickToClient.Values)
			{
				if (excludeClient != null && client == excludeClient) continue;
				try
				{
					if (client.Connected)
						await client.GetStream().WriteAsync(data, 0, data.Length);
				}
				catch { }
			}
		}

		private async Task SendErrorMessage(TcpClient client, string error)
		{
			byte[] data = Encoding.UTF8.GetBytes(error);
			await client.GetStream().WriteAsync(data, 0, data.Length);
		}

		private string GetDialogKey(string nick1, string nick2)
		{
			string[] sorted = new[] { nick1, nick2 }.OrderBy(x => x).ToArray();
			return $"{sorted[0]}|{sorted[1]}";
		}

		private async Task SendPrivateMessage(string senderNick, string recipientNick, string message, string time)
		{
			if (!nickToClient.ContainsKey(recipientNick))
			{
				string errorMsg = $"[{time}] Пользователь {recipientNick} не найден.";
				if (nickToClient.TryGetValue(senderNick, out TcpClient senderClient))
				{
					byte[] data = Encoding.UTF8.GetBytes(errorMsg);
					await senderClient.GetStream().WriteAsync(data, 0, data.Length);
				}
				return;
			}

			string dialogKey = GetDialogKey(senderNick, recipientNick);
			ObservableCollection<ChatMessage> dialogMessages = dialogs.GetOrAdd(dialogKey, new ObservableCollection<ChatMessage>());

			ChatMessage chatMsg = new ChatMessage
			{
				Author = senderNick,
				Text = message,
				Timestamp = DateTime.Parse(time),
				IsOwn = false
			};
			Application.Current.Dispatcher.Invoke(() => dialogMessages.Add(chatMsg));
			DialogMessageReceived?.Invoke(dialogKey, chatMsg);

			string formatted = $"[{time}] {senderNick}: {message}";
			byte[] dataToRecipient = Encoding.UTF8.GetBytes(formatted);
			TcpClient recipientClient = nickToClient[recipientNick];
			await recipientClient.GetStream().WriteAsync(dataToRecipient, 0, dataToRecipient.Length);
		}

		private async Task BroadcastAsync(string formattedMessage, string senderNick)
		{
			ChatMessage broadcastMsg = new ChatMessage
			{
				Author = senderNick,
				Text = formattedMessage,
				Timestamp = DateTime.Now,
				IsOwn = false
			};
			Application.Current.Dispatcher.Invoke(() => broadcastMessages.Add(broadcastMsg));
			BroadcastMessageReceived?.Invoke(broadcastMsg);

			byte[] data = Encoding.UTF8.GetBytes(formattedMessage);
			foreach (TcpClient client in nickToClient.Values)
			{
				if (clientNick[client] == senderNick) continue;
				try
				{
					if (client.Connected)
						await client.GetStream().WriteAsync(data, 0, data.Length);
				}
				catch { }
			}
		}

		public async Task SendAdminMessageToDialog(string nick1, string nick2, string message)
		{
			string dialogKey = GetDialogKey(nick1, nick2);
			ObservableCollection<ChatMessage> dialogMessages = dialogs.GetOrAdd(dialogKey, new ObservableCollection<ChatMessage>());
			ChatMessage adminMsg = new ChatMessage
			{
				Author = "Админ",
				Text = message,
				Timestamp = DateTime.Now,
				IsOwn = true
			};
			Application.Current.Dispatcher.Invoke(() => dialogMessages.Add(adminMsg));
			DialogMessageReceived?.Invoke(dialogKey, adminMsg);

			string formatted = $"[{DateTime.Now:HH:mm:ss}] Админ: {message}";
			byte[] data = Encoding.UTF8.GetBytes(formatted);
			if (nickToClient.TryGetValue(nick1, out TcpClient client1))
				await client1.GetStream().WriteAsync(data, 0, data.Length);
			if (nickToClient.TryGetValue(nick2, out TcpClient client2))
				await client2.GetStream().WriteAsync(data, 0, data.Length);
		}

		public async Task SendAdminMessageToAll(string message)
		{
			ChatMessage adminMsg = new ChatMessage
			{
				Author = "Админ",
				Text = message,
				Timestamp = DateTime.Now,
				IsOwn = true
			};
			Application.Current.Dispatcher.Invoke(() => broadcastMessages.Add(adminMsg));
			BroadcastMessageReceived?.Invoke(adminMsg);

			byte[] data = Encoding.UTF8.GetBytes(message);
			foreach (TcpClient client in nickToClient.Values)
			{
				try
				{
					if (client.Connected)
						await client.GetStream().WriteAsync(data, 0, data.Length);
				}
				catch { }
			}
		}

		public ObservableCollection<ChatMessage> GetDialogMessages(string nick1, string nick2)
		{
			string key = GetDialogKey(nick1, nick2);
			return dialogs.TryGetValue(key, out ObservableCollection<ChatMessage> messages) ? messages : null;
		}

		public ObservableCollection<ChatMessage> GetBroadcastMessages() => broadcastMessages;

		public System.Collections.Generic.IEnumerable<string> GetAllDialogs() => dialogs.Keys;

		private void Log(string text)
		{
			OnLog?.Invoke(text);
			lock (fileLock) { File.AppendAllText(logFilePath, text + Environment.NewLine); }
		}

		public void Dispose()
		{
			Stop();
		}
	}
}