using System;
using System.Net.Sockets;
using System.Windows;
using SubnetCalculator.Chat.Services;

namespace SubnetCalculator.Chat.Views
{
	public partial class ServerWindow : Window
	{
		private ChatServer _server;

		public ServerWindow()
		{
			InitializeComponent();
		}

		private async void BtnStart_Click(object sender, RoutedEventArgs e)
		{
			if (!int.TryParse(txtPort.Text, out int port))
			{
				AppendLog("Неверный номер порта.");
				return;
			}

			if (_server != null)
			{
				AppendLog("Сервер уже был создан. Перезапустите окно.");
				return;
			}

			try
			{
				_server = new ChatServer();
				_server.OnLog += AppendLog;
				//_server.OnMessageReceived += (msg) => AppendLog(msg);
				await _server.StartAsync(port);
				btnStart.IsEnabled = false;
				btnStop.IsEnabled = true;
				btnSend.IsEnabled = true;
			}
			catch (SocketException sockEx) when (sockEx.ErrorCode == 10048) // 10048 = WSAEADDRINUSE
			{
				AppendLog($"Порт {port} уже занят. Закройте другой сервер или выберите другой порт.");
			}
			catch (Exception ex)
			{
				AppendLog($"Неожиданная ошибка: {ex.Message}");
			}
		}

		private void AppendLog(string text)
		{
			Dispatcher.Invoke(() =>
			{
				lstLog.Items.Add(text);
				if (lstLog.Items.Count > 0)
					lstLog.ScrollIntoView(lstLog.Items[lstLog.Items.Count - 1]);
			});
		}

		private async void BtnSend_Click(object sender, RoutedEventArgs e)
		{
			string msg = txtAdminMessage.Text.Trim();
			if (string.IsNullOrEmpty(msg)) return;
			string formatted = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Админ: {msg}";
			AppendLog(formatted);
			if (_server != null)
				await _server.SendAdminMessageAsync(formatted);
			txtAdminMessage.Clear();
		}

		private void BtnStop_Click(object sender, RoutedEventArgs e)
		{
			_server?.Stop();
			btnStart.IsEnabled = true;
			btnStop.IsEnabled = false;
			btnSend.IsEnabled = false;
		}

		protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
		{
			_server?.Stop();
			base.OnClosing(e);
		}
	}
}