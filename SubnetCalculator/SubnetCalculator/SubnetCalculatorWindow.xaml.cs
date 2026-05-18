using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Collections.Specialized;


namespace SubnetCalculator
{
	public partial class SubnetCalculatorWindow : Window
	{
		private HelpWindow currentHelpWindow;
		private List<string> defaultIP = new List<string>();
		public SubnetCalculatorWindow()
		{
			InitializeComponent();
			foreach (ComboBoxItem item in cmbIP.Items)		//Перебор всех элементов из списка стандартных айпи адресов
			{
				if (item?.Content != null)					//Если содержимое не ноль, тогда добавляет элемент в список
					defaultIP.Add(item.Content.ToString());
			}
			LoadRecentIP();									//Вызов метода сохранённых айпи-адресов
			ResetFields();									//Очистка поля ввода
		}
		//====================================================================Метод очистки полей ввода=========================================================================================//
		private void ResetFields()
		{
			cmbIP.Text = "";
			txtMask.Text = "";
			txtNetwork.Text = "Адрес сети: ";
			txtBroadcast.Text = "Широковещательный адрес: ";
			txtHostRange.Text = "Диапазон узлов: ";
			txtIPBinary.Text = "IP в двоичном виде:";
			txtMaskBinary.Text = "Маска в двоичном виде:";
			txtError.Text = "";

			sliderPrefix.Value = 0;
			txtPrefixValue.Text = "0";
		}
		//===================================================================Метод загрузки коллекции IP-адресов================================================================================//
		private void LoadRecentIP()
		{
			StringCollection recent = Properties.Settings.Default.RecentIP; //Извлекаю коллекцию строк из сохранённых IP-адресов внутри XAMl-развёртки
			if (recent == null || recent.Count == 0) return;				//Если коллекции нет - выход из метода
			foreach (string ip in recent)									//Перебор всех айпишников
			{
				if (string.IsNullOrEmpty(ip)) continue;						//Пропускаю пустые строки
				if (!IsIPInComboBox(ip))									//Проверка на наличие данного айпишника в коллекции, и если нет, то добавляю в комбобокс
				{
					AddIPToComboBox(ip, isUserAdded: true);
				}
			}
		}
		//===================================================================Метод сохранения коллекции IP-адресов==============================================================================//
		private void SaveRecentIP(string ip)
		{
			if (string.IsNullOrEmpty(ip)) return;                                   //Если переданная строка пустая - пропускаю...
			StringCollection recent = Properties.Settings.Default.RecentIP;         //Получаю коллекцию из настроек(settings.settings)
			if (recent == null)														//Если коллекция пустая - создаю новую
				recent = new System.Collections.Specialized.StringCollection();
			if (recent.Contains(ip))												//Если такой адрес уже был, то удаляю его
				recent.Remove(ip);
			recent.Insert(0, ip);													//Вставляю новый адрес в начало списка дополнительной(введённой пользователем) коллекции
			while (recent.Count > 5)												//Ограничиваю дополнительную коллекцию 5 адресами
				recent.RemoveAt(recent.Count - 1);
			Properties.Settings.Default.RecentIP = recent;							//Сохраняю обновлённую коллекцию и вызываю метод сохранения, чтобы данные остались
			Properties.Settings.Default.Save();

			if (!IsIPInComboBox(ip))												//Если айпишник не отображается в списке - добавляю его туда как юзерский, делаю красного цвета и с обводкой
			{
				AddIPToComboBox(ip, isUserAdded: true);
			}
		}
		//===============================================================Метод проверки на наличие введённого айпи в коллекции==================================================================//
		private bool IsIPInComboBox(string ip)
		{
			foreach (ComboBoxItem item in cmbIP.Items)			//Проход по элементам коллекции
			{
				if (item.Content.ToString() == ip)return true;
			}
			return false;
		}
		//==================================================Метод добвления юзерских айпишников в коллецию с особыми условиями==================================================================//
		private void AddIPToComboBox(string ip, bool isUserAdded)
		{
			if (string.IsNullOrEmpty(ip) || cmbIP == null) return;
			ComboBoxItem newItem = new ComboBoxItem();								//Создаю новый элемент списка и устанавливаю его содержимое
			newItem.Content = ip;
			if (isUserAdded)														//Если это пользовательский адрес, то задаю ему тёмно-красный цвет и жирный шрифт
			{
				newItem.Foreground = System.Windows.Media.Brushes.DarkRed;
				newItem.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(80, 255, 0, 0));
				newItem.FontWeight = FontWeights.Bold;
			}
			cmbIP.Items?.Add(newItem);												//Если не ноль, добавляю элемент в список
		}
		//=============================================Обработчик кнопки удаления истории добавленных айпи адресов==============================================================================//
		private void BtnClearHistory_Click(object sender, RoutedEventArgs e)
		{
			Properties.Settings.Default.RecentIP = new System.Collections.Specialized.StringCollection();				//Сбрасываю коллекцию айпишников
			Properties.Settings.Default.Save();																			//Вызываю метод сохранения

			if (cmbIP?.Items != null)																					//Создаю список элементов, которые нужно удалить
			{
				List<ComboBoxItem> toRemove = new List<ComboBoxItem>();
				foreach (ComboBoxItem item in cmbIP.Items)																//Прохожу по элементам комбобокса
				{
					if (item?.Content != null && !defaultIP.Contains(item.Content.ToString()))							//Если элемента нет в списке дефолтных айпишников, добавляю его в ремув
					{
						toRemove.Add(item);
					}
				}
				foreach (ComboBoxItem item in toRemove)																	//Удаляю все пользовательские айпишники
				{
					cmbIP.Items.Remove(item);
				}
			}

			cmbIP.Text = "";																							//Очищаю текстовое поле
		}
		//======================================================================================================================================================================================//
		private void TxtMask_TextChanged(object sender, TextChangedEventArgs e)
		{
			if (string.IsNullOrWhiteSpace(txtMask.Text)) return;				//Если поле маски пустое или пробелы - выход
			if (IPAddress.TryParse(txtMask.Text, out IPAddress maskIp))			//Разибраю строку в формате Ip-адреса
			{
				int mask = IpToInt(maskIp);                                     //Если подходит, то преобразую int в IpToInt, далее вычисляю по ней префикс
				if (IsValidMask(mask))											
				{
					int prefix = CountPrefixBits(mask);
					sliderPrefix.Value = prefix;
					txtError.Text = "";
				}
				else															//Если не подходит, то вывожу ошибку
				{
					txtError.Text = "Ошибка: маска должна содержать непрерывные единицы (например, 255.255.255.0)";
				}
			}
			else																//Или если формат неверный, то тоже вывожу ошибку
			{
				txtError.Text = "Неверный формат маски. Используйте десятичный вид (255.255.255.0)";
			}
		}
		//======================================================================Обработчик слайдера(ползунка)===================================================================================//
		private void SliderPrefix_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
		{
			int prefix = (int)sliderPrefix.Value;			//Получаю значение из "ползунка"
			txtPrefixValue.Text = prefix.ToString();		//Отображаю в текстовом блоке
			int mask = PrefixToMask(prefix);				
			txtMask.Text = IntToIp(mask);
		}
		//=========================================================Обработчик кнопки "Решить" (основной расчёт)=================================================================================//
		private void BtnCalc_Click(object sender, RoutedEventArgs e)
		{
			txtError.Text = "";
			string ipText = cmbIP.Text.Trim();
			SaveRecentIP(ipText);                                               //Сохраняю введённый IP в историю

			if (!IPAddress.TryParse(ipText, out IPAddress ip))
			{
				txtError.Text = "Неверный IP-адрес (пример: 192.168.1.1)";
				return;
			}
			if (!IPAddress.TryParse(txtMask.Text, out IPAddress maskIp))
			{
				txtError.Text = "Неверная маска (пример: 255.255.255.0)";
				return;
			}

			int ipInt = IpToInt(ip);
			int maskInt = IpToInt(maskIp);
			if (!IsValidMask(maskInt))
			{
				txtError.Text = "Маска должна содержать непрерывные единицы от старшего бита.";
				return;
			}

			int network = ipInt & maskInt;                                      //Адрес сети
			int broadcast = network | ~maskInt;                                 //Широковещательный адрес
			int firstHost = network + 1;                                        //Первый узел
			int lastHost = broadcast - 1;                                       //Последний узел
			int prefix = CountPrefixBits(maskInt);                              //Префикс

			//Блок кода с отображением результатов:
			txtNetwork.Text = $"Адрес сети: {IntToIp(network)}";					
			txtBroadcast.Text = $"Широковещательный адрес: {IntToIp(broadcast)}";

			if (firstHost <= lastHost && prefix < 31)
				txtHostRange.Text = $"Диапазон узлов: {IntToIp(firstHost)} — {IntToIp(lastHost)}";
			else if (prefix == 31)
				txtHostRange.Text = $"Диапазон узлов: {IntToIp(network)} — {IntToIp(broadcast)} (точка-точка, /31)";
			else if (prefix == 32)
				txtHostRange.Text = "Нет узлов (одиночный хост)";
			else
				txtHostRange.Text = "Диапазон узлов: (нет свободных)";

			txtIPBinary.Text = $"IP в двоичном виде:\n{FormatBinary(ipInt)}";
			txtMaskBinary.Text = $"Маска в двоичном виде:\n{FormatBinary(maskInt)}";
		}

