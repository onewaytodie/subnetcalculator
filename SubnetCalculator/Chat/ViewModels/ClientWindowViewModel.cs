using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Threading.Tasks;
using System.Windows.Input;
using SubnetCalculator.Chat.Models;
using SubnetCalculator.Chat.Services;

namespace SubnetCalculator.Chat.ViewModels
{
	public class ClientWindowViewModel : INotifyPropertyChanged
	{
		private readonly ClientManager _clientManager;
		private string _selectedUser;
		private string _chatTitle = "Общий чат";
		private ObservableCollection<ChatMessage> _messages = new ObservableCollection<ChatMessage>();
		private ObservableCollection<string> _users = new ObservableCollection<string>();
		private string _messageText;
		private bool _isConnected;
		private bool _isConnecting;

		public string ServerIP { get; set; } = "127.0.0.1";
		public string Port { get; set; } = "27015";
		public string Nick { get; set; } = "User";

		public ObservableCollection<ChatMessage> Messages => _messages;
		public ObservableCollection<string> Users => _users;

		public string MessageText
		{
			get => _messageText;
			set { _messageText = value; OnPropertyChanged(nameof(MessageText)); }
		}

		public bool IsConnected
		{
			get => _isConnected;
			set
			{
				if (_isConnected != value)
				{
					_isConnected = value;
					OnPropertyChanged(nameof(IsConnected));
					OnPropertyChanged(nameof(IsDisconnected));
					OnPropertyChanged(nameof(ConnectionButtonText));
					(DisconnectCommand as RelayCommand)?.RaiseCanExecuteChanged();
					(ConnectCommand as RelayCommand)?.RaiseCanExecuteChanged();
				}
			}
		}

		public bool IsDisconnected => !_isConnected;
		public bool IsConnecting { get => _isConnecting; set { _isConnecting = value; OnPropertyChanged(nameof(IsConnecting)); OnPropertyChanged(nameof(ConnectionButtonText)); } }
		public string ConnectionButtonText => IsConnected ? "Отключиться" : (IsConnecting ? "Подключение..." : "Подключиться");

		public string SelectedUser
		{
			get => _selectedUser;
			set
			{
				_selectedUser = value;
				OnPropertyChanged(nameof(SelectedUser));
				ChatTitle = value == "Общий чат" ? "Общий чат" : $"Чат с {value}";
				(SendCommand as RelayCommand)?.RaiseCanExecuteChanged();
			}
		}

		public string ChatTitle
		{
			get => _chatTitle;
			set { _chatTitle = value; OnPropertyChanged(nameof(ChatTitle)); }
		}

		public ICommand ConnectCommand { get; }
		public ICommand SendCommand { get; }
		public ICommand DisconnectCommand { get; }

		public event PropertyChangedEventHandler PropertyChanged;
		protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

		public ClientWindowViewModel()
		{
			_clientManager = new ClientManager();
			_clientManager.MessageReceived += OnMessageReceived;
			_clientManager.StatusChanged += OnStatusChanged;

			ConnectCommand = new RelayCommand(async () => await ConnectAsync(), () => !IsConnected && !IsConnecting);
			SendCommand = new RelayCommand(async () => await SendAsync(), () => IsConnected && !string.IsNullOrWhiteSpace(MessageText) && SelectedUser != null);
			DisconnectCommand = new RelayCommand(() => Disconnect(), () => IsConnected);
		}

		private async Task ConnectAsync()
		{
			if (IsConnected) return;
			IsConnecting = true;
			bool success = await _clientManager.ConnectAsync(ServerIP, int.Parse(Port), Nick);
			IsConnecting = false;
			if (!success)
			{
				// ошибка уже обработана в StatusChanged
			}
		}

		private async Task SendAsync()
		{
			if (!IsConnected) return;
			string msg = MessageText.Trim();
			if (string.IsNullOrEmpty(msg)) return;

			string command;
			if (SelectedUser == "Общий чат")
				command = $"/all {msg}";
			else
				command = $"/msg {SelectedUser} {msg}";

			await _clientManager.SendMessageAsync(command);
			// Добавляем своё сообщение локально
			_messages.Add(new ChatMessage { Author = "Я", Text = msg, Timestamp = DateTime.Now, IsOwn = true });
			MessageText = "";
		}

		private void Disconnect()
		{
			_clientManager.Disconnect();
		}

		private void OnMessageReceived(string msg)
		{
			Application.Current.Dispatcher.Invoke(() =>
			{
				if (msg.StartsWith("/users "))
				{
					string usersData = msg.Substring(7);
					var users = usersData.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
					_users.Clear();
					_users.Add("Общий чат");
					foreach (var u in users)
						if (u != Nick) _users.Add(u);
				}
				else
				{
					_messages.Add(new ChatMessage { Author = "Сервер", Text = msg, Timestamp = DateTime.Now, IsOwn = false });
				}
			});
		}

		private void OnStatusChanged(string status)
		{
			Application.Current.Dispatcher.Invoke(() =>
			{
				if (status == "connected")
				{
					IsConnected = true;
					_messages.Add(new ChatMessage { Author = "Система", Text = "Подключено к серверу.", Timestamp = DateTime.Now, IsOwn = false });
				}
				else if (status == "disconnected")
				{
					IsConnected = false;
					_messages.Add(new ChatMessage { Author = "Система", Text = "Отключено от сервера.", Timestamp = DateTime.Now, IsOwn = false });
				}
				else if (status.StartsWith("error"))
				{
					MessageBox.Show(status, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
				}
			});
		}
	}

	public class RelayCommand : ICommand
	{
		private readonly Func<Task> _execute;
		private readonly Func<bool> _canExecute;

		public RelayCommand(Func<Task> execute, Func<bool> canExecute = null)
		{
			_execute = execute;
			_canExecute = canExecute;
		}

		public RelayCommand(Action execute, Func<bool> canExecute = null)
		{
			_execute = async () => { execute(); await Task.CompletedTask; };
			_canExecute = canExecute;
		}

		public bool CanExecute(object parameter) => _canExecute == null || _canExecute();
		public async void Execute(object parameter) => await _execute();
		public event EventHandler CanExecuteChanged { add { } remove { } }

		public void RaiseCanExecuteChanged() => CommandManager.InvalidateRequerySuggested();
	}
}
