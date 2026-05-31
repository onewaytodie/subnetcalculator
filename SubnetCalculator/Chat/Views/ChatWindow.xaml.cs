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
		private ChatServer _server;
		private string _selectedDialogKey;
		private ObservableCollection<ChatMessage> _currentMessages = new ObservableCollection<ChatMessage>();
		private ObservableCollection<string> _dialogsList = new ObservableCollection<string>();

		public event PropertyChangedEventHandler PropertyChanged;

		protected virtual void OnPropertyChanged(string propertyName)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		public ChatWindow()
		{
			InitializeComponent();
			DataContext = this;
			DialogsListView.ItemsSource = _dialogsList;
			MessagesItemsControl.ItemsSource = _currentMessages;
			Loaded += ChatWindow_Loaded;
		}

		private async void ChatWindow_Loaded(object sender, RoutedEventArgs e)
		{
			_server = new ChatServer();
			_server.OnLog += AppendLog;
			_server.ClientConnected += OnClientConnected;
			_server.ClientDisconnected += OnClientDisconnected;
			_server.DialogMessageReceived += OnDialogMessageReceived;
			_server.BroadcastMessageReceived += OnBroadcastMessageReceived;

			await _server.StartAsync(27015);
			AppendLog("Сервер запущен.");
		}

		private void OnClientConnected(string nick)
		{
			Dispatcher.Invoke(() =>
			{
				_server.GetBroadcastMessages().Add(new ChatMessage
				{
					Author = "Система",
					Text = $"Клиент {nick} подключился.",
					Timestamp = DateTime.Now,
					IsOwn = false
				});
				if (_selectedDialogKey == "Общий чат")
					RefreshMessages();
				UpdateDialogsList();
			});
		}

		private void OnClientDisconnected(string nick)
		{
			Dispatcher.Invoke(() =>
			{
				_server.GetBroadcastMessages().Add(new ChatMessage
				{
					Author = "Система",
					Text = $"Клиент {nick} отключился.",
					Timestamp = DateTime.Now,
					IsOwn = false
				});
				if (_selectedDialogKey == "Общий чат")
					RefreshMessages();
				UpdateDialogsList();
			});
		}

		private void OnDialogMessageReceived(string dialogKey, ChatMessage message)
		{
			Dispatcher.Invoke(() =>
			{
				if (_selectedDialogKey == dialogKey)
				{
					_currentMessages.Add(message);
					ScrollToBottom();
				}
				UpdateDialogsList();
			});
		}

		private void OnBroadcastMessageReceived(ChatMessage message)
		{
			Dispatcher.Invoke(() =>
			{
				if (_selectedDialogKey == "Общий чат")
				{
					_currentMessages.Add(message);
					ScrollToBottom();
				}
			});
		}

		private void UpdateDialogsList()
		{
			Dispatcher.Invoke(() =>
			{
				var dialogs = _server.GetAllDialogs().ToList();
				_dialogsList.Clear();
				_dialogsList.Add("Общий чат");
				foreach (var d in dialogs.OrderBy(x => x))
					_dialogsList.Add(d);
			});
		}

		private void DialogsListView_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
		{
			if (DialogsListView.SelectedItem is string selected)
			{
				_selectedDialogKey = selected;
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
			_currentMessages.Clear();
			if (_selectedDialogKey == "Общий чат")
			{
				foreach (var msg in _server.GetBroadcastMessages())
					_currentMessages.Add(msg);
			}
			else
			{
				var parts = _selectedDialogKey.Split('|');
				if (parts.Length == 2)
				{
					var messages = _server.GetDialogMessages(parts[0], parts[1]);
					if (messages != null)
					{
						foreach (var msg in messages)
							_currentMessages.Add(msg);
					}
				}
			}
			ScrollToBottom();
		}

		private async void SendButton_Click(object sender, RoutedEventArgs e)
		{
			if (string.IsNullOrWhiteSpace(MessageTextBox.Text) || _selectedDialogKey == null)
				return;
			string text = MessageTextBox.Text.Trim();

			if (_selectedDialogKey == "Общий чат")
			{
				await _server.SendAdminMessageToAll($"[Админ] {text}");
				// Не добавляем локально – сообщение придёт через BroadcastMessageReceived
			}
			else
			{
				var parts = _selectedDialogKey.Split('|');
				if (parts.Length == 2)
				{
					await _server.SendAdminMessageToDialog(parts[0], parts[1], text);
					// Не добавляем локально – сообщение придёт через DialogMessageReceived
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

		private void AppendLog(string text)
		{
			System.Diagnostics.Debug.WriteLine(text);
		}

		protected override void OnClosing(CancelEventArgs e)
		{
			_server?.Stop();
			base.OnClosing(e);
		}
	}
}