		//=========================================================Преобразование IPAddress в int================================================================================================//
		private int IpToInt(IPAddress ip)
		{
			byte[] bytes = ip.GetAddressBytes();                                //ip.GetAddressBytes() возвращает массив из 4 байт в сетевом порядке (старший байт — первый элемент)
			if (BitConverter.IsLittleEndian) Array.Reverse(bytes);              //Учитываю порядок байтов(проверяет архитектуру процессора: в little-endian младший байт числа хранится по меньшему адресу)
			//массив переворачивается (Array.Reverse), чтобы привести его к порядку, ожидаемому BitConverter.ToInt32. Для little-endian метод ожидает массив, где младший байт идёт первым. Переворот превращает [192,168,1,1] в [1,1,168,192]
			//на little-endian младший байт идёт первым, поэтому [1,1,168,192] означает 0xC0A80101 (192.168.1.1). В итоге число соответствует IP в порядке хост (не сетевой)
			//https://learn.microsoft.com/ru-ru/dotnet/api/system.bitconverter.islittleendian?view=netframework-4.8.1&viewFallbackFrom=net-9.0
			return BitConverter.ToInt32(bytes, 0);                              //читает 4 байта и возвращает int
		}
		//=========================================================Преобразование int в строку IP================================================================================================//
		private string IntToIp(int ip)
		{
			//обратное преобразование — из целого числа в строку IP вида "192.168.1.1"
			byte[] bytes = BitConverter.GetBytes(ip);
			if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
			return new IPAddress(bytes).ToString();     //создаёт объект IPAddress из байтов и преобразует в строку
		}

