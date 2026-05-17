using System;
using System.Net;
using System.Text;
using System.Windows;
using System.Windows.Controls;


namespace SubnetCalculator
{
	public partial class MainWindow : Window
	{
		private HelpWindow currentHelpWindow;
		public MainWindow()
		{
			InitializeComponent();
			LoadRecentIP();
			ResetFields();
		}

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

		private void LoadRecentIP()
		{
			var recent = Properties.Settings.Default.RecentIP;
			if (recent != null)
			{
				foreach (string ip in recent)
				{
					if (!string.IsNullOrEmpty(ip) && !cmbIP.Items.Contains(ip))
						cmbIP.Items.Add(ip);
				}
			}
		}
		private void SaveRecentIP(string ip)
		{
			if (string.IsNullOrEmpty(ip)) return;
			var recent = Properties.Settings.Default.RecentIP ?? new System.Collections.Specialized.StringCollection();
			if (recent.Contains(ip))
				recent.Remove(ip);
			recent.Insert(0, ip);
			while (recent.Count > 5)
				recent.RemoveAt(recent.Count - 1);
			Properties.Settings.Default.RecentIP = recent;
			Properties.Settings.Default.Save();
		}
		private void TxtMask_TextChanged(object sender, TextChangedEventArgs e)
		{
			if (string.IsNullOrWhiteSpace(txtMask.Text)) return;
			if (IPAddress.TryParse(txtMask.Text, out IPAddress maskIp))
			{
				int mask = IpToInt(maskIp);
				if (IsValidMask(mask))
				{
					int prefix = CountPrefixBits(mask);
					sliderPrefix.Value = prefix;
					txtError.Text = "";
				}
				else
				{
					txtError.Text = "Ошибка: маска должна содержать непрерывные единицы (например, 255.255.255.0)";
				}
			}
			else
			{
				txtError.Text = "Неверный формат маски. Используйте десятичный вид (255.255.255.0)";
			}
		}

		private void SliderPrefix_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
		{
			int prefix = (int)sliderPrefix.Value;
			txtPrefixValue.Text = prefix.ToString();
			int mask = PrefixToMask(prefix);
			txtMask.Text = IntToIp(mask);
		}

