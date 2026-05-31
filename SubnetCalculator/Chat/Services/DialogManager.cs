using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Linq;
using SubnetCalculator.Chat.Models;

namespace SubnetCalculator.Chat.Services
{
	public class DialogManager
	{
		private readonly ConcurrentDictionary<string, ObservableCollection<ChatMessage>> _dialogs = new ConcurrentDictionary<string, ObservableCollection<ChatMessage>>();
		private ObservableCollection<ChatMessage> _broadcastMessages = new ObservableCollection<ChatMessage>();

		public ObservableCollection<ChatMessage> BroadcastMessages => _broadcastMessages;

		public string GetDialogKey(string nick1, string nick2)
		{
			var sorted = new[] { nick1, nick2 }.OrderBy(x => x).ToArray();
			return $"{sorted[0]}|{sorted[1]}";
		}

		public ObservableCollection<ChatMessage> GetOrCreateDialog(string key)
		{
			return _dialogs.GetOrAdd(key, new ObservableCollection<ChatMessage>());
		}

		public void AddMessageToDialog(string key, ChatMessage message)
		{
			var dialog = GetOrCreateDialog(key);
			App.Current.Dispatcher.Invoke(() => dialog.Add(message));
		}

		public void AddBroadcastMessage(ChatMessage message)
		{
			App.Current.Dispatcher.Invoke(() => _broadcastMessages.Add(message));
		}

		public ObservableCollection<ChatMessage> GetDialogMessages(string key)
		{
			return _dialogs.TryGetValue(key, out var messages) ? messages : null;
		}

		public ObservableCollection<ChatMessage> GetDialogMessages(string nick1, string nick2)
		{
			return GetDialogMessages(GetDialogKey(nick1, nick2));
		}

		public ObservableCollection<ChatMessage> GetBroadcastMessages() => _broadcastMessages;

		public System.Collections.Generic.IEnumerable<string> GetAllDialogKeys() => _dialogs.Keys;
	}
}