		//=========================================================Проверка маски на непрерывность===============================================================================================//
		private bool IsValidMask(int mask)
		{
			//проверяет, является ли маска корректной (непрерывные единицы слева, затем нули)
			return mask == -1 || ((~mask + 1) & (~mask)) == 0; //mask == -1 — это маска 0xFFFFFFFF (все 32 бита единицы, префикс 32)
			//(~mask + 1) — операция, выделяющая младший установленный бит. Если единицы не образуют непрерывный блок в конце, то (~mask + 1) имеет единицу там, где в ~mask её нет, и И даст ненулевое значение.
			//https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/bitwise-and-shift-operators
		}

		//=========================================================Подсчёт количества единиц в маске (префикс)==================================================================================//
		private int CountPrefixBits(int mask)
		{
			int count = 0;
			for (int i = 31; i >= 0; i--)                                       //Иду от старшего бита
			{
				if ((mask & (1 << i)) != 0) count++;                            //Если бит = 1, увеличиваем счётчик
				else break;                                                     //Как встретили 0, выход с цикла
			}
			return count;														//По итогу count - число единиц до первого нуля
		}

		//=========================================================Получение маски по префиксу==========================================//
		private int PrefixToMask(int prefix)
		{
			if (prefix == 0) return 0;
			long maskLong = (0xFFFFFFFFL) << (32 - prefix);                     //0xFFFFFFFFL – это 32 единицы в 64-битном беззнаковом long,
			//Сдвиг влево на (32 - prefix) битов – в результате младшие биты заполняются нулями
			//Затем результат приводится к int
			return (int)maskLong;
		}
		//=========================================================Форматирование числа в двоичную строку с точками========================//
		private string FormatBinary(int value)
		{
			long v = value;														//Копирую int в long, чтобы избежать проблем со знаком при сдвигах вправо.
			StringBuilder sb = new StringBuilder();
			for (int i = 3; i >= 0; i--)                                        //4 октета
			{
				byte octet = (byte)((v >> (i * 8)) & 0xFF);                     //Выделяем очередной байт(сдвиг вправо на нужное количество бит, чтобы нужный октет оказался в младших 8 битах)
				sb.Append(Convert.ToString(octet, 2).PadLeft(8, '0'));          //Дополняем до 8 бит(Convert.ToString(octet, 2) – преобразование в двоичную строку, PadLeft(8, '0') – дополнение нулями слева до 8 символов)
				if (i > 0) sb.Append('.');                                      //Разделитель между октетами(Если октет не последний)
			}
			return sb.ToString();
		}
		//=========================================================Обработчик кнопки "Сброс"====================================================================================================//
		private void BtnReset_Click(object sender, RoutedEventArgs e)
		{
			ResetFields();                                                      //Очищаю поля
			BtnCalc_Click(sender, e);                                           //Пересчитываю
		}
		//=========================================================Определение класса IP-адреса (A/B/C)=================================//
		private char GetIPClass(int ip)
		{
			byte firstOctet = (byte)((ip >> 24) & 0xFF);          //Первый октет(ip >> 24 – сдвигаем 32-битное число на 24 бита вправо. Старший (самый левый) байт, содержащий первый октет, перемещается в младшие 8 бит.)
			//& 0xFF – побитовая маска, оставляющая только последние 8 бит (отбрасывает всё, что выше). Результат – число от 0 до 255
			//Приведение к byte – сохраняет значение как беззнаковый байт
			if (firstOctet >= 1	  && firstOctet <= 126) return 'A';       //Диапазон: от 1 до 126 (включительно) и далее разибваю по диапзаонам классов
			if (firstOctet >= 128 && firstOctet <= 191) return 'B';
			if (firstOctet >= 192 && firstOctet <= 223) return 'C';
			return '?';													//Классы D и E без особого внимания
		}
		//=========================================================Обработчик кнопки "Класс сети"========================================//
		private void BtnClassify_Click(object sender, RoutedEventArgs e)
		{
			txtError.Text = "";													//Очистка поля ошибок
			if (string.IsNullOrWhiteSpace(cmbIP.Text))							//Если ничего не введено, выводим ошибку
			{
				txtError.Text = "Сначала введите IP-адрес!";
				return;
			}
			if (!IPAddress.TryParse(cmbIP.Text, out IPAddress ip))              //Разбираем строку как IP-адрес
																				//https://learn.microsoft.com/en-us/dotnet/api/system.net.ipaddress.tryparse?view=netframework-4.8.1
			{
				txtError.Text = "Неверный IP-адрес (пример: 192.168.1.1)";
				return;
			}

			//Преобразование в 32-бит
			int ipInt = IpToInt(ip);
			char ipClass = GetIPClass(ipInt);

			//Определяю класс и формирую текстовое описание для него
			string description = "", range = "";
			switch (ipClass)
			{
				case 'A':
					description = "Класс A — для очень крупных сетей.\nПервый бит всегда 0, первый октет от 1 до 126.";
					range = "Диапазон: 1.0.0.0 — 126.255.255.255\nСтандартная маска: 255.0.0.0 (/8)";
					break;
				case 'B':
					description = "Класс B — для средних сетей.\nПервые два бита 10, первый октет от 128 до 191.";
					range = "Диапазон: 128.0.0.0 — 191.255.255.255\nСтандартная маска: 255.255.0.0 (/16)";
					break;
				case 'C':
					description = "Класс C — для небольших сетей.\nПервые три бита 110, первый октет от 192 до 223.";
					range = "Диапазон: 192.0.0.0 — 223.255.255.255\nСтандартная маска: 255.255.255.0 (/24)";
					break;
				default:
					description = "Адрес не относится к классам A, B или C.\nЭто класс D (многоадресная рассылка) или E (экспериментальный).";
					range = "Класс D: 224.0.0.0 — 239.255.255.255\nКласс E: 240.0.0.0 — 255.255.255.255";
					break;
			}

			WindowResult resultWindow = new WindowResult(ipClass, description, range);	//Передаю тип класса, описание и диапазон
			resultWindow.Owner = this;													//Чтобы окно было дочерним по отношению к текущему
			resultWindow.ShowDialog();
		}

