using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using SubnetCalculator.Chat.Models;
using SubnetCalculator.Chat.Services;

namespace SubnetCalculator.Chat.Views
{
	public partial class ChatWindow : Window, INotifyPropertyChanged
	{
		private ChatServer _server;
		private string _selectedClient;

		public event PropertyChangedEventHandler PropertyChanged;

		public ChatWindow()
		{
			InitializeComponent();
			DataContext = this;
			Loaded += ChatWindow_Loaded;
		}

		private async void ChatWindow_Loaded(object sender, RoutedEventArgs e)
		{
			_server = new ChatServer();
			_server.OnLog += AppendLog;
			_server.ClientConnected += OnClientConnected;
			_server.ClientDisconnected += OnClientDisconnected;
			_server.ChatMessageReceived += OnChatMessageReceived;

			// Привязываем список клиентов к коллекции сервера
			ClientsListView.ItemsSource = _server.ConnectedClients;

			await _server.StartAsync(27015);
			AppendLog("Сервер запущен.");
		}

		private void OnClientConnected(string endpoint)
		{
			// Не нужно добавлять вручную – ItemsSource обновляется автоматически
		}

		private void OnClientDisconnected(string endpoint)
		{
			Dispatcher.Invoke(() =>
			{
				if (_selectedClient == endpoint)
				{
					_selectedClient = null;
					MessagesItemsControl.Items.Clear();
				}
			});
		}

		private void OnChatMessageReceived(string endpoint, ChatMessage message)
		{
			Dispatcher.Invoke(() =>
			{
				if (_selectedClient == endpoint)
					MessagesItemsControl.Items.Add(message);
			});
		}

		private void ClientsListView_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
		{
			if (ClientsListView.SelectedItem is string client)
			{
				_selectedClient = client;
				var messages = _server.GetClientMessages(client);
				MessagesItemsControl.Items.Clear();
				if (messages != null)
				{
					foreach (var msg in messages)
						MessagesItemsControl.Items.Add(msg);
				}
				ScrollToBottom();
				MessageTextBox.IsEnabled = true;
				SendButton.IsEnabled = true;
			}
			else
			{
				MessageTextBox.IsEnabled = false;
				SendButton.IsEnabled = false;
			}
		}

		private async void SendButton_Click(object sender, RoutedEventArgs e)
		{
			if (string.IsNullOrWhiteSpace(MessageTextBox.Text) || _selectedClient == null)
				return;
			string text = MessageTextBox.Text.Trim();
			var adminMsg = new ChatMessage
			{
				Author = "Админ",
				Text = text,
				Timestamp = DateTime.Now,
				IsOwn = true
			};
			MessagesItemsControl.Items.Add(adminMsg);
			ScrollToBottom();
			await _server.SendAdminMessageAsync($"[Админ] {text}");
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