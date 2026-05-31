using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Sockets;
using System.Windows;

namespace SubnetCalculator.Chat.Services
{
	public class UserManager
	{
		private readonly ConcurrentDictionary<TcpClient, string> _clientNick = new ConcurrentDictionary<TcpClient, string>();
		private readonly ConcurrentDictionary<string, TcpClient> _nickToClient = new ConcurrentDictionary<string, TcpClient>();
		public ObservableCollection<string> ConnectedClients { get; } = new ObservableCollection<string>();

		public event Action<string> ClientConnected;
		public event Action<string> ClientDisconnected;

		public bool TryRegisterClient(TcpClient client, string requestedNick, out string error, int maxClients = 10)
		{
			error = null;
			if (_nickToClient.Count >= maxClients)
			{
				error = $"Достигнуто максимальное количество клиентов ({maxClients}).";
				return false;
			}
			if (_nickToClient.ContainsKey(requestedNick))
			{
				error = $"Ник '{requestedNick}' уже занят.";
				return false;
			}
			_clientNick[client] = requestedNick;
			_nickToClient[requestedNick] = client;
			Application.Current.Dispatcher.Invoke(() => ConnectedClients.Add(requestedNick));
			ClientConnected?.Invoke(requestedNick);
			return true;
		}

		public void UnregisterClient(TcpClient client)
		{
			if (_clientNick.TryRemove(client, out var nick))
			{
				_nickToClient.TryRemove(nick, out _);
				Application.Current.Dispatcher.Invoke(() => ConnectedClients.Remove(nick));
				ClientDisconnected?.Invoke(nick);
			}
		}

		public string GetNick(TcpClient client) => _clientNick.TryGetValue(client, out var nick) ? nick : null;
		public TcpClient GetClient(string nick) => _nickToClient.TryGetValue(nick, out var client) ? client : null;
		public bool IsNickExists(string nick) => _nickToClient.ContainsKey(nick);
		public int Count => _nickToClient.Count;
		public System.Collections.Generic.IEnumerable<string> GetAllNicks() => _nickToClient.Keys;
		public System.Collections.Generic.IEnumerable<TcpClient> GetAllClients() => _nickToClient.Values;
	}
}