		//=========================================================Обработчик клика по элементам со справкой (всплывающее окно)=================================================================//
		private void HelpLabel_Click(object sender, RoutedEventArgs e)
		{
			e.Handled = true;                                                   //Останавливаю всплытие(чтобы справка не открывалась дважды)
			FrameworkElement source = sender as FrameworkElement;               //привожу sender к типу FrameworkElement(нужно для tag, cursor и других)
			//https://learn.microsoft.com/ru-ru/dotnet/api/system.windows.frameworkelement?view=netframework-4.8.1&viewFallbackFrom=windowsdesktop-10.0
			if (source == null) return;

			string topic = source.Tag?.ToString();                              //свойство Tag - универсальный контейнер для хранения любых пользовательских данных
			if (string.IsNullOrEmpty(topic)) return;

			if (currentHelpWindow != null && currentHelpWindow.IsVisible)       //Если окно уже открыто, не создаю новое
			{
				currentHelpWindow.Activate();
				return;
			}

			string content = GetHelpContent(topic);								//вызываю метод, который возвращает текст справки
			currentHelpWindow = new HelpWindow("Справка", content);				//Создаю новый экземпляр окна HelpWindow(справки)
			currentHelpWindow.Owner = this;										//Текущее окно - владелец
			currentHelpWindow.Closed += (s, args) => currentHelpWindow = null;	//Подписываюсь на событие закрытия окна, при закрытии обнуляется поле
			currentHelpWindow.ShowDialog();
		}

