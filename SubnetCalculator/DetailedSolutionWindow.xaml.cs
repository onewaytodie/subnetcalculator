using System.Linq;
using System.Windows;

namespace SubnetCalculator
{
	public partial class DetailedSolutionWindow : Window
	{
		public DetailedSolutionWindow(string ip, string mask, int prefix,
			string ipBinary, string maskBinary,
			string networkBinary, string networkIp,
			string broadcastBinary, string broadcastIp,
			string firstHost, string lastHost, string hostCount)
		{
			InitializeComponent();
			DataContext = new DetailedSolutionViewModel(ip, mask, prefix,
				ipBinary, maskBinary, networkBinary, networkIp,
				broadcastBinary, broadcastIp, firstHost, lastHost, hostCount);
		}
	}

	public class DetailedSolutionViewModel
	{
		public string IP { get; }
		public string Mask { get; }
		public string Prefix { get; }
		public string IPBinary { get; }
		public string MaskBinary { get; }
		public string NetworkBinary { get; }
		public string NetworkIp { get; }
		public string BroadcastBinary { get; }
		public string BroadcastIp { get; }
		public string FirstHost { get; }
		public string LastHost { get; }
		public string HostCount { get; }

		public string DetailedBinaryExplanation { get; }
		public string DetailedNetworkExplanation { get; }
		public string DetailedBroadcastExplanation { get; }
		public string DetailedHostRangeExplanation { get; }

		public DetailedSolutionViewModel(string ip, string mask, int prefix,
			string ipBinary, string maskBinary,
			string networkBinary, string networkIp,
			string broadcastBinary, string broadcastIp,
			string firstHost, string lastHost, string hostCount)
		{
			IP = $"IP-адрес: {ip}";
			Mask = $"Маска подсети: {mask}";
			Prefix = $"Префикс: /{prefix}";
			IPBinary = ipBinary;
			MaskBinary = maskBinary;
			NetworkBinary = networkBinary;
			NetworkIp = networkIp;
			BroadcastBinary = broadcastBinary;
			BroadcastIp = broadcastIp;
			FirstHost = firstHost;
			LastHost = lastHost;
			HostCount = $"Количество доступных узлов: {hostCount}";

			DetailedBinaryExplanation = GenerateBinaryExplanation(ip, mask, ipBinary, maskBinary);
			DetailedNetworkExplanation = GenerateNetworkExplanation(ip, mask, ipBinary, maskBinary, networkBinary, networkIp);
			DetailedBroadcastExplanation = GenerateBroadcastExplanation(mask, maskBinary, networkBinary, broadcastBinary, broadcastIp);
			DetailedHostRangeExplanation = GenerateHostRangeExplanation(networkIp, broadcastIp, firstHost, lastHost, hostCount, prefix);
		}

		private string GenerateBinaryExplanation(string ip, string mask, string ipBinary, string maskBinary)
		{
			return $"Каждый октет IP-адреса и маски переводится в двоичную систему отдельно.\n\n" +
				   $"IP {ip} в двоичном виде:\n{ipBinary}\n\n" +
				   $"Маска {mask} в двоичном виде:\n{maskBinary}\n\n" +
				   $"Двоичное представление необходимо для выполнения побитовых операций (AND, OR, NOT), которые используются для вычисления адреса сети и широковещательного адреса.";
		}

		private string GenerateNetworkExplanation(string ip, string mask, string ipBinary, string maskBinary, string networkBinary, string networkIp)
		{
			return $"Адрес сети получается побитовым логическим И (AND) между IP-адресом и маской.\n\n" +
				   $"Бинарно:\n{ipBinary} & {maskBinary} = {networkBinary}\n\n" +
				   $"Правило: бит результата равен 1 только если оба соответствующих бита операндов равны 1. " +
				   $"Таким образом, все биты узла (где в маске 0) обнуляются.\n\n" +
				   $"Результат в десятичном виде: {networkIp}. Этот адрес не может быть назначен устройству, он используется для идентификации всей подсети.";
		}

		private string GenerateBroadcastExplanation(string mask, string maskBinary, string networkBinary, string broadcastBinary, string broadcastIp)
		{
			string invertedMaskBinary = string.Join(".", maskBinary.Split('.').Select(octet => new string(octet.Select(c => c == '0' ? '1' : '0').ToArray())));

			return $"Широковещательный адрес вычисляется как: (IP-адрес AND маска) OR (побитовое НЕ маски).\n\n" +
				   $"Сначала находим инверсию маски (все нули маски становятся единицами):\n{maskBinary} ~ (NOT) {invertedMaskBinary}\n\n" +
				   $"Затем выполняем побитовое ИЛИ между адресом сети и инвертированной маской:\n{networkBinary} | {invertedMaskBinary} = {broadcastBinary}\n\n" +
				   $"Результат: {broadcastIp}. Все биты узла установлены в 1, поэтому пакет, отправленный на этот адрес, получают все устройства в подсети.";
		}

		private string GenerateHostRangeExplanation(string networkIp, string broadcastIp, string firstHost, string lastHost, string hostCount, int prefix)
		{
			return	$"Диапазон адресов, которые можно назначать устройствам, определяется следующим образом:\n\n" +
					$"- Первый адрес = адрес сети + 1 -> {networkIp} + 1 = {firstHost}\n\n" +
					$"- Последний адрес = широковещательный адрес - 1 -> {broadcastIp} - 1 = {lastHost}\n\n" +
					$"- Количество доступных адресов = 2^(32 - префикс) - 2 (исключаем адрес сети и широковещательный)\n" +
					$"Для префикса /{prefix}: 2^{32 - prefix} - 2 = {hostCount}";
		}
	}
}
