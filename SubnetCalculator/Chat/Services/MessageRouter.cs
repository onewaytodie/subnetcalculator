using System;
using System.Text;
using System.Threading.Tasks;
using SubnetCalculator.Chat.Models;

namespace SubnetCalculator.Chat.Services
{
	public class MessageRouter
	{
		private readonly UserManager _userManager;
		private readonly DialogManager _dialogManager;
		private readonly Action<string> _log;

		public event Action<string, ChatMessage> DialogMessageReceived;
		public event Action<ChatMessage> BroadcastMessageReceived;

		public MessageRouter(UserManager userManager, DialogManager dialogManager, Action<string> log)
		{
			_userManager = userManager;
			_dialogManager = dialogManager;
			_log = log;
		}

		public async Task SendPrivateMessage(string senderNick, string recipientNick, string message, string time)
		{
			if (!_userManager.IsNickExists(recipientNick))
			{
				string errorMsg = $"[{time}] Пользователь {recipientNick} не найден.";
				var senderClient = _userManager.GetClient(senderNick);
				if (senderClient != null)
				{
					byte[] data = Encoding.UTF8.GetBytes(errorMsg);
					await senderClient.GetStream().WriteAsync(data, 0, data.Length);
				}
				return;
			}

			string dialogKey = _dialogManager.GetDialogKey(senderNick, recipientNick);
			var chatMsg = new ChatMessage
			{
				Author = senderNick,
				Text = message,
				Timestamp = DateTime.Parse(time),
				IsOwn = false
			};
			_dialogManager.AddMessageToDialog(dialogKey, chatMsg);
			DialogMessageReceived?.Invoke(dialogKey, chatMsg);

			string formatted = $"[{time}] {senderNick}: {message}";
			byte[] dataToRecipient = Encoding.UTF8.GetBytes(formatted);
			var recipientClient = _userManager.GetClient(recipientNick);
			await recipientClient.GetStream().WriteAsync(dataToRecipient, 0, dataToRecipient.Length);
		}

		public async Task Broadcast(string formattedMessage, string senderNick)
		{
			var broadcastMsg = new ChatMessage
			{
				Author = senderNick,
				Text = formattedMessage,
				Timestamp = DateTime.Now,
				IsOwn = false
			};
			_dialogManager.AddBroadcastMessage(broadcastMsg);
			BroadcastMessageReceived?.Invoke(broadcastMsg);

			byte[] data = Encoding.UTF8.GetBytes(formattedMessage);
			foreach (var client in _userManager.GetAllClients())
			{
				if (_userManager.GetNick(client) == senderNick) continue;
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
			string dialogKey = _dialogManager.GetDialogKey(nick1, nick2);
			var adminMsg = new ChatMessage
			{
				Author = "Админ",
				Text = message,
				Timestamp = DateTime.Now,
				IsOwn = true
			};
			_dialogManager.AddMessageToDialog(dialogKey, adminMsg);
			DialogMessageReceived?.Invoke(dialogKey, adminMsg);

			string formatted = $"[{DateTime.Now:HH:mm:ss}] Админ: {message}";
			byte[] data = Encoding.UTF8.GetBytes(formatted);
			var client1 = _userManager.GetClient(nick1);
			var client2 = _userManager.GetClient(nick2);
			if (client1 != null) await client1.GetStream().WriteAsync(data, 0, data.Length);
			if (client2 != null) await client2.GetStream().WriteAsync(data, 0, data.Length);
		}

		public async Task SendAdminMessageToAll(string message)
		{
			var adminMsg = new ChatMessage
			{
				Author = "Админ",
				Text = message,
				Timestamp = DateTime.Now,
				IsOwn = true
			};
			_dialogManager.AddBroadcastMessage(adminMsg);
			BroadcastMessageReceived?.Invoke(adminMsg);

			byte[] data = Encoding.UTF8.GetBytes(message);
			foreach (var client in _userManager.GetAllClients())
			{
				try
				{
					if (client.Connected)
						await client.GetStream().WriteAsync(data, 0, data.Length);
				}
				catch { }
			}
		}
	}
}