		//=========================================================Возврат текста справки по теме===============================================================================================//
		private string GetHelpContent(string topic)
		{
			switch (topic)
			{
				case "IP": 
					return "IP-адрес (Internet Protocol address) – уникальный идентификатор устройства в сети." +
						"\n\nФормат IPv4: четыре числа от 0 до 255, разделённые точками (например, 192.168.1.1)." +
						"\n\nВ данном калькуляторе вы можете ввести любой корректный IPv4-адрес.";
				case "Mask": 
					return "Маска подсети определяет, какая часть IP-адреса относится к сети, а какая – к узлу." +
						"\n\nОна представляет собой 32-битное число, в двоичном виде состоящее из последовательности единиц (сеть) и нулей (узлы)." +
						"\n\nПример правильной маски: 255.255.255.0 (двоично: 11111111.11111111.11111111.00000000)." +
						"\n\nМаска должна быть непрерывной: нельзя, чтобы после нуля снова шли единицы.";
				case "Prefix": 
					return "Префикс (CIDR) – количество единиц в маске подсети. Обозначается косой чертой после IP-адреса." +
						"\n\nНапример, /24 соответствует маске 255.255.255.0, /16 – 255.255.0.0." +
						"\n\nПрефикс может быть от 0 до 32. Чем больше префикс, тем меньше узлов в сети.";
				case "DecimalMask":
					return "Десятичный вид маски подсети — это привычное представление в виде четырёх чисел от 0 до 255, разделённых точками." +
						"\n\nПримеры правильных масок:\n- 255.255.255.0 (префикс /24)\n- 255.255.0.0 (/16)\n- 255.255.254.0 (/23)" +
						"\n\nВажно: маска должна содержать непрерывные единицы, начиная со старшего бита." +
						"\nНеправильная маска: 255.100.255.0 — в ней после нуля во втором октете снова идут единицы." +
						"\n\nВы можете ввести маску в это поле, и программа автоматически определит префикс и обновит ползунок.";
				case "Network": 
					return "Адрес сети – это IP-адрес, в котором все биты узла обнулены." +
						"\n\nОн получается в результате побитового AND между IP-адресом и маской подсети." +
						"\n\nАдрес сети нельзя назначать устройству, он используется для идентификации всей подсети.";
				case "Broadcast": 
					return "Широковещательный адрес – специальный адрес, предназначенный для отправки пакетов всем устройствам в данной сети." +
						"\n\nВычисляется как адрес сети, в котором все биты узла установлены в единицу." +
						"\n\nПакет, отправленный на широковещательный адрес, получают все узлы в подсети.";
				case "HostRange": 
					return "Диапазон узлов – это все доступные IP-адреса в подсети, которые могут быть назначены устройствам." +
						"\n\nПервый адрес диапазона – это адрес сети + 1, последний – широковещательный адрес - 1." +
						"\n\nНапример, для сети 192.168.1.0/24 диапазон узлов: 192.168.1.1 – 192.168.1.254.";
				case "BinaryIP": 
					return "Двоичное представление IP-адреса — это запись всех 32 бит адреса в виде четырёх групп по 8 бит (октетов), разделённых точками." +
						"\n\nКаждый октет переводится из десятичного числа в двоичную систему (от 00000000 до 11111111)." +
						"\n\nНапример, IP 192.168.1.1 в двоичном виде: 11000000.10101000.00000001.00000001." +
						"\n\nДвоичный вид помогает понять, как маска разделяет адрес на сетевую и узловую части. " +
						"Бит, стоящий на позиции, где в маске 1, относится к сети; где 0 — к узлу.";
				case "BinaryMask": 
					return "Двоичное представление маски подсети — это 32 бита, состоящие из непрерывной последовательности единиц (слева) и нулей (справа)." +
						"\n\nПример: маска 255.255.248.0 (/21) в двоичном виде: 11111111.11111111.11111000.00000000." +
						"\n\nЕдиницы указывают на сетевую часть адреса, нули — на узловую. Количество единиц равно префиксу (CIDR)." +
						"\n\nДвоичный вид маски критичен для понимания подсетей: побитовое И между IP и маской даёт адрес сети.";
				case "Results": 
					return "В этом блоке отображаются основные вычисленные параметры подсети: адрес сети, широковещательный адрес и диапазон доступных узлов." +
						"\n\nНиже находится выпадающий список с подробным объяснением каждого из расчётов. " +
						"Выберите интересующий пункт, и под списком появится понятное описание с формулами и примерами.";
				default: 
					return "Нет подробной информации по этому элементу.";
			}
		}

