using System;
using System.Net;
using System.Text;
using System.Windows;
using System.Windows.Controls;


namespace SubnetCalculator
{
	public partial class MainWindow : Window
	{
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
	}
}