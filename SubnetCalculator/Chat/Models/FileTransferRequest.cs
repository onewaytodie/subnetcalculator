using System;

namespace SubnetCalculator.Chat.Models
{
	public class FileTransferRequest
	{
		public string SenderNick { get; set; }
		public string RecipientNick { get; set; }
		public string FileName { get; set; }
		public long FileSize { get; set; }
		public DateTime Timestamp { get; set; }
		public string RequestId { get; set; } // уникальный идентификатор запроса

		public FileTransferRequest()
		{
			RequestId = Guid.NewGuid().ToString();
			Timestamp = DateTime.Now;
		}
	}
}