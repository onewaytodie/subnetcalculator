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
		private TcpListener _listener;
		private bool _isRunning;
		private readonly ConcurrentDictionary<TcpClient, string> _clientNick = new ConcurrentDictionary<TcpClient, string>();
		private readonly ConcurrentDictionary<string, TcpClient> _nickToClient = new ConcurrentDictionary<string, TcpClient>();
		private readonly object _fileLock = new object();
		private readonly string _logFilePath;
		private readonly UserManager _userManager = new UserManager();
		private readonly DialogManager _dialogManager = new DialogManager();
		private readonly MessageRouter _messageRouter;

		// События – теперь обычные поля (без явных аксессоров)
		public event Action<string> ClientConnected;
		public event Action<string> ClientDisconnected;
		public event Action<string, ChatMessage> DialogMessageReceived;
		public event Action<ChatMessage> BroadcastMessageReceived;

		public event Action<string> OnLog;
		public event Action<string> OnMessageReceived;

		public ChatServer(string logFilePath = "chat_log.txt")
		{
			_logFilePath = logFilePath;
			_messageRouter = new MessageRouter(_userManager, _dialogManager, Log);

			// Прокси: пробрасываем события из UserManager наружу
			_userManager.ClientConnected += nick => ClientConnected?.Invoke(nick);
			_userManager.ClientDisconnected += nick => ClientDisconnected?.Invoke(nick);
			// Прокси для событий маршрутизатора
			_messageRouter.DialogMessageReceived += (key, msg) => DialogMessageReceived?.Invoke(key, msg);
			_messageRouter.BroadcastMessageReceived += msg => BroadcastMessageReceived?.Invoke(msg);
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
			foreach (var client in _userManager.GetAllClients().ToList())
				client.Close();
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

			if (!_userManager.TryRegisterClient(client, requestedNick, out string error))
			{
				await SendErrorMessage(client, error);
				client.Close();
				return;
			}

			// Создаём диалог администратора с новым клиентом
			string adminDialogKey = _dialogManager.GetDialogKey("Админ", requestedNick);
			_dialogManager.GetOrCreateDialog(adminDialogKey);

			Log($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} Клиент {requestedNick} ({endpoint}) подключился.");

			byte[] success = Encoding.UTF8.GetBytes("/nick ok");
			await stream.WriteAsync(success, 0, success.Length);

			await SendUserListToClient(client, requestedNick);
			await BroadcastUserList(client);
			// Теперь можно вызывать событие (оно обычное поле)
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
						var parts = msg.Substring(5).Split(new[] { ' ' }, 2);
						if (parts.Length == 2)
						{
							string recipient = parts[0];
							string privateMsg = parts[1];
							await _messageRouter.SendPrivateMessage(requestedNick, recipient, privateMsg, time);
						}
						else
						{
							await _messageRouter.SendPrivateMessage(requestedNick, requestedNick, "Неверный формат. Используйте /msg ник сообщение", time);
						}
					}
					else if (msg.StartsWith("/all "))
					{
						string broadcastMsg = msg.Substring(5);
						string formatted = $"[{time}] {requestedNick}: {broadcastMsg}";
						Log(formatted);
						OnMessageReceived?.Invoke(formatted);
						await _messageRouter.Broadcast(formatted, requestedNick);
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
				_userManager.UnregisterClient(client);
				Log($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} Клиент {requestedNick} отключился.");
				await BroadcastUserList(null);
				client.Close();
			}
		}

		private async Task SendUserListToClient(TcpClient client, string selfNick)
		{
			var list = string.Join(",", _userManager.GetAllNicks().Where(n => n != selfNick));
			string userListMsg = $"/users {list}";
			byte[] data = Encoding.UTF8.GetBytes(userListMsg);
			await client.GetStream().WriteAsync(data, 0, data.Length);
		}

		private async Task BroadcastUserList(TcpClient excludeClient)
		{
			var list = string.Join(",", _userManager.GetAllNicks());
			string userListMsg = $"/users {list}";
			byte[] data = Encoding.UTF8.GetBytes(userListMsg);
			foreach (var client in _userManager.GetAllClients())
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

		public async Task SendAdminMessageToDialog(string nick1, string nick2, string message)
		{
			await _messageRouter.SendAdminMessageToDialog(nick1, nick2, message);
		}

		public async Task SendAdminMessageToAll(string message)
		{
			await _messageRouter.SendAdminMessageToAll(message);
		}

		public ObservableCollection<ChatMessage> GetDialogMessages(string nick1, string nick2)
		{
			string key = _dialogManager.GetDialogKey(nick1, nick2);
			return _dialogManager.GetDialogMessages(key);
		}

		public ObservableCollection<ChatMessage> GetBroadcastMessages() => _dialogManager.GetBroadcastMessages();

		public System.Collections.Generic.IEnumerable<string> GetAllDialogs() => _dialogManager.GetAllDialogKeys();

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