		private void BtnCalc_Click(object sender, RoutedEventArgs e)
		{
			txtError.Text = "";
			string ipText = cmbIP.Text.Trim();

			SaveRecentIP(ipText);

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

			int network = ipInt & maskInt;
			int broadcast = network | ~maskInt;
			int firstHost = network + 1;
			int lastHost = broadcast - 1;
			int prefix = CountPrefixBits(maskInt);

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

		private int IpToInt(IPAddress ip)
		{
			byte[] bytes = ip.GetAddressBytes();
			if (BitConverter.IsLittleEndian)
				Array.Reverse(bytes);
			return BitConverter.ToInt32(bytes, 0);
		}

		private string IntToIp(int ip)
		{
			byte[] bytes = BitConverter.GetBytes(ip);
			if (BitConverter.IsLittleEndian)
				Array.Reverse(bytes);
			return new IPAddress(bytes).ToString();
		}

		private bool IsValidMask(int mask)
		{
			return mask == -1 || ((~mask + 1) & (~mask)) == 0;
		}

		private int CountPrefixBits(int mask)
		{
			int count = 0;
			for (int i = 31; i >= 0; i--)
			{
				if ((mask & (1 << i)) != 0)
					count++;
				else
					break;
			}
			return count;
		}

		private int PrefixToMask(int prefix)
		{
			if (prefix == 0) return 0;
			long maskLong = (0xFFFFFFFFL) << (32 - prefix);
			return (int)maskLong;
		}

		private string FormatBinary(int value)
		{
			long v = value;
			StringBuilder sb = new StringBuilder();
			for (int i = 3; i >= 0; i--)
			{
				byte octet = (byte)((v >> (i * 8)) & 0xFF);
				sb.Append(Convert.ToString(octet, 2).PadLeft(8, '0'));
				if (i > 0) sb.Append('.');
			}
			return sb.ToString();
		}

		private void BtnReset_Click(object sender, RoutedEventArgs e)
		{
			ResetFields();
			BtnCalc_Click(sender, e);
		}

		private char GetIPClass(int ip)
		{
			byte firstOctet = (byte)((ip >> 24) & 0xFF);
			if (firstOctet >= 1 && firstOctet <= 126) return 'A';
			if (firstOctet >= 128 && firstOctet <= 191) return 'B';
			if (firstOctet >= 192 && firstOctet <= 223) return 'C';
			return '?';
		}

		private void BtnClassify_Click(object sender, RoutedEventArgs e)
		{
			txtError.Text = "";

			if (string.IsNullOrWhiteSpace(cmbIP.Text))
			{
				txtError.Text = "Сначала введите IP-адрес!";
				return;
			}

			if (!IPAddress.TryParse(cmbIP.Text, out IPAddress ip))
			{
				txtError.Text = "Неверный IP-адрес (пример: 192.168.1.1)";
				return;
			}

			int ipInt = IpToInt(ip);
			char ipClass = GetIPClass(ipInt);

			string description = "";
			string range = "";

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

			WindowResult resultWindow = new WindowResult(ipClass, description, range);
			resultWindow.Owner = this;
			resultWindow.ShowDialog();
		}

		private void HelpLabel_Click(object sender, RoutedEventArgs e)
		{
			e.Handled = true;
			FrameworkElement source = sender as FrameworkElement;
			if (source == null) return;

			string topic = source.Tag?.ToString();
			if (string.IsNullOrEmpty(topic)) return;

			if (currentHelpWindow != null && currentHelpWindow.IsVisible)
			{
				currentHelpWindow.Activate();
				return;
			}

			string content = GetHelpContent(topic);
			currentHelpWindow = new HelpWindow("Справка", content);
			currentHelpWindow.Owner = this;
			currentHelpWindow.Closed += (s, args) => currentHelpWindow = null;
			currentHelpWindow.ShowDialog();
		}

		private string GetHelpContent(string topic)
		{
			switch (topic)
			{
				case "IP":
					return "IP-адрес (Internet Protocol address) – уникальный идентификатор устройства в сети.\n\n" +
						   "Формат IPv4: четыре числа от 0 до 255, разделённые точками (например, 192.168.1.1).\n\n" +
						   "В данном калькуляторе вы можете ввести любой корректный IPv4-адрес.";

				case "Mask":
					return "Маска подсети определяет, какая часть IP-адреса относится к сети, а какая – к узлу.\n\n" +
						   "Она представляет собой 32-битное число, в двоичном виде состоящее из последовательности единиц (сеть) и нулей (узлы).\n\n" +
						   "Пример правильной маски: 255.255.255.0 (двоично: 11111111.11111111.11111111.00000000).\n\n" +
						   "Маска должна быть непрерывной: нельзя, чтобы после нуля снова шли единицы.";

				case "Prefix":
					return "Префикс (CIDR) – количество единиц в маске подсети. Обозначается косой чертой после IP-адреса.\n\n" +
						   "Например, /24 соответствует маске 255.255.255.0, /16 – 255.255.0.0.\n\n" +
						   "Префикс может быть от 0 до 32. Чем больше префикс, тем меньше узлов в сети.";

				case "DecimalMask":
					return "Десятичный вид маски подсети — это привычное представление в виде четырёх чисел от 0 до 255, разделённых точками.\n\n" +
						   "Примеры правильных масок:\n" +
						   "- 255.255.255.0 (префикс /24)\n" +
						   "- 255.255.0.0 (/16)\n" +
						   "- 255.255.254.0 (/23)\n\n" +
						   "Важно: маска должна содержать непрерывные единицы, начиная со старшего бита.\n" +
						   "Неправильная маска: 255.100.255.0 — в ней после нуля во втором октете снова идут единицы.\n\n" +
						   "Вы можете ввести маску в это поле, и программа автоматически определит префикс и обновит ползунок.";

				case "Network":
					return "Адрес сети – это IP-адрес, в котором все биты узла обнулены.\n\n" +
						   "Он получается в результате побитового AND между IP-адресом и маской подсети.\n\n" +
						   "Адрес сети нельзя назначать устройству, он используется для идентификации всей подсети.";

				case "Broadcast":
					return "Широковещательный адрес – специальный адрес, предназначенный для отправки пакетов всем устройствам в данной сети.\n\n" +
						   "Вычисляется как адрес сети, в котором все биты узла установлены в единицу.\n\n" +
						   "Пакет, отправленный на широковещательный адрес, получают все узлы в подсети.";

				case "HostRange":
					return "Диапазон узлов – это все доступные IP-адреса в подсети, которые могут быть назначены устройствам.\n\n" +
						   "Первый адрес диапазона – это адрес сети + 1, последний – широковещательный адрес - 1.\n\n" +
						   "Например, для сети 192.168.1.0/24 диапазон узлов: 192.168.1.1 – 192.168.1.254.";

				case "BinaryIP":
					return "Двоичное представление IP-адреса — это запись всех 32 бит адреса в виде четырёх групп по 8 бит (октетов), разделённых точками.\n\n" +
						   "Каждый октет переводится из десятичного числа в двоичную систему (от 00000000 до 11111111).\n\n" +
						   "Например, IP 192.168.1.1 в двоичном виде: 11000000.10101000.00000001.00000001.\n\n" +
						   "Двоичный вид помогает понять, как маска разделяет адрес на сетевую и узловую части. Бит, стоящий на позиции, где в маске 1, относится к сети; где 0 — к узлу.";

				case "BinaryMask":
					return "Двоичное представление маски подсети — это 32 бита, состоящие из непрерывной последовательности единиц (слева) и нулей (справа).\n\n" +
						   "Пример: маска 255.255.248.0 (/21) в двоичном виде: 11111111.11111111.11111000.00000000.\n\n" +
						   "Единицы указывают на сетевую часть адреса, нули — на узловую. Количество единиц равно префиксу (CIDR).\n\n" +
						   "Двоичный вид маски критичен для понимания подсетей: побитовое И между IP и маской даёт адрес сети.";

				case "Results":
					return "В этом блоке отображаются основные вычисленные параметры подсети: адрес сети, широковещательный адрес и диапазон доступных узлов.\n\n" +
						   "Ниже находится выпадающий список с подробным объяснением каждого из расчётов. Выберите интересующий пункт, и под списком появится понятное описание с формулами и примерами.";

				default:
					return "Нет подробной информации по этому элементу.";
			}
		}
		private void CmbCalcHelp_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (txtCalcDescription == null) return;

			ComboBoxItem selectedItem = cmbCalcHelp.SelectedItem as ComboBoxItem;
			if (selectedItem == null) return;

			string topic = selectedItem.Content.ToString();
			txtCalcDescription.Text = GetCalculationDescription(topic);
		}