		//=========================================================Обработчик выбора пункта в комбобоксе "Подробнее о расчёте"==================================================================//
		private void CmbCalcHelp_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (txtCalcDescription == null) return;									//Защита от несозданных элементов
			ComboBoxItem selectedItem = cmbCalcHelp.SelectedItem as ComboBoxItem;	//Возвращаю выбранный элемент как object, если нет, то ноль
			if (selectedItem == null) return;
			string topic = selectedItem.Content.ToString();							//Преобразую строку заголовка в тему описания
			txtCalcDescription.Text = GetCalculationDescription(topic);          //Подставляем описание
		}
		//=========================================================Возврат подробного описания выбранного расчёта===============================================================================//
		private string GetCalculationDescription(string topic)
		{
			switch (topic)
			{
				case "Адрес сети":
					return "Адрес сети получается в результате поразрядной логической операции AND между IP-адресом и маской подсети." +
						"\n\nФормула: Сеть = IP & Маска\n\nВ двоичном виде все биты узла обнуляются, остаётся только сетевая часть. " +
						"Адрес сети не может быть присвоен устройству — он служит для идентификации всей подсети." +
						"\n\nПример: IP 192.168.1.1, маска 255.255.255.0 -> адрес сети 192.168.1.0.";
				case "Широковещательный адрес":
					return "Широковещательный адрес (broadcast) — это адрес, по которому пакет получают все устройства в данной подсети." +
						"\n\nФормула: Broadcast = Сеть | (~Маска)\n\nТо есть берётся адрес сети, и все биты узла устанавливаются в 1. Этот адрес также нельзя назначать хосту." +
						"\n\nПример: для сети 192.168.1.0/24 широковещательный адрес — 192.168.1.255.";
				case "Диапазон узлов":
					return "Диапазон узлов (или диапазон хостов) — это все IP-адреса, которые можно присвоить устройствам в сети." +
						"\n\nПервый адрес: адрес сети + 1\nПоследний адрес: широковещательный адрес - 1" +
						"\n\nКоличество доступных узлов: 2^(32-префикс) - 2 (для масок /31 и /32 есть исключения)." +
						"\n\nПример: сеть 192.168.1.0/24 -> узлы от 192.168.1.1 до 192.168.1.254.";
				case "IP в двоичном виде":
					return "IP-адрес переводится в двоичную систему счисления для наглядного представления его 32-битной структуры." +
						"\n\nКаждый октет (число от 0 до 255) преобразуется в 8-битное двоичное число, затем октеты объединяются через точку." +
						"\n\nДвоичный вид помогает понять, какие биты относятся к сети, а какие — к узлу (сравнивая с маской)." +
						"\n\nПример: 192.168.1.1 -> 11000000.10101000.00000001.00000001.";
				case "Маска в двоичном виде":
					return "Маска подсети в двоичном виде всегда состоит из непрерывной последовательности единиц (слева) и нулей (справа)." +
						"\n\nКоличество единиц равно префиксу (CIDR). Например, маска 255.255.248.0 в двоичном виде: 11111111.11111111.11111000.00000000 — здесь 21 единица, т.е. префикс /21." +
						"\n\nДвоичная маска используется для поразрядного умножения с IP-адресом (операция AND) и получения адреса сети.";
				case "Префикс (CIDR)":
					return "Префикс (CIDR — Classless Inter-Domain Routing) — это количество единиц в маске подсети, записываемое через косую черту после IP-адреса." +
						"\n\nОн определяет длину сетевой части адреса. Чем больше префикс, тем меньше узлов может быть в сети." +
						"\n\nПримеры:\n- /24 -> маска 255.255.255.0 -> 254 узла\n- /16 -> маска 255.255.0.0 -> 65534 узла\n- /30 -> маска 255.255.255.252 → 2 узла (для точечных соединений)." +
						"\n\nПрефикс можно задать ползунком или вручную в поле «Префикс».";
				default:
					return "Выберите тему из списка, чтобы увидеть подробное описание расчёта.";
			}
		}

		//=========================================================Обработчик кнопки "Подробное решение"========================================================================================//
		private void BtnDetailedSolution_Click(object sender, RoutedEventArgs e)
		{
			// Проверяю, что IP и маска введены корректно
			if (!IPAddress.TryParse(cmbIP.Text, out IPAddress ip))
			{
				txtError.Text = "Сначала введите корректный IP-адрес и нажмите «Решить».";
				return;
			}
			if (!IPAddress.TryParse(txtMask.Text, out IPAddress maskIp))
			{
				txtError.Text = "Сначала введите корректную маску и нажмите «Решить».";
				return;
			}

			int ipInt = IpToInt(ip);		//Преобразую объекты IP-адреса в 32-битные числа, и проверяю на непрерывность
			int maskInt = IpToInt(maskIp);
			if (!IsValidMask(maskInt))
			{
				txtError.Text = "Маска невалидна. Исправьте маску.";
				return;
			}

			// Повторяю вычисления, чтобы получить все параметры(это сделано для надёжности - окно подробного решения может быть открыто даже после того,
			// как пользователь изменил IP или маску, но не нажал «Решить»)
			int network = ipInt & maskInt;
			int broadcast = network | ~maskInt;
			int firstHost = network + 1;
			int lastHost = broadcast - 1;
			int prefix = CountPrefixBits(maskInt);

			// форматирую IP, маску, адрес сети, широковещательный адрес в двоичный вид
			string ipBinary = FormatBinary(ipInt);
			string maskBinary = FormatBinary(maskInt);
			string networkBinary = FormatBinary(network);
			string broadcastBinary = FormatBinary(broadcast);

			// Преобразую вычисленные целые числа обратно в строковые IP-адреса для пользователя
			string networkIp = IntToIp(network);
			string broadcastIp = IntToIp(broadcast);
			string firstHostIp = IntToIp(firstHost);
			string lastHostIp = IntToIp(lastHost);

			string hostCount = (prefix < 31) ? (Math.Pow(2, 32 - prefix) - 2).ToString() : (prefix == 31 ? "2" : "1"); //Тернарником определяю количество узлов в сети
			//Если Prefix < 21, то по дефолтной формуле иду (Math.Pow(2, 32 - prefix) - 2), Если prefix == 31: маска /31 (точка-точка) – 2 узла, без широковещательного адреса
			//Иначе - prefix == 32: маска /32 – только один адрес (одиночный хост)

			DetailedSolutionWindow solutionWindow = new DetailedSolutionWindow			//Создаю экземпляр окна и передаю все данные ему
				(
				cmbIP.Text, txtMask.Text, prefix,
				ipBinary, maskBinary,
				networkBinary, networkIp,
				broadcastBinary, broadcastIp,
				firstHostIp, lastHostIp, hostCount
				);
			solutionWindow.Owner = this;												//Делаю окно модальным(нельзя открыть поверх него ещё одно)
			solutionWindow.ShowDialog();
		}
	}
}