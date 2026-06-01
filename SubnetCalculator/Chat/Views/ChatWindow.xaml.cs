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
		private ChatServer server; // Экземпляр сервера чата, который управляет сетевыми соединениями, клиентами, диалогами и сообщениями.
		private string selectedDialogKey; // Ключ текущего выбранного диалога в левой панели (DialogsListView).
		private ObservableCollection<ChatMessage> currentMessages = new ObservableCollection<ChatMessage>(); // Коллекция сообщений для текущего выбранного диалога (привязана к MessagesItemsControl в XAML).
		private ObservableCollection<string> dialogsList = new ObservableCollection<string>(); // Коллекция строк – список всех доступных диалогов (привязана к DialogsListView в XAML).
		public event PropertyChangedEventHandler PropertyChanged; // Событие, реализующее интерфейс INotifyPropertyChanged.
		protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name)); //метод для вызова события PropertyChanged.

		// Конструктор окна сервера чата. Вызывается при создании нового экземпляра окна (например, из MainWindow).
		public ChatWindow()
		{
			// Инициализация XAML-компонентов: загружает и связывает все элементы, описанные в ChatWindow.xaml.
			// Без этого вызова окно не отобразится, а дочерние элементы будут null.
			InitializeComponent();

			// Устанавливает DataContext (контекст данных) этого окна равным самому окну (this).
			// Это позволяет использовать привязки (Binding) в XAML, обращаясь к свойствам и методам самого класса ChatWindow.
			DataContext = this;

			// Привязываем коллекцию dialogsList (ObservableCollection<string>) к ListBox с именем DialogsListView.
			// Благодаря этому при добавлении или удалении строк в dialogsList, UI автоматически обновляется.
			DialogsListView.ItemsSource = dialogsList;

			// Привязываем коллекцию currentMessages (ObservableCollection<ChatMessage>) к ItemsControl с именем MessagesItemsControl.
			// При добавлении новых сообщений в currentMessages, они сразу отображаются в правой области чата.
			MessagesItemsControl.ItemsSource = currentMessages;

			// Подписываемся на событие Loaded окна. Это событие возникает один раз после полной инициализации окна (после того, как XAML загружен и все элементы созданы).
			// Обработчик ChatWindow_Loaded будет вызван при загрузке окна.
			Loaded += ChatWindow_Loaded;
		}

		// Обработчик события Loaded окна. Выполняется асинхронно (async) из-за вызова await внутри.
		// Не возвращает значение (void), но помечен async, чтобы использовать await.
		private async void ChatWindow_Loaded(object sender, RoutedEventArgs e)
		{
			// === 1. Создание экземпляра сервера ===
			// ChatServer – класс, реализующий всю сетевую логику (приём клиентов, обработка сообщений, хранение диалогов).
			server = new ChatServer();

			// === 2. Подписка на события сервера ===
			// OnLog – событие для вывода отладочных сообщений (лог). Присоединяем метод AppendLog, который выводит текст в консоль.
			server.OnLog += AppendLog;

			// ClientConnected – возникает при подключении нового клиента (передаётся его ник). Присоединяем метод OnClientConnected,
			// который будет обновлять UI: добавлять клиента в список диалогов и выводить системное сообщение.
			server.ClientConnected += OnClientConnected;

			// ClientDisconnected – возникает при отключении клиента. Присоединяем OnClientDisconnected для обновления UI.
			server.ClientDisconnected += OnClientDisconnected;

			// DialogMessageReceived – возникает при получении нового сообщения в приватном диалоге.
			// Присоединяем OnDialogMessageReceived, который добавляет сообщение в currentMessages, если открыт именно этот диалог.
			server.DialogMessageReceived += OnDialogMessageReceived;

			// BroadcastMessageReceived – возникает при получении сообщения в общем чате (broadcast).
			// Присоединяем OnBroadcastMessageReceived, который добавляет сообщение в currentMessages, если открыт общий чат.
			server.BroadcastMessageReceived += OnBroadcastMessageReceived;

			// === 3. Асинхронный запуск сервера ===
			// Метод StartAsync запускает TcpListener и начинает принимать клиентов на порту 27015.
			// await – асинхронное ожидание. Пока сервер запускается, UI не блокируется.
			// Если запуск не удастся (например, порт уже занят), исключение будет перехвачено внутри ChatServer и залогировано.
			await server.StartAsync(27015);

			// === 4. Логируем успешный запуск ===
			// AppendLog просто пишет строку в отладочный вывод (Debug.WriteLine).
			AppendLog("Сервер запущен.");
		}

		// Обработчик события ClientConnected от сервера. Вызывается, когда новый клиент успешно зарегистрировался (выбрал ник).
		// Событие приходит из фонового потока (из обработчика клиента в ChatServer), поэтому нельзя напрямую обновлять UI.
		private void OnClientConnected(string nick)
		{
			// === 1. Переключение в поток UI через Dispatcher ===
			// Dispatcher.Invoke синхронно выполняет переданное действие в потоке диспетчера (главном потоке WPF).
			// Это необходимо, чтобы безопасно изменять UI-элементы (ObservableCollection, привязанные к XAML).
			//https://learn.microsoft.com/ru-ru/dotnet/api/system.collections.objectmodel.observablecollection-1?view=net-10.0
			Dispatcher.Invoke(() =>
			{
				// === 2. Добавление системного сообщения в общий чат ===
				// server.GetBroadcastMessages() возвращает коллекцию сообщений общего чата (ObservableCollection<ChatMessage>).
				// Добавляем новое сообщение от имени "Система" с текстом о подключении клиента.
				server.GetBroadcastMessages().Add(new ChatMessage
				{
					Author = "Система",                         // Отправитель – система
					Text = $"Клиент {nick} подключился.",       // Текст: "Клиент User1 подключился."
					Timestamp = DateTime.Now,                   // Время события
					IsOwn = false                               // Не своё сообщение (сервер, для UI это чужое)
				});

				// === 3. Обновление текущего отображения, если открыт общий чат ===
				// selectedDialogKey – ключ текущего выбранного диалога.
				// Если администратор в данный момент смотрит общий чат ("Общий чат"), нужно обновить список сообщений,
				// чтобы новое системное сообщение появилось на экране.
				if (selectedDialogKey == "Общий чат")
					RefreshMessages();   // Очищает currentMessages и заполняет её заново из GetBroadcastMessages()

				// === 4. Обновление списка диалогов в левой панели ===
				// UpdateDialogsList() перестраивает список dialogsList, добавляя новые диалоги (например, диалог "Админ|ник").
				// Это нужно, чтобы в левой панели появился новый приватный диалог с подключившимся клиентом.
				UpdateDialogsList();
			});
		}

		// Обработчик события ClientDisconnected от сервера. Вызывается, когда клиент отключился (закрыл окно, нажал "Отключиться" или потерял соединение).
		// Событие приходит из фонового потока (из ChatServer), поэтому необходимо переключение в поток UI через Dispatcher.
		private void OnClientDisconnected(string nick)
		{
			// === 1. Переключение в поток UI ===
			// Dispatcher.Invoke гарантирует, что все изменения UI (добавление сообщения, обновление списков) выполнятся в главном потоке WPF.
			Dispatcher.Invoke(() =>
			{
				// === 2. Добавление системного сообщения в общий чат ===
				// server.GetBroadcastMessages() – коллекция сообщений общего чата.
				// Добавляем сообщение о том, что клиент отключился.
				server.GetBroadcastMessages().Add(new ChatMessage
				{
					Author = "Система",                         // Отправитель – система
					Text = $"Клиент {nick} отключился.",        // Текст: "Клиент User1 отключился."
					Timestamp = DateTime.Now,                   // Время события
					IsOwn = false                               // Не своё сообщение (выравнивается слева)
				});

				// === 3. Обновление текущего отображения, если открыт общий чат ===
				// Если администратор в данный момент смотрит общий чат, нужно обновить список сообщений,
				// чтобы новое системное сообщение появилось на экране.
				if (selectedDialogKey == "Общий чат")
					RefreshMessages();   // Очищает currentMessages и заполняет её заново из GetBroadcastMessages()

				// === 4. Обновление списка диалогов в левой панели ===
				// UpdateDialogsList() перестраивает список диалогов, получая все актуальные ключи через server.GetAllDialogs().
				// Поскольку клиент отключился, приватный диалог "Админ|ник" больше неактивен (но может остаться в истории).
				// Чтобы он исчез из левой панели, нужно обновить список (обычно сервер хранит диалоги даже после отключения,
				// но если решено удалять диалог при отключении, это должно быть в ChatServer. В текущей реализации диалоги сохраняются,
				// так что UpdateDialogsList просто перезагрузит актуальные ключи (диалог не удаляется, но его наличие в списке зависит от логики сервера).
				UpdateDialogsList();
			});
		}

		// Обработчик события DialogMessageReceived от сервера.
		// Вызывается, когда в каком-либо приватном диалоге появляется новое сообщение (от одного из участников).
		// dialogKey – строка вида "ник1|ник2" (отсортированная пара ников).
		// message – объект ChatMessage с автором, текстом, временем и флагом IsOwn (для сервера обычно false, если сообщение не от админа).
		private void OnDialogMessageReceived(string dialogKey, ChatMessage message)
		{
			// Переключаемся в поток UI (событие приходит из фонового потока сервера).
			Dispatcher.Invoke(() =>
			{
				// === 1. Проверка: открыт ли именно этот диалог? ===
				// Если администратор в данный момент смотрит этот приватный диалог (selectedDialogKey совпадает),
				// то добавляем сообщение в коллекцию currentMessages, которая привязана к UI.
				if (selectedDialogKey == dialogKey)
					currentMessages.Add(message);

				// === 2. Обновление списка диалогов в левой панели ===
				// UpdateDialogsList() перестраивает список dialogsList (получает все ключи диалогов через server.GetAllDialogs()).
				// Это нужно, чтобы:
				//   - появился новый диалог, если это первое сообщение в нём
				//   - обновить порядок диалогов (например, поднять диалог с последним сообщением вверх)
				// Хотя в текущей реализации UpdateDialogsList просто перезагружает весь список, это не оптимально, но для учебного проекта приемлемо.
				UpdateDialogsList();
			});
		}

		// Обработчик события BroadcastMessageReceived от сервера.
		// Вызывается, когда в общий чат приходит новое сообщение (от клиента через команду /all или от администратора).
		// message – объект ChatMessage с автором, текстом, временем и флагом IsOwn (для администратора IsOwn = true).
		private void OnBroadcastMessageReceived(ChatMessage message)
		{
			// Переключаемся в поток UI (событие приходит из фонового потока сервера).
			Dispatcher.Invoke(() =>
			{
				// === Проверка: открыт ли общий чат? ===
				// Если администратор смотрит общий чат (selectedDialogKey == "Общий чат"),
				// то добавляем сообщение в коллекцию currentMessages для немедленного отображения.
				if (selectedDialogKey == "Общий чат")
					currentMessages.Add(message);
			});
		}


		// Обновляет список диалогов в левой панели (ListBox с именем DialogsListView).
		// Вызывается при подключении/отключении клиентов и при получении новых сообщений (чтобы отразить новые диалоги или обновить порядок).
		private void UpdateDialogsList()
		{
			// Переключаемся в поток UI, так как метод может быть вызван из фонового потока (например, из событий сервера).
			Dispatcher.Invoke(() =>
			{
				// Получаем все ключи текущих диалогов (строки вида "ник1|ник2") из сервера.
				// server.GetAllDialogs() возвращает IEnumerable<string> (коллекцию ключей).
				// .ToList() преобразует в список, чтобы мы могли работать с фиксированной коллекцией (не меняющейся во время итерации).
				var dialogs = server.GetAllDialogs().ToList();

				// Очищаем текущий список диалогов в UI (ObservableCollection dialogsList).
				// Очистка происходит в потоке UI, что безопасно, так как мы уже внутри Dispatcher.Invoke.
				dialogsList.Clear();

				// Добавляем специальный элемент "Общий чат" в самое начало списка.
				// Этот элемент не является реальным диалогом, а служит для отображения общего чата.
				dialogsList.Add("Общий чат");

				// Перебираем все ключи приватных диалогов, сортируем их в алфавитном порядке (OrderBy(x => x)).
				// Сортировка нужна для стабильного порядка отображения (необязательно, но удобно).
				foreach (var dialogKey in dialogs.OrderBy(x => x))
				{
					// Добавляем каждый ключ диалога в список.
					dialogsList.Add(dialogKey);
				}
			});
		}

		// Обработчик события выбора элемента в списке диалогов (DialogsListView).
		// Вызывается, когда администратор кликает по другому элементу в левой панели.
		private void DialogsListView_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
		{
			// Проверяем, что выбранный элемент является строкой (в нашем списке все элементы – строки).
			// sender – это сам ListBox (не используется), e – аргументы события (содержат информацию о выборе).
			if (DialogsListView.SelectedItem is string selected)
			{
				// === 1. Сохраняем выбранный ключ диалога ===
				// selectedDialogKey – поле класса, запоминающее, какой диалог сейчас открыт.
				selectedDialogKey = selected;

				// === 2. Обновляем отображение сообщений в правой области ===
				// RefreshMessages() очищает коллекцию currentMessages и заполняет её сообщениями,
				// соответствующими выбранному диалогу (общий чат или приватный).
				RefreshMessages();

				// === 3. Активируем поле ввода сообщения и кнопку отправки ===
				// Администратор может писать сообщения только когда выбран какой-либо диалог.
				MessageTextBox.IsEnabled = true;
				SendButton.IsEnabled = true;
			}
			else
			{
				// Если выбор сброшен (например, список пуст или выбранный элемент не строка), деактивируем ввод.
				MessageTextBox.IsEnabled = false;
				SendButton.IsEnabled = false;
			}
		}

		// Обновляет отображение сообщений в правой области чата в соответствии с выбранным диалогом.
		// Вызывается при выборе другого диалога (DialogsListView_SelectionChanged) или при изменении содержимого диалога (например, при получении нового сообщения, если этот диалог открыт).
		private void RefreshMessages()
		{
			// === 1. Очищаем текущий список сообщений ===
			// currentMessages – ObservableCollection<ChatMessage>, привязанная к MessagesItemsControl.
			// Очистка удаляет все старые сообщения, чтобы затем заполнить новыми (соответствующими выбранному диалогу).
			currentMessages.Clear();

			// === 2. Определяем, какой диалог выбран ===
			// selectedDialogKey – строка, хранящая ключ текущего диалога (устанавливается в DialogsListView_SelectionChanged).
			if (selectedDialogKey == "Общий чат")
			{
				// === 2a. Выбран общий чат ===
				// Получаем все сообщения общего чата из сервера (коллекция broadcastMessages).
				foreach (var msg in server.GetBroadcastMessages())
				{
					// Добавляем каждое сообщение в currentMessages.
					// Благодаря тому, что currentMessages – ObservableCollection, UI автоматически обновляется.
					currentMessages.Add(msg);
				}
			}
			else
			{
				// === 2b. Выбран приватный диалог ===
				// Ключ приватного диалога имеет формат "ник1|ник2" (отсортированная пара).
				var parts = selectedDialogKey.Split('|');
				if (parts.Length == 2)   // Проверка корректности формата
				{
					// Получаем историю сообщений для этой пары пользователей из сервера.
					// Метод GetDialogMessages(parts[0], parts[1]) возвращает ObservableCollection<ChatMessage> или null, если диалог не существует.
					var messages = server.GetDialogMessages(parts[0], parts[1]);
					if (messages != null)
					{
						// Добавляем все сообщения из истории в currentMessages.
						foreach (var msg in messages)
							currentMessages.Add(msg);
					}
				}
			}

			// === 3. Автоматическая прокрутка чата вниз ===
			// После добавления сообщений вызываем метод ScrollToBottom(), который прокручивает ScrollViewer к самому низу,
			// чтобы последнее сообщение было видно.
			ScrollToBottom();
		}

		// Обработчик нажатия кнопки "Отправить" в окне сервера чата.
		// Асинхронный (async), так как вызывает методы отправки сообщений, которые могут выполнять асинхронные сетевые операции.
		private async void SendButton_Click(object sender, RoutedEventArgs e)
		{
			// === 1. Проверка: есть ли текст и выбран ли диалог ===
			// MessageTextBox – поле ввода текста сообщения администратором.
			// selectedDialogKey – ключ текущего выбранного диалога (общий чат или приватный диалог).
			// Если текст пуст или диалог не выбран, ничего не делаем.
			if (string.IsNullOrWhiteSpace(MessageTextBox.Text) || selectedDialogKey == null)        //https://learn.microsoft.com/ru-ru/dotnet/api/system.string.isnullorwhitespace?view=net-10.0
				return;

			// Удаляем лишние пробелы в начале и конце.
			string text = MessageTextBox.Text.Trim();

			// === 2. Определяем, куда отправлять сообщение ===
			if (selectedDialogKey == "Общий чат")
			{
				// Если выбран общий чат, отправляем сообщение всем клиентам через метод SendAdminMessageToAll.
				// Текст дополняется префиксом "[Админ]", чтобы получатели знали, что сообщение от администратора.
				await server.SendAdminMessageToAll($"[Админ] {text}");
			}
			else
			{
				// Если выбран приватный диалог, ключ имеет формат "ник1|ник2".
				// Разделяем строку по символу '|', чтобы получить оба ника.
				var parts = selectedDialogKey.Split('|');
				if (parts.Length == 2)   // Проверка корректности формата
				{
					// Отправляем сообщение в приватный диалог между двумя пользователями.
					// Метод SendAdminMessageToDialog отправляет сообщение обоим участникам диалога.
					await server.SendAdminMessageToDialog(parts[0], parts[1], text);
				}
			}

			// === 3. Очищаем поле ввода ===
			// После отправки сообщения поле ввода очищается, чтобы администратор мог написать следующее сообщение.
			MessageTextBox.Clear();
		}

		// Метод для прокрутки области сообщений вниз (к последнему сообщению).
		private void ScrollToBottom()
		{
			// Используем Dispatcher.BeginInvoke, чтобы отложить прокрутку до завершения отрисовки UI.
			// Это гарантирует, что все добавленные сообщения уже отображены, и ScrollViewer может корректно рассчитать свою высоту.
			Dispatcher.BeginInvoke(new Action(() =>             //https://learn.microsoft.com/ru-ru/dotnet/api/system.windows.threading.dispatcher.begininvoke?view=windowsdesktop-10.0
			{
				// MessagesScrollViewer – имя ScrollViewer, который содержит ItemsControl.
				// Проверяем, что прокрутка возможна (ScrollableHeight > 0).
				if (MessagesScrollViewer.ScrollableHeight > 0)
				{
					// Прокручиваем в самый конец.
					MessagesScrollViewer.ScrollToEnd();
				}
			}));
		}

		// Метод для вывода отладочной информации в окно Output Visual Studio (или в консоль отладчика).
		private void AppendLog(string text) => System.Diagnostics.Debug.WriteLine(text);


		protected override void OnClosing(CancelEventArgs e)
		{
			server?.Stop();
			base.OnClosing(e);
		}
	}
}