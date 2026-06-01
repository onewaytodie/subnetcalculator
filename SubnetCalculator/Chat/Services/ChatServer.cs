using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using SubnetCalculator.Chat.Models;

namespace SubnetCalculator.Chat.Services
{
	public class ChatServer : IDisposable
	{
		private TcpListener listener;																							// Серверный TCP-слушатель (объект, который принимает входящие подключения)
		private bool isRunning;																									// Флаг, указывающий, работает ли сервер (запущен ли цикл приёма клиентов)
		private readonly ConcurrentDictionary<TcpClient, string> clientNick = new ConcurrentDictionary<TcpClient, string>();	// Словарь, отображающий TCP-клиент на его ник (ConcurrentDictionary – потокобезопасный)
		private readonly ConcurrentDictionary<string, TcpClient> nickToClient = new ConcurrentDictionary<string, TcpClient>();  // Обратный словарь: ник -> TCP-клиент (для быстрого поиска клиента по нику)
		private readonly object fileLock = new object();																		// Объект для синхронизации доступа к файлу лога (lock)
		private readonly string logFilePath;																					// Путь к файлу, в который сохраняются все сообщения и события сервера
		public ObservableCollection<string> ConnectedClients { get; private set; } = new ObservableCollection<string>(); // Коллекция для хранения ников подключённых клиентов (используется для привязки к UI сервера)
		// Словарь диалогов: ключ – строка вида "ник1|ник2" (отсортированная пара), значение – коллекция сообщений в этом диалоге
		private readonly ConcurrentDictionary<string, ObservableCollection<ChatMessage>> dialogs = new ConcurrentDictionary<string, ObservableCollection<ChatMessage>>();
		// Коллекция сообщений общего чата (broadcast), видна всем клиентам и администратору
		private ObservableCollection<ChatMessage> broadcastMessages = new ObservableCollection<ChatMessage>();
		public event Action<string> ClientConnected;					// Событие, возникающее при подключении нового клиента (передаётся его ник)
		public event Action<string> ClientDisconnected;					// Событие при отключении клиента (передаётся ник)
		public event Action<string, ChatMessage> DialogMessageReceived; // Событие при получении нового сообщения в каком-либо диалоге (ключ диалога и само сообщение)
		public event Action<ChatMessage> BroadcastMessageReceived;		// Событие при получении нового сообщения в общем чате (broadcast)
		public event Action<string> OnLog;								// Событие для логирования текстовых сообщений (используется для вывода в консоль отладки или в UI лог)
		public event Action<string> OnMessageReceived;					// Событие при получении любого текстового сообщения (не структурированного) – используется для совместимости

		public ChatServer(string logFilePath = "chat_log.txt")	//путь
		{
			this.logFilePath = logFilePath;	//Инициализация нового экземпляра сервера
		}

		public async Task StartAsync(int port)
		{
			if (isRunning) return;									//1. Если сервер уже работает, выходим
			try
			{
				listener = new TcpListener(IPAddress.Any, port);	//2. Создаём TCP-слушатель на всех интерфейсах и указанном порту
				listener.Start();                                   //3. Запускаем прослушивание
				isRunning = true;                                   //4. Устанавливаем флаг работы сервера
				Log($"Сервер запущен на порту {port}");             //5. Записываем в лог (и вызываем событие OnLog)
				await AcceptClientsAsync();                         //6. Запускаем асинхронный цикл приёма клиентов
			}
			catch (Exception ex)                                    //7. Если произошла ошибка (например, порт занят)
			{
				Log($"Ошибка запуска: {ex.Message}");               //8. Логируем ошибку
				throw;                                              //9. Перебрасываем исключение выше (вызывающий код узнает об ошибке)
			}
		}

		public void Stop()
		{
			isRunning = false;                              // 1. Снимаем флаг работы сервера
			listener?.Stop();                               // 2. Останавливаем прослушивание (если listener не null)
			foreach (TcpClient client in clientNick.Keys)   // 3. Перебираем всех подключённых клиентов
				client.Close();                             // 4. Закрываем TCP-соединение с каждым клиентом
			clientNick.Clear();                             // 5. Очищаем словарь "клиент -> ник"
			nickToClient.Clear();                           // 6. Очищаем словарь "ник -> клиент"
			ConnectedClients.Clear();                       // 7. Очищаем коллекцию ников для UI
			dialogs.Clear();                                // 8. Очищаем все сохранённые диалоги (история сообщений)
			broadcastMessages.Clear();                      // 9. Очищаем сообщения общего чата
			Log("Сервер остановлен.");                      // 10. Записываем в лог и уведомляем UI
		}

		private async Task AcceptClientsAsync()
		{
			while (isRunning)													// 1. Цикл работает, пока сервер запущен
			{
				try
				{
					TcpClient client = await listener.AcceptTcpClientAsync();   // 2. Ожидаем подключения нового клиента
					_ = Task.Run(() => HandleClientAsync(client));				// 3. Запускаем обработку клиента в отдельной задаче
				}
				catch (ObjectDisposedException) { break; }						// 4. Если listener был уничтожен (вызван Stop), выходим из цикла
				catch (Exception ex) { Log($"Ошибка Accept: {ex.Message}"); }	// 5. Логируем другие ошибки и продолжаем цикл
			}
		}

		private async Task HandleClientAsync(TcpClient client)
		{
			// Получаем строковое представление удалённой конечной точки (IP:порт)
			string endpoint = client.Client.RemoteEndPoint.ToString();
			// Логируем попытку подключения (администратор видит в окне сервера)
			Log($"Новый клиент {endpoint} пытается подключиться.");

			// Получаем поток для чтения/записи данных с этим клиентом
			NetworkStream stream = client.GetStream();
			// Буфер для приёма данных (4 КБ – достаточно для команд и небольших сообщений)
			byte[] buffer = new byte[4096];

			// Читаем первое сообщение – это должен быть ник
			int bytes = await stream.ReadAsync(buffer, 0, buffer.Length);
			if (bytes == 0) return;   // Если ничего не прочитано – завершаем
									  // Преобразуем байты в строку и убираем лишние пробелы/переводы строк
			string firstMsg = Encoding.UTF8.GetString(buffer, 0, bytes).Trim();

			// Проверяем, что сообщение начинается с "/nick "
			if (!firstMsg.StartsWith("/nick "))
			{
				// Отправляем ошибку и закрываем соединение
				await SendErrorMessage(client, "Необходимо указать ник: /nick ВашНик");
				client.Close();
				return;
			}

			// Извлекаем запрошенный ник (удаляем "/nick ")
			string requestedNick = firstMsg.Substring(6).Trim();
			if (string.IsNullOrEmpty(requestedNick))
			{
				await SendErrorMessage(client, "Ник не может быть пустым.");
				client.Close();
				return;
			}

			// Ограничение на максимальное количество клиентов (10)
			if (nickToClient.Count >= 10)
			{
				await SendErrorMessage(client, "Достигнуто максимальное количество клиентов (10).");
				client.Close();
				return;
			}

			// Проверяем, не занят ли ник
			if (nickToClient.ContainsKey(requestedNick))
			{
				await SendErrorMessage(client, $"Ник '{requestedNick}' уже занят.");
				client.Close();
				return;
			}

			// Регистрируем клиента: запоминаем ник в обоих словарях
			clientNick[client] = requestedNick;
			nickToClient[requestedNick] = client;

			// Создаём диалог между администратором и этим клиентом (для отображения в левой панели сервера)
			string adminDialogKey = GetDialogKey("Админ", requestedNick);
			dialogs.GetOrAdd(adminDialogKey, new ObservableCollection<ChatMessage>());

			// Обновляем UI сервера: добавляем ник в список ConnectedClients
			Application.Current.Dispatcher.Invoke(() => ConnectedClients.Add(requestedNick));
			// Логируем успешное подключение с временем
			Log($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} Клиент {requestedNick} ({endpoint}) подключился.");

			// Отправляем клиенту подтверждение "/nick ok"
			byte[] success = Encoding.UTF8.GetBytes("/nick ok");
			await stream.WriteAsync(success, 0, success.Length);

			// Отправляем новому клиенту список уже подключённых пользователей (без него самого)
			await SendUserListToClient(client, requestedNick);
			// Рассылаем остальным клиентам обновлённый список пользователей (включая нового)
			await BroadcastUserList(client);
			// Уведомляем UI сервера о подключении через событие
			ClientConnected?.Invoke(requestedNick);

			// ===== Основной цикл приёма сообщений =====
			try
			{
				while (true)
				{
					// Читаем очередное сообщение от клиента
					bytes = await stream.ReadAsync(buffer, 0, buffer.Length);
					if (bytes == 0) break;   // Соединение закрыто клиентом

					string msg = Encoding.UTF8.GetString(buffer, 0, bytes);
					string time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

					// ===== Приватное сообщение (/msg ник текст) =====
					if (msg.StartsWith("/msg "))
					{
						// Разделяем строку после "/msg " на две части: получатель и текст
						string[] parts = msg.Substring(5).Split(new[] { ' ' }, 2);
						if (parts.Length == 2)
						{
							string recipient = parts[0];
							string privateMsg = parts[1];
							// Передаём на обработку в метод SendPrivateMessage
							await SendPrivateMessage(requestedNick, recipient, privateMsg, time);
						}
						else
						{
							// Неверный формат – отправляем подсказку
							await SendPrivateMessage(requestedNick, requestedNick, "Неверный формат. Используйте /msg ник сообщение", time);
						}
					}
					// ===== Общее сообщение (/all текст) =====
					else if (msg.StartsWith("/all "))
					{
						string broadcastMsg = msg.Substring(5);
						string formatted = $"[{time}] {requestedNick}: {broadcastMsg}";
						Log(formatted);                       // Пишем в лог сервера
						OnMessageReceived?.Invoke(formatted); // Событие для UI (если нужно)
															  // Рассылаем сообщение всем клиентам, кроме отправителя
						await BroadcastAsync(formatted, requestedNick);
					}
					// ===== Любая другая команда – ошибка =====
					else
					{
						string errorMsg = $"Используйте /msg ник сообщение или /all сообщение";
						byte[] errorData = Encoding.UTF8.GetBytes(errorMsg);
						await stream.WriteAsync(errorData, 0, errorData.Length);
					}
				}
			}
			catch (Exception ex)
			{
				// При любой ошибке (например, разрыв соединения) логируем
				Log($"Ошибка с {requestedNick}: {ex.Message}");
			}
			finally
			{
				// ===== Очистка при отключении клиента =====
				// Удаляем клиента из словарей
				clientNick.TryRemove(client, out _);
				nickToClient.TryRemove(requestedNick, out _);
				// Обновляем UI сервера – убираем ник из списка
				Application.Current.Dispatcher.Invoke(() => ConnectedClients.Remove(requestedNick));
				// Уведомляем UI сервера об отключении
				ClientDisconnected?.Invoke(requestedNick);
				// Логируем отключение с временем
				Log($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} Клиент {requestedNick} отключился.");
				// Рассылаем остальным клиентам обновлённый список пользователей (без этого клиента)
				await BroadcastUserList(null);
				// Закрываем TCP-соединение
				client.Close();
			}
		}

		private async Task SendUserListToClient(TcpClient client, string selfNick)
		{
			// Формируем строку, содержащую все ники подключённых клиентов, кроме самого себя.
			// nickToClient.Keys – коллекция ников.
			// .Where(n => n != selfNick) – исключаем ник самого клиента.
			// string.Join(",", ...) – объединяем ники через запятую.
			string list = string.Join(",", nickToClient.Keys.Where(n => n != selfNick));

			// Формируем команду /users, которую клиент распознаёт и обновляет свой список пользователей.
			string userListMsg = $"/users {list}";

			// Преобразуем строку в массив байт для отправки по сети.
			byte[] data = Encoding.UTF8.GetBytes(userListMsg);

			// Отправляем данные клиенту через его сетевой поток.
			await client.GetStream().WriteAsync(data, 0, data.Length);
		}

		private async Task BroadcastUserList(TcpClient excludeClient)
		{
			// Формируем строку, содержащую ВСЕ ники подключённых клиентов (включая отправителя? да, включая).
			// Это нужно, чтобы каждый клиент знал полный список, но сам себя он потом отфильтрует в UI.
			string list = string.Join(",", nickToClient.Keys);
			string userListMsg = $"/users {list}";
			byte[] data = Encoding.UTF8.GetBytes(userListMsg);

			// Перебираем всех подключённых клиентов.
			foreach (TcpClient client in nickToClient.Values)
			{
				// Пропускаем клиента, которого нужно исключить (например, отправителя запроса или только что отключившегося).
				if (excludeClient != null && client == excludeClient) continue;

				try
				{
					// Если клиент ещё подключён, отправляем ему список.
					if (client.Connected)
						await client.GetStream().WriteAsync(data, 0, data.Length);
				}
				catch
				{
					// Игнорируем ошибки отправки (например, клиент внезапно отключился).
				}
			}
		}

		private async Task SendErrorMessage(TcpClient client, string error)
		{
			// Преобразуем строку с текстом ошибки в массив байт, используя кодировку UTF-8
			byte[] data = Encoding.UTF8.GetBytes(error);
			// Отправляем полученный массив байт клиенту через его сетевой поток
			// WriteAsync(данные, смещение, количество байт) – асинхронная отправка
			await client.GetStream().WriteAsync(data, 0, data.Length);
		}

		private string GetDialogKey(string nick1, string nick2)
		{
			// Создаём массив строк из двух ников
			string[] sorted = new[] { nick1, nick2 }
				// Сортируем массив в алфавитном порядке (по умолчанию – по возрастанию)
				.OrderBy(x => x)
				// Преобразуем обратно в массив (OrderBy возвращает IEnumerable, нужен ToArray)
				.ToArray();
			// Формируем строку-ключ, разделяя ники символом '|'
			// После сортировки порядок ников не зависит от того, кто отправитель, а кто получатель
			return $"{sorted[0]}|{sorted[1]}";
		}

		private async Task SendPrivateMessage(string senderNick, string recipientNick, string message, string time)
		{
			// 1. Проверяем, существует ли получатель в словаре подключённых клиентов
			if (!nickToClient.ContainsKey(recipientNick))
			{
				// 2. Если получатель не найден, формируем сообщение об ошибке
				string errorMsg = $"[{time}] Пользователь {recipientNick} не найден.";
				// 3. Отправляем ошибку обратно отправителю (если отправитель ещё онлайн)
				if (nickToClient.TryGetValue(senderNick, out TcpClient senderClient))
				{
					byte[] data = Encoding.UTF8.GetBytes(errorMsg);
					await senderClient.GetStream().WriteAsync(data, 0, data.Length);
				}
				return; // Завершаем выполнение
			}

			// 4. Генерируем ключ диалога (сортировка ников, чтобы "User1|User2" == "User2|User1")
			string dialogKey = GetDialogKey(senderNick, recipientNick);
			// 5. Получаем или создаём коллекцию сообщений для этого диалога
			ObservableCollection<ChatMessage> dialogMessages = dialogs.GetOrAdd(dialogKey, new ObservableCollection<ChatMessage>());

			// 6. Создаём объект сообщения
			ChatMessage chatMsg = new ChatMessage
			{
				Author = senderNick,               // Отправитель
				Text = message,                    // Текст сообщения
				Timestamp = DateTime.Parse(time),  // Время отправки
				IsOwn = false                      // Для сервера это сообщение не от админа, оно от клиента
			};

			// 7. Добавляем сообщение в коллекцию диалога (обязательно через Dispatcher, так как коллекция может быть привязана к UI)
			Application.Current.Dispatcher.Invoke(() => dialogMessages.Add(chatMsg));
			// 8. Уведомляем UI сервера о новом сообщении в этом диалоге
			DialogMessageReceived?.Invoke(dialogKey, chatMsg);

			// 9. Форматируем сообщение для отправки получателю (с временем и отправителем)
			string formatted = $"[{time}] {senderNick}: {message}";
			byte[] dataToRecipient = Encoding.UTF8.GetBytes(formatted);
			// 10. Получаем TCP-клиент получателя
			TcpClient recipientClient = nickToClient[recipientNick];
			// 11. Отправляем сообщение получателю
			await recipientClient.GetStream().WriteAsync(dataToRecipient, 0, dataToRecipient.Length);

			// Примечание: отправитель не получает обратно своё сообщение, так как предполагается, что он уже добавил его локально (в клиенте).
			// Это предотвращает дублирование сообщений в чате отправителя.
		}

		private async Task BroadcastAsync(string formattedMessage, string senderNick)
		{
			// 1. Создаём объект сообщения для общего чата
			ChatMessage broadcastMsg = new ChatMessage
			{
				Author = senderNick,           // Ник отправителя
				Text = formattedMessage,       // Отформатированное сообщение (с временем и ником)
				Timestamp = DateTime.Now,      // Текущее время
				IsOwn = false                  // Для сервера это сообщение не от админа
			};

			// 2. Добавляем сообщение в коллекцию broadcastMessages (общий чат сервера)
			//    Используем Dispatcher, так как коллекция может быть привязана к UI окна сервера
			Application.Current.Dispatcher.Invoke(() => broadcastMessages.Add(broadcastMsg));

			// 3. Уведомляем UI сервера о новом сообщении в общем чате
			BroadcastMessageReceived?.Invoke(broadcastMsg);

			// 4. Преобразуем отформатированное сообщение в байты для отправки
			byte[] data = Encoding.UTF8.GetBytes(formattedMessage);

			// 5. Перебираем всех подключённых клиентов
			foreach (TcpClient client in nickToClient.Values)
			{
				// 6. Пропускаем отправителя (не отправляем сообщение обратно ему)
				if (clientNick[client] == senderNick) continue;

				try
				{
					// 7. Если клиент всё ещё подключён, отправляем ему сообщение
					if (client.Connected)
						await client.GetStream().WriteAsync(data, 0, data.Length);
				}
				catch
				{
					// Игнорируем ошибки отправки (например, клиент внезапно отключился)
				}
			}
		}

		public async Task SendAdminMessageToDialog(string nick1, string nick2, string message)
		{
			// Генерируем ключ диалога на основе двух ников (сортировка гарантирует уникальность)
			string dialogKey = GetDialogKey(nick1, nick2);
			// Получаем или создаём коллекцию сообщений для этого диалога
			ObservableCollection<ChatMessage> dialogMessages = dialogs.GetOrAdd(dialogKey, new ObservableCollection<ChatMessage>());
			// Создаём объект сообщения от администратора
			ChatMessage adminMsg = new ChatMessage
			{
				Author = "Админ",          // Отправитель – администратор
				Text = message,            // Текст сообщения
				Timestamp = DateTime.Now,  // Текущее время
				IsOwn = true               // Для UI сервера это сообщение считается "своим" (от администратора)
			};
			// Добавляем сообщение в историю диалога (через Dispatcher, так как коллекция привязана к UI)
			Application.Current.Dispatcher.Invoke(() => dialogMessages.Add(adminMsg));
			// Уведомляем UI сервера о новом сообщении в этом диалоге
			DialogMessageReceived?.Invoke(dialogKey, adminMsg);

			// Форматируем сообщение для отправки клиентам (без даты, только время)
			string formatted = $"[{DateTime.Now:HH:mm:ss}] Админ: {message}";
			byte[] data = Encoding.UTF8.GetBytes(formatted);
			// Отправляем первому участнику диалога (если он онлайн)
			if (nickToClient.TryGetValue(nick1, out TcpClient client1))
				await client1.GetStream().WriteAsync(data, 0, data.Length);
			// Отправляем второму участнику диалога (если он онлайн)
			if (nickToClient.TryGetValue(nick2, out TcpClient client2))
				await client2.GetStream().WriteAsync(data, 0, data.Length);
		}

		public async Task SendAdminMessageToAll(string message)
		{
			// Создаём объект сообщения от администратора для общего чата
			ChatMessage adminMsg = new ChatMessage
			{
				Author = "Админ",                    // Отправитель – администратор
				Text = message,                      // Текст сообщения
				Timestamp = DateTime.Now,            // Текущее время
				IsOwn = true                         // Для сервера это сообщение от администратора
			};

			// Добавляем сообщение в коллекцию broadcastMessages (общий чат сервера)
			// Используем Dispatcher, так как коллекция привязана к UI окна сервера
			Application.Current.Dispatcher.Invoke(() => broadcastMessages.Add(adminMsg));

			// Уведомляем UI сервера о новом сообщении в общем чате
			BroadcastMessageReceived?.Invoke(adminMsg);

			// Преобразуем текст сообщения в байты
			byte[] data = Encoding.UTF8.GetBytes(message);

			// Рассылаем сообщение всем подключённым клиентам
			foreach (TcpClient client in nickToClient.Values)
			{
				try
				{
					if (client.Connected)
						await client.GetStream().WriteAsync(data, 0, data.Length);
				}
				catch
				{
					// Игнорируем ошибки отправки (например, клиент отключился)
				}
			}
		}

		public ObservableCollection<ChatMessage> GetDialogMessages(string nick1, string nick2)
		{
			// Генерируем ключ диалога для пары пользователей (с сортировкой ников)
			string key = GetDialogKey(nick1, nick2);
			// Пытаемся получить из словаря dialogs коллекцию сообщений по этому ключу
			// Если ключ существует, возвращаем коллекцию, иначе – null
			return dialogs.TryGetValue(key, out ObservableCollection<ChatMessage> messages) ? messages : null;
		}

		public ObservableCollection<ChatMessage> GetBroadcastMessages() => broadcastMessages;   //возвращаю коллекцию сообщений общего чата

		public System.Collections.Generic.IEnumerable<string> GetAllDialogs() => dialogs.Keys;  //возвращаю перечисление всех ключей диалогов

		private void Log(string text)
		{
			// Вызываем событие OnLog, чтобы уведомить подписчиков (например, окно сервера) о новом лог-сообщении
			OnLog?.Invoke(text);
			// Блокируем доступ к файлу (lock), чтобы избежать конфликтов при одновременной записи из разных потоков
			lock (fileLock)
			{
				// Добавляем строку в конец файла с переводом строки
				File.AppendAllText(logFilePath, text + Environment.NewLine);
			}
		}

		public void Dispose()
		{
			// Останавливаем сервер (закрываем все соединения, очищаем коллекции, логируем)
			Stop();
		}
	}
}