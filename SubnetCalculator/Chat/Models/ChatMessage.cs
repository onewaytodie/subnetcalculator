using System;


namespace SubnetCalculator.Chat.Models
{
	public class ChatMessage
	{
		public string Author { get; set; }
		public string Text { get; set; }
		public DateTime Timestamp { get; set; }
		public bool IsOwn { get; set; } // true – сообщение от текущего пользователя
	}
}