		private string GetCalculationDescription(string topic)
		{
			switch (topic)
			{
				case "Адрес сети":
					return "Адрес сети получается в результате поразрядной логической операции AND между IP-адресом и маской подсети.\n\n" +
						   "Формула: Сеть = IP & Маска\n\n" +
						   "В двоичном виде все биты узла обнуляются, остаётся только сетевая часть. Адрес сети не может быть присвоен устройству — он служит для идентификации всей подсети.\n\n" +
						   "Пример: IP 192.168.1.1, маска 255.255.255.0 -> адрес сети 192.168.1.0.";

				case "Широковещательный адрес":
					return "Широковещательный адрес (broadcast) — это адрес, по которому пакет получают все устройства в данной подсети.\n\n" +
						   "Формула: Broadcast = Сеть | (~Маска)\n\n" +
						   "То есть берётся адрес сети, и все биты узла устанавливаются в 1. Этот адрес также нельзя назначать хосту.\n\n" +
						   "Пример: для сети 192.168.1.0/24 широковещательный адрес — 192.168.1.255.";

				case "Диапазон узлов":
					return "Диапазон узлов (или диапазон хостов) — это все IP-адреса, которые можно присвоить устройствам в сети.\n\n" +
						   "Первый адрес: адрес сети + 1\n" +
						   "Последний адрес: широковещательный адрес - 1\n\n" +
						   "Количество доступных узлов: 2^(32-префикс) - 2 (для масок /31 и /32 есть исключения).\n\n" +
						   "Пример: сеть 192.168.1.0/24 -> узлы от 192.168.1.1 до 192.168.1.254.";

				case "IP в двоичном виде":
					return "IP-адрес переводится в двоичную систему счисления для наглядного представления его 32-битной структуры.\n\n" +
						   "Каждый октет (число от 0 до 255) преобразуется в 8-битное двоичное число, затем октеты объединяются через точку.\n\n" +
						   "Двоичный вид помогает понять, какие биты относятся к сети, а какие — к узлу (сравнивая с маской).\n\n" +
						   "Пример: 192.168.1.1 -> 11000000.10101000.00000001.00000001.";

				case "Маска в двоичном виде":
					return "Маска подсети в двоичном виде всегда состоит из непрерывной последовательности единиц (слева) и нулей (справа).\n\n" +
						   "Количество единиц равно префиксу (CIDR). Например, маска 255.255.248.0 в двоичном виде: 11111111.11111111.11111000.00000000 — здесь 21 единица, т.е. префикс /21.\n\n" +
						   "Двоичная маска используется для поразрядного умножения с IP-адресом (операция AND) и получения адреса сети.";

				case "Префикс (CIDR)":
					return "Префикс (CIDR — Classless Inter-Domain Routing) — это количество единиц в маске подсети, записываемое через косую черту после IP-адреса.\n\n" +
						   "Он определяет длину сетевой части адреса. Чем больше префикс, тем меньше узлов может быть в сети.\n\n" +
						   "Примеры:\n" +
						   "- /24 -> маска 255.255.255.0 -> 254 узла\n" +
						   "- /16 -> маска 255.255.0.0 -> 65534 узла\n" +
						   "- /30 -> маска 255.255.255.252 → 2 узла (для точечных соединений).\n\n" +
						   "Префикс можно задать ползунком или вручную в поле «Префикс».";

				default:
					return "Выберите тему из списка, чтобы увидеть подробное описание расчёта.";
			}
		}
	}
}