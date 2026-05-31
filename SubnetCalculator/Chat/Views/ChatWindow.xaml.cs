using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using SubnetCalculator.Chat.Models;
using SubnetCalculator.Chat.Services;

namespace SubnetCalculator.Chat.Views
{
	public partial class ChatWindow : Window, INotifyPropertyChanged
	{
		private ChatServer server;
		private string selectedDialogKey;
		private ObservableCollection<ChatMessage> currentMessages = new ObservableCollection<ChatMessage>();
		private ObservableCollection<string> dialogsList = new ObservableCollection<string>();

		public event PropertyChangedEventHandler PropertyChanged;
		protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

		public ChatWindow()
		{
			InitializeComponent();
			DataContext = this;
			DialogsListView.ItemsSource = dialogsList;
			MessagesItemsControl.ItemsSource = currentMessages;
			Loaded += ChatWindow_Loaded;
		}

		private async void ChatWindow_Loaded(object sender, RoutedEventArgs e)
		{
			server = new ChatServer();
			server.OnLog += AppendLog;
			server.ClientConnected += OnClientConnected;
			server.ClientDisconnected += OnClientDisconnected;
			server.DialogMessageReceived += OnDialogMessageReceived;
			server.BroadcastMessageReceived += OnBroadcastMessageReceived;

			await server.StartAsync(27015);
			AppendLog("Сервер запущен.");
		}

		private void OnClientConnected(string nick)
		{
			Dispatcher.Invoke(() =>
			{
				server.GetBroadcastMessages().Add(new ChatMessage
				{
					Author = "Система",
					Text = $"Клиент {nick} подключился.",
					Timestamp = DateTime.Now,
					IsOwn = false
				});
				if (selectedDialogKey == "Общий чат")
					RefreshMessages();
				UpdateDialogsList();
			});
		}

		private void OnClientDisconnected(string nick)
		{
			Dispatcher.Invoke(() =>
			{
				server.GetBroadcastMessages().Add(new ChatMessage
				{
					Author = "Система",
					Text = $"Клиент {nick} отключился.",
					Timestamp = DateTime.Now,
					IsOwn = false
				});
				if (selectedDialogKey == "Общий чат")
					RefreshMessages();
				UpdateDialogsList();
			});
		}

		private void OnDialogMessageReceived(string dialogKey, ChatMessage message)
		{
			Dispatcher.Invoke(() =>
			{
				if (selectedDialogKey == dialogKey)
					currentMessages.Add(message);
				UpdateDialogsList();
			});
		}

		private void OnBroadcastMessageReceived(ChatMessage message)
		{
			Dispatcher.Invoke(() =>
			{
				if (selectedDialogKey == "Общий чат")
					currentMessages.Add(message);
			});
		}

		private void UpdateDialogsList()
		{
			Dispatcher.Invoke(() =>
			{
				string[] dialogs = server.GetAllDialogs().ToArray();
				dialogsList.Clear();
				dialogsList.Add("Общий чат");
				foreach (string dialog in dialogs.OrderBy(x => x))
					dialogsList.Add(dialog);
			});
		}

		private void DialogsListView_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
		{
			if (DialogsListView.SelectedItem is string selected)
			{
				selectedDialogKey = selected;
				RefreshMessages();
				MessageTextBox.IsEnabled = true;
				SendButton.IsEnabled = true;
			}
			else
			{
				MessageTextBox.IsEnabled = false;
				SendButton.IsEnabled = false;
			}
		}

		private void RefreshMessages()
		{
			currentMessages.Clear();
			if (selectedDialogKey == "Общий чат")
			{
				foreach (ChatMessage msg in server.GetBroadcastMessages())
					currentMessages.Add(msg);
			}
			else
			{
				string[] parts = selectedDialogKey.Split('|');
				if (parts.Length == 2)
				{
					ObservableCollection<ChatMessage> messages = server.GetDialogMessages(parts[0], parts[1]);
					if (messages != null)
						foreach (ChatMessage msg in messages)
							currentMessages.Add(msg);
				}
			}
			ScrollToBottom();
		}

		private async void SendButton_Click(object sender, RoutedEventArgs e)
		{
			if (string.IsNullOrWhiteSpace(MessageTextBox.Text) || selectedDialogKey == null)
				return;
			string text = MessageTextBox.Text.Trim();

			if (selectedDialogKey == "Общий чат")
			{
				await server.SendAdminMessageToAll($"[Админ] {text}");
			}
			else
			{
				string[] parts = selectedDialogKey.Split('|');
				if (parts.Length == 2)
				{
					await server.SendAdminMessageToDialog(parts[0], parts[1], text);
				}
			}
			MessageTextBox.Clear();
		}

		private void ScrollToBottom()
		{
			Dispatcher.BeginInvoke(new Action(() =>
			{
				if (MessagesScrollViewer.ScrollableHeight > 0)
					MessagesScrollViewer.ScrollToEnd();
			}));
		}

		private void AppendLog(string text) => System.Diagnostics.Debug.WriteLine(text);

		protected override void OnClosing(CancelEventArgs e)
		{
			server?.Stop();
			base.OnClosing(e);
		}
	}
}