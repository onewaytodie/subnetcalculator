using System;

namespace SubnetCalculator.Chat.Models
{
	public class FileTransferRequest
	{
		// Имя (ник) отправителя файла
		public string SenderNick { get; set; }

		// Имя (ник) получателя файла
		public string RecipientNick { get; set; }

		// Имя файла (без пути)
		public string FileName { get; set; }

		// Размер файла в байтах (long – для больших файлов)
		public long FileSize { get; set; }

		// Время создания запроса (когда клиент отправил /file_request)
		public DateTime Timestamp { get; set; }

		// Уникальный идентификатор запроса (генерируется автоматически)
		public string RequestId { get; set; }

		// Конструктор – инициализирует новый запрос:
		// - создаёт уникальный Guid в виде строки
		// - устанавливает текущее время
		public FileTransferRequest()
		{
			RequestId = Guid.NewGuid().ToString(); // глобально уникальный идентификатор
			Timestamp = DateTime.Now;              // текущее время на сервере (или на клиенте)
		}
	}
}