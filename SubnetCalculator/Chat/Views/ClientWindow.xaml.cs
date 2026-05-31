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
		private TcpClient client;
		private NetworkStream stream;
		private bool isConnected;
		private string selectedUser;
		private string myNick;
		private ObservableCollection<ChatMessage> messages = new ObservableCollection<ChatMessage>();
		private ObservableCollection<string> users = new ObservableCollection<string>();

		public ClientWindow()
		{
			InitializeComponent();
			UsersListBox.ItemsSource = users;
			MessagesItemsControl.ItemsSource = messages;
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
				client = new TcpClient();
				await client.ConnectAsync(txtServerIP.Text, port);
				stream = client.GetStream();

				myNick = txtNick.Text.Trim();
				if (string.IsNullOrEmpty(myNick)) myNick = "User";
				if (myNick.Equals("Админ", StringComparison.OrdinalIgnoreCase))
				{
					MessageBox.Show("Ник 'Админ' зарезервирован для сервера. Выберите другой ник.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
					client.Close();
					return;
				}
				byte[] nickData = Encoding.UTF8.GetBytes($"/nick {myNick}");
				await stream.WriteAsync(nickData, 0, nickData.Length);

				byte[] buffer = new byte[4096];
				int bytes = await stream.ReadAsync(buffer, 0, buffer.Length);
				string response = Encoding.UTF8.GetString(buffer, 0, bytes);
				if (response != "/nick ok")
				{
					MessageBox.Show($"Ошибка регистрации: {response}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
					client.Close();
					return;
				}

				isConnected = true;
				AddSystemMessage("Подключено к серверу.");
				btnConnect.IsEnabled = false;
				btnDisconnect.IsEnabled = true;
				btnSend.IsEnabled = true;
				txtMessage.IsEnabled = true;
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
				while (isConnected)
				{
					int bytes = await stream.ReadAsync(buffer, 0, buffer.Length);
					if (bytes == 0) break;
					string msg = Encoding.UTF8.GetString(buffer, 0, bytes);

					if (msg.StartsWith("/users "))
					{
						string usersData = msg.Substring(7);
						string[] usersArray = usersData.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
						Dispatcher.Invoke(() =>
						{
							users.Clear();
							users.Add("Общий чат");
							foreach (string u in usersArray)
								if (u != myNick) users.Add(u);
						});
					}
					else
					{
						Dispatcher.Invoke(() => AddMessage(new ChatMessage
						{
							Author = "Сервер",
							Text = msg,
							Timestamp = DateTime.Now,
							IsOwn = false
						}));
					}
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



		private async void SendFileButton_Click(object sender, RoutedEventArgs e)
		{
			AddSystemMessage("Отправка файлов временно отключена.");
			return;
		}

		private void SendButton_Click(object sender, RoutedEventArgs e)
		{
			if (!isConnected) return;
			string msg = txtMessage.Text.Trim();
			if (string.IsNullOrEmpty(msg)) return;

			if (selectedUser == null || selectedUser == "Общий чат")
			{
				string command = $"/all {msg}";
				byte[] data = Encoding.UTF8.GetBytes(command);
				stream.Write(data, 0, data.Length);
				AddMessage(new ChatMessage { Author = "Я", Text = msg, Timestamp = DateTime.Now, IsOwn = true });
			}
			else
			{
				string command = $"/msg {selectedUser} {msg}";
				byte[] data = Encoding.UTF8.GetBytes(command);
				stream.Write(data, 0, data.Length);
				AddMessage(new ChatMessage { Author = "Я", Text = msg, Timestamp = DateTime.Now, IsOwn = true });
			}
			txtMessage.Clear();
		}

		private void UsersListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
		{
			if (UsersListBox.SelectedItem is string user)
			{
				selectedUser = user;
				ChatTitle.Text = user == "Общий чат" ? "Общий чат" : $"Чат с {user}";
			}
		}

		private void AddMessage(ChatMessage msg)
		{
			Dispatcher.Invoke(() =>
			{
				messages.Add(msg);
				MessagesScrollViewer.ScrollToEnd();
			});
		}

		private void AddSystemMessage(string text)
		{
			AddMessage(new ChatMessage { Author = "Система", Text = text, Timestamp = DateTime.Now, IsOwn = false });
		}

		private void Disconnect()
		{
			if (isConnected)
			{
				isConnected = false;
				stream?.Close();
				client?.Close();
				AddSystemMessage("Отключено от сервера.");
			}
			btnConnect.IsEnabled = true;
			btnDisconnect.IsEnabled = false;
			btnSend.IsEnabled = false;
			btnSendFile.IsEnabled = false;
			txtMessage.IsEnabled = false;
		}

		private void BtnDisconnect_Click(object sender, RoutedEventArgs e) => Disconnect();

		protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
		{
			Disconnect();
			base.OnClosing(e);
		}
	}
}