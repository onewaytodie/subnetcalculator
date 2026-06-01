using System;
using System.Collections.ObjectModel;   //для ObservableCollection
using System.Net.Sockets;				//Библа для TCP-клиента
using System.Text;
using System.Threading.Tasks;           //для Task и async/await
using System.Windows;
using SubnetCalculator.Chat.Models;

namespace SubnetCalculator.Chat.Views
{
	public partial class ClientWindow : Window
	{
		private TcpClient client;																		// TCP-клиент для соединения с сервером
		private NetworkStream stream;																	// Поток для чтения и записи данных
		private bool isConnected;																		// Флаг: подключён ли клиент к серверу
		private string selectedUser;																	// Ник выбранного в списке собеседника
		private string myNick;																			// Ник текущего пользователя
		private ObservableCollection<ChatMessage> messages = new ObservableCollection<ChatMessage>();	// Коллекция сообщений (привязывается к ItemsControl)
		private ObservableCollection<string> users = new ObservableCollection<string>();                // Коллекция ников пользователей (привязывается к ListBox)

		public ClientWindow()
		{
			InitializeComponent();							// Инициализация XAML-компонентов
			UsersListBox.ItemsSource = users;				// Привязываем список пользователей к ListBox
			MessagesItemsControl.ItemsSource = messages;	// Привязываем список сообщений к ItemsControl
		}

		// Обработчик нажатия кнопки "Подключиться". Асинхронный, чтобы не блокировать UI.
		private async void BtnConnect_Click(object sender, RoutedEventArgs e)
		{
			// === 1. Проверка корректности порта ===
			// Пытаемся преобразовать текст из txtPort в целое число.
			// out int port – переменная, куда будет записан результат.
			if (!int.TryParse(txtPort.Text, out int port))
			{
				// Если порт не число – показываем ошибку и выходим.
				MessageBox.Show("Неверный порт", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}

			// === 2. Блок попытки подключения ===
			try
			{
				// Создаём новый экземпляр TcpClient – это объект, представляющий TCP-соединение.
				// TcpClient из пространства имён System.Net.Sockets.
				client = new TcpClient();

				// Асинхронно подключаемся к серверу по указанному IP и порту.
				// await – приостанавливает выполнение метода, но не блокирует поток UI.
				// После завершения подключения выполнение продолжается.
				await client.ConnectAsync(txtServerIP.Text, port);

				// Получаем поток для чтения/записи данных. NetworkStream – двунаправленный поток.
				stream = client.GetStream();

				// === 3. Регистрация ника ===
				// Берём текст из поля txtNick, убираем пробелы по краям.
				myNick = txtNick.Text.Trim();
				if (string.IsNullOrEmpty(myNick)) myNick = "User"; // Защита от пустого ника.

				// Запрещаем использовать ник "Админ" (регистронезависимо), чтобы не было путаницы с сервером.
				if (myNick.Equals("Админ", StringComparison.OrdinalIgnoreCase))
				{
					MessageBox.Show("Ник 'Админ' зарезервирован для сервера. Выберите другой ник.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
					client.Close(); // Закрываем сокет.
					return;
				}

				// Формируем команду /nick и преобразуем её в байты (UTF-8).
				byte[] nickData = Encoding.UTF8.GetBytes($"/nick {myNick}");

				// Асинхронно отправляем команду на сервер.
				await stream.WriteAsync(nickData, 0, nickData.Length);

				// === 4. Ожидание подтверждения от сервера ===
				// Буфер для приёма данных (4 КБ – достаточно для короткого ответа).
				byte[] buffer = new byte[4096];
				// Читаем ответ. bytes – количество прочитанных байт.
				int bytes = await stream.ReadAsync(buffer, 0, buffer.Length);
				// Преобразуем байты в строку.
				string response = Encoding.UTF8.GetString(buffer, 0, bytes);

				// Если сервер не ответил "/nick ok" – ошибка регистрации.
				if (response != "/nick ok")
				{
					MessageBox.Show($"Ошибка регистрации: {response}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
					client.Close();
					return;
				}

				// === 5. Успешное подключение ===
				isConnected = true;                 // Устанавливаем флаг подключения.
				AddSystemMessage("Подключено к серверу."); // Добавляем системное сообщение в чат.

				// Обновляем состояние кнопок и полей ввода.
				btnConnect.IsEnabled = false;       // Отключаем кнопку "Подключиться".
				btnDisconnect.IsEnabled = true;     // Включаем кнопку "Отключиться".
				btnSend.IsEnabled = true;           // Включаем кнопку отправки сообщений.
													// btnSendFile.IsEnabled = true;    // Отключено (файловый обмен временно не работает).
				txtMessage.IsEnabled = true;        // Разрешаем ввод текста.

				// Запускаем асинхронный метод приёма сообщений в фоне.
				// _ = – отбрасываем задачу (fire-and-forget), не ожидаем её завершения.
				// Метод ReceiveMessagesAsync будет работать в фоновом потоке (пул потоков) и обновлять UI через Dispatcher.
				_ = ReceiveMessagesAsync();
			}
			catch (Exception ex)
			{
				// Если на любом этапе произошла ошибка (например, сервер не отвечает, порт закрыт, сеть недоступна),
				// показываем сообщение об ошибке.
				MessageBox.Show($"Ошибка подключения: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}

		// Асинхронный метод, работающий в фоновом режиме. Не возвращает значение (Task).
		// Он запускается один раз после успешного подключения и работает до отключения.
		private async Task ReceiveMessagesAsync()
		{
			// Буфер для приёма данных. 4096 байт = 4 КБ – достаточно для типовых сообщений.
			// При необходимости размер можно увеличить (например, 1 МБ для файлов, но сейчас файлы отключены).
			byte[] buffer = new byte[4096];

			try
			{
				// Цикл работает, пока флаг isConnected = true.
				// При отключении (Disconnect) isConnected становится false, и цикл прерывается.
				while (isConnected)
				{
					// Асинхронно читаем данные из сетевого потока.
					// ReadAsync возвращает количество реально прочитанных байт.
					// Если соединение закрыто, возвращает 0.
					int bytes = await stream.ReadAsync(buffer, 0, buffer.Length);
					if (bytes == 0) break; // Соединение закрыто – выходим из цикла.

					// Преобразуем массив байт в строку (кодировка UTF-8).
					// Используем только фактически прочитанные байты (bytes), чтобы избежать мусора в конце.
					string msg = Encoding.UTF8.GetString(buffer, 0, bytes);

					// ===== Обработка специальной команды /users =====
					// Сервер присылает список пользователей в формате: "/users ник1,ник2,ник3"
					if (msg.StartsWith("/users "))
					{
						// Извлекаем часть после "/users " (длина префикса = 7)
						string usersData = msg.Substring(7);
						// Разбиваем строку по запятым, удаляем пустые элементы (на случай лишних запятых)
						string[] usersArray = usersData.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

						// Обновляем UI (коллекцию users, привязанную к ListBox).
						// Dispatcher.Invoke обязателен, так как этот метод выполняется в фоновом потоке.
						Dispatcher.Invoke(() =>
						{
							users.Clear();                              // Очищаем старый список
							users.Add("Общий чат");                     // Добавляем псевдо-пользователя для общего чата
							foreach (string u in usersArray)
								if (u != myNick) users.Add(u);          // Добавляем всех, кроме себя
						});
					}
					else
					{
						// ===== Обычное текстовое сообщение от сервера =====
						// Это может быть:
						// - сообщение от другого пользователя (приватное /msg)
						// - сообщение в общий чат (/all)
						// - системное уведомление (например, "Клиент X подключился")
						// Сервер уже отформатировал строку (включил время, ник отправителя).
						Dispatcher.Invoke(() => AddMessage(new ChatMessage
						{
							Author = "Сервер",          // Отображаем как "Сервер", но содержимое строки уже содержит ник
							Text = msg,                 // Само сообщение (уже готовое)
							Timestamp = DateTime.Now,   // Время получения (можно было бы взять из сообщения, но для простоты – текущее)
							IsOwn = false               // Это не наше сообщение, поэтому выравнивается слева
						}));
					}
				}
			}
			catch (Exception ex)
			{
				// При любой ошибке (например, разрыв соединения, проблемы с сетью) показываем системное сообщение.
				// Используем Dispatcher, чтобы обновить UI из фонового потока.
				//Dispatcher.Invoke(() => AddSystemMessage($"Ошибка приёма: {ex.Message}"));
			}
			finally
			{
				// В любом случае (нормальный выход из цикла, исключение или разрыв) отключаемся.
				// Dispatcher.Invoke нужен, потому что Disconnect обновляет UI (кнопки, поле ввода).
				Dispatcher.Invoke(() => Disconnect());
			}
		}
		//https://learn.microsoft.com/ru-ru/dotnet/csharp/asynchronous-programming/
		//https://learn.microsoft.com/ru-ru/dotnet/api/system.net.sockets.networkstream.readasync?view=net-10.0
		//https://learn.microsoft.com/ru-ru/dotnet/api/system.windows.threading.dispatcher.invoke?view=windowsdesktop-10.0



		private async void SendFileButton_Click(object sender, RoutedEventArgs e)
		{
			AddSystemMessage("Отправка файлов отключена.");
			return;
		}

		// Обработчик нажатия кнопки "Отправить". Отправляет текстовое сообщение на сервер.
		// Не асинхронный (void), так как операция записи в поток выполняется синхронно.
		// Для простоты можно было бы сделать async, но в данном случае это не требуется.
		private void SendButton_Click(object sender, RoutedEventArgs e)
		{
			// === 1. Проверка: подключён ли клиент? ===
			if (!isConnected) return;   // Если не подключён – игнорируем нажатие.

			// === 2. Получаем текст из поля ввода и удаляем пробелы по краям ===
			string msg = txtMessage.Text.Trim();
			if (string.IsNullOrEmpty(msg)) return;   // Если пусто – ничего не отправляем.

			// === 3. Формируем команду в зависимости от выбранного собеседника ===
			// selectedUser может быть:
			//   - null (ещё не выбран)
			//   - "Общий чат" (отправляем всем через /all)
			//   - конкретный ник (отправляем приватное сообщение через /msg)
			if (selectedUser == null || selectedUser == "Общий чат")
			{
				// Команда для общего чата: /all текст
				string command = $"/all {msg}";

				// Преобразуем строку команды в массив байт (UTF-8)
				byte[] data = Encoding.UTF8.GetBytes(command);

				// Синхронно записываем данные в сетевой поток (отправляем на сервер)
				// Примечание: для учебного проекта синхронная запись допустима, так как сообщения небольшие.
				// В реальном приложении лучше использовать асинхронный метод WriteAsync.
				stream.Write(data, 0, data.Length);

				// Оптимистичное обновление UI: сразу добавляем своё сообщение в локальную коллекцию,
				// чтобы оно отобразилось мгновенно, не дожидаясь ответа сервера.
				// Это создаёт эффект быстрой отправки (сервер потом не пришлёт эхо, так как он не шлёт отправителю).
				AddMessage(new ChatMessage
				{
					Author = "Я",                 // Отображается как "Я"
					Text = msg,                  // Текст сообщения
					Timestamp = DateTime.Now,    // Время отправки (локальное)
					IsOwn = true                 // Помечаем как своё (для правильного выравнивания и цвета)
				});
			}
			else
			{
				// Команда для приватного сообщения: /msg ник_получателя текст
				string command = $"/msg {selectedUser} {msg}";
				byte[] data = Encoding.UTF8.GetBytes(command);
				stream.Write(data, 0, data.Length);

				// Также добавляем своё сообщение локально (оптимистичное обновление)
				AddMessage(new ChatMessage
				{
					Author = "Я",
					Text = msg,
					Timestamp = DateTime.Now,
					IsOwn = true
				});
			}

			// === 4. Очищаем поле ввода и возвращаем фокус для удобства пользователя ===
			txtMessage.Clear();
			txtMessage.Focus();   // После отправки курсор снова в поле ввода, можно писать следующее сообщение
		}

		// Обработчик события изменения выбранного элемента в списке пользователей (ListBox).
		// Этот метод вызывается, когда пользователь кликает по другому элементу в левой панели.
		private void UsersListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
		{
			// === 1. Получение выбранного элемента ===
			// UsersListBox.SelectedItem – объект, хранящий выбранный элемент.
			// Пытаемся привести его к строке (string), так как наша коллекция users содержит строки (ники).
			if (UsersListBox.SelectedItem is string user)
			{
				// === 2. Сохраняем выбранного пользователя ===
				// selectedUser – поле класса, запоминающее ник выбранного собеседника.
				// Оно будет использоваться при отправке сообщений (чтобы знать, кому отправлять).
				selectedUser = user;

				// === 3. Обновляем заголовок чата ===
				// ChatTitle – это TextBlock, который отображает, с кем сейчас идёт диалог.
				// Если выбран "Общий чат", то заголовок – "Общий чат".
				// Иначе отображаем "Чат с {ник}".
				ChatTitle.Text = user == "Общий чат" ? "Общий чат" : $"Чат с {user}";
			}
		}

		// Метод для добавления сообщения в коллекцию messages и автоматической прокрутки чата вниз.
		// Он вызывается из разных мест (из ReceiveMessagesAsync, из SendButton_Click и из AddSystemMessage).
		// msg – объект ChatMessage, который нужно добавить.
		private void AddMessage(ChatMessage msg)
		{
			// === 1. Обеспечение потокобезопасного доступа к UI ===
			// Этот метод может быть вызван из фонового потока (например, из ReceiveMessagesAsync, который работает в пуле потоков).
			// WPF требует, чтобы изменения UI-элементов (коллекция messages привязана к ItemsControl) выполнялись в потоке диспетчера.
			// Dispatcher.Invoke синхронно выполняет переданное действие в потоке UI.
			Dispatcher.Invoke(() =>
			{
				// === 2. Добавление сообщения в коллекцию ===
				// messages – ObservableCollection<ChatMessage>, привязанная к MessagesItemsControl.
				// При добавлении элемента ItemsControl автоматически обновляет свой список (благодаря INotifyCollectionChanged).
				messages.Add(msg);

				// === 3. Автоматическая прокрутка вниз ===
				// MessagesScrollViewer – это ScrollViewer, содержащий ItemsControl.
				// ScrollToEnd() перемещает скролл в самый низ, чтобы последнее сообщение было видно.
				MessagesScrollViewer.ScrollToEnd();
			});
		}

		// Метод для добавления системного сообщения (например, "Подключено к серверу", "Отключено").
		// Он упрощает создание сообщений от имени "Система" без необходимости каждый раз создавать объект ChatMessage вручную.
		// Принимает текст системного сообщения.
		private void AddSystemMessage(string text)
		{
			AddMessage(new ChatMessage		// Вызываем AddMessage, передавая новый экземпляр ChatMessage с предустановленными полями:
			{
				Author = "Система",			// - Author = "Система" – отображается как отправитель "Система"
				Text = text,				// - Text = text – переданный текст сообщения
				Timestamp = DateTime.Now,   // - Timestamp = DateTime.Now – текущее время (момент создания)
				IsOwn = false               // - IsOwn = false – системные сообщения не являются собственными (обычно выравниваются слева, серый цвет)
			});
		}

		// Метод для разрыва соединения с сервером и сброса состояния клиента.
		// Вызывается:
		//   - при нажатии кнопки "Отключиться" (BtnDisconnect_Click)
		//   - при закрытии окна (OnClosing)
		//   - из ReceiveMessagesAsync при разрыве связи (в finally)
		private void Disconnect()
		{
			// === 1. Если клиент всё ещё подключён, выполняем очистку ресурсов ===
			if (isConnected)
			{
				// Сбрасываем флаг подключения, чтобы остановить цикл приёма сообщений.
				isConnected = false;

				// Закрываем сетевой поток (если он не null). Оператор ?. защищает от NullReferenceException.
				stream?.Close();

				// Закрываем TCP-клиент (сокет). Это разрывает соединение.
				client?.Close();

				// Добавляем системное сообщение в чат, информируя пользователя об отключении.
				AddSystemMessage("Отключено от сервера.");
			}

			// === 2. Обновляем состояние UI-элементов (независимо от того, было ли соединение) ===
			// Это нужно, чтобы интерфейс всегда приходил в исходное состояние после отключения.
			// Включаем кнопку "Подключиться" (пользователь может снова подключиться).
			btnConnect.IsEnabled = true;

			// Отключаем кнопку "Отключиться" (она больше не нужна, пока не установлено новое соединение).
			btnDisconnect.IsEnabled = false;

			// Отключаем кнопку отправки текстовых сообщений.
			btnSend.IsEnabled = false;

			// Отключаем кнопку отправки файлов (если она была включена).
			btnSendFile.IsEnabled = false;

			// Отключаем поле ввода сообщения (пользователь не может писать текст, пока не подключится заново).
			txtMessage.IsEnabled = false;
		}
		//https://learn.microsoft.com/ru-ru/dotnet/api/system.net.sockets.networkstream.close?view=net-10.0

		// Обработчик нажатия кнопки "Отключиться". Вызывает метод Disconnect().
		// Используется синтаксис expression-bodied member (=>) – сокращённая запись для метода, который состоит из одного выражения.
		private void BtnDisconnect_Click(object sender, RoutedEventArgs e) => Disconnect();

		protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
		{
			// Вызываем Disconnect(), чтобы корректно разорвать соединение с сервером:
			// - закрыть сетевой поток и сокет
			// - обновить UI (кнопки, поле ввода)
			// - добавить системное сообщение об отключении
			Disconnect();

			// Вызываеn базовую реализацию, чтобы окно действительно закрылось. 
			//https://learn.microsoft.com/ru-ru/dotnet/api/system.windows.window.onclosing?view=windowsdesktop-10.0
			base.OnClosing(e);
		}
	}
}