using System;
using System.Collections.ObjectModel;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using SubnetCalculator.Chat.Models;

namespace SubnetCalculator.Chat.Views
{
	public partial class ClientWindow : Window
	{
		private TcpClient _client;
		private NetworkStream _stream;
		private bool _isConnected;
		private string _selectedUser;
		private ObservableCollection<ChatMessage> _messages = new ObservableCollection<ChatMessage>();
		private ObservableCollection<string> _users = new ObservableCollection<string>();

		public ClientWindow()
		{
			InitializeComponent();
			UsersListBox.ItemsSource = _users;
			MessagesItemsControl.ItemsSource = _messages;
		}

		private async void BtnConnect_Click(object sender, RoutedEventArgs e)
		{
			if (!int.TryParse(txtPort.Text, out int port))
			{
				MessageBox.Show("Неверный порт", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}
			try
			{
				_client = new TcpClient();
				await _client.ConnectAsync(txtServerIP.Text, port);
				_stream = _client.GetStream();
				_isConnected = true;
				AddSystemMessage("Подключено к серверу.");
				btnConnect.IsEnabled = false;
				btnDisconnect.IsEnabled = true;
				btnSend.IsEnabled = true;
				_ = ReceiveMessagesAsync();
			}
			catch (Exception ex)
			{
				MessageBox.Show($"Ошибка подключения: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}

		private async Task ReceiveMessagesAsync()
		{
			byte[] buffer = new byte[4096];
			try
			{
				while (_isConnected)
				{
					int bytes = await _stream.ReadAsync(buffer, 0, buffer.Length);
					if (bytes == 0) break;
					string msg = Encoding.UTF8.GetString(buffer, 0, bytes);

					if (msg.StartsWith("/users "))
					{
						string usersData = msg.Substring(7);
						var users = usersData.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
						Dispatcher.Invoke(() =>
						{
							_users.Clear();
							_users.Add("Общий чат");
							foreach (var u in users)
								if (!string.IsNullOrEmpty(u))
									_users.Add(u);
						});
						continue;
					}

					Dispatcher.Invoke(() => AddMessage(new ChatMessage
					{
						Author = "Сервер",
						Text = msg,
						Timestamp = DateTime.Now,
						IsOwn = false
					}));
				}
			}
			catch (Exception ex)
			{
				Dispatcher.Invoke(() => AddSystemMessage($"Ошибка приёма: {ex.Message}"));
			}
			finally
			{
				Dispatcher.Invoke(() => Disconnect());
			}
		}

		private void SendButton_Click(object sender, RoutedEventArgs e)
		{
			if (!_isConnected) return;
			string msg = txtMessage.Text.Trim();
			if (string.IsNullOrEmpty(msg)) return;

			if (_selectedUser == null || _selectedUser == "Общий чат")
			{
				byte[] data = Encoding.UTF8.GetBytes(msg);
				_stream.Write(data, 0, data.Length);
				AddMessage(new ChatMessage
				{
					Author = "Я",
					Text = msg,
					Timestamp = DateTime.Now,
					IsOwn = true
				});
			}
			else
			{
				string command = $"/msg {_selectedUser} {msg}";
				byte[] data = Encoding.UTF8.GetBytes(command);
				_stream.Write(data, 0, data.Length);
				AddMessage(new ChatMessage
				{
					Author = "Я -> " + _selectedUser,
					Text = msg,
					Timestamp = DateTime.Now,
					IsOwn = true
				});
			}
			txtMessage.Clear();
		}

		private void UsersListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
		{
			if (UsersListBox.SelectedItem is string user)
			{
				_selectedUser = user;
				ChatTitle.Text = user == "Общий чат" ? "Общий чат" : $"Чат с {user}";
			}
		}

		private void AddMessage(ChatMessage msg)
		{
			Dispatcher.Invoke(() =>
			{
				_messages.Add(msg);
				MessagesScrollViewer.ScrollToEnd();
			});
		}

		private void AddSystemMessage(string text)
		{
			AddMessage(new ChatMessage
			{
				Author = "Система",
				Text = text,
				Timestamp = DateTime.Now,
				IsOwn = false
			});
		}

		private void Disconnect()
		{
			_isConnected = false;
			_stream?.Close();
			_client?.Close();
			AddSystemMessage("Отключено от сервера.");
			btnConnect.IsEnabled = true;
			btnDisconnect.IsEnabled = false;
			btnSend.IsEnabled = false;
		}

		private void BtnDisconnect_Click(object sender, RoutedEventArgs e) => Disconnect();
	}
}