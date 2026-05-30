using System;
using System.Windows;
using SubnetCalculator.Chat.Services;

namespace SubnetCalculator.Chat.Views
{
	public partial class ClientWindow : Window
	{
		private ChatClient _client;

		public ClientWindow()
		{
			InitializeComponent();
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
				_client = new ChatClient();
				_client.OnMessageReceived += AppendMessage;
				_client.OnStatusChanged += OnStatusChanged;
				await _client.ConnectAsync(txtServerIP.Text, port);
			}
			catch (Exception ex)
			{
				MessageBox.Show($"Ошибка подключения: {ex.Message}", "Ошибка");
			}
		}

		private void AppendMessage(string msg)
		{
			Dispatcher.Invoke(() => lstChat.Items.Add(msg));
		}

		private void OnStatusChanged(string status)
		{
			Dispatcher.Invoke(() =>
			{
				if (status == "connected")
				{
					btnConnect.IsEnabled = false;
					btnDisconnect.IsEnabled = true;
					btnSend.IsEnabled = true;
					AppendMessage("[Система] Подключено к серверу.");
				}
				else if (status == "disconnected")
				{
					btnConnect.IsEnabled = true;
					btnDisconnect.IsEnabled = false;
					btnSend.IsEnabled = false;
					AppendMessage("[Система] Отключено от сервера.");
				}
				else if (status.StartsWith("error"))
				{
					MessageBox.Show(status, "Ошибка");
				}
			});
		}

		private async void BtnSend_Click(object sender, RoutedEventArgs e)
		{
			string msg = txtMessage.Text.Trim();
			if (string.IsNullOrEmpty(msg) || _client == null) return;
			await _client.SendMessageAsync(msg);
			txtMessage.Clear();
		}

		private void BtnDisconnect_Click(object sender, RoutedEventArgs e)
		{
			_client?.Disconnect();
		}

		protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
		{
			_client?.Disconnect();
			base.OnClosing(e);
		}
	}
}
