using System;

namespace SubnetCalculator.Chat.Models
{
	public class ChatMessage // Класс, представляющий одно сообщение в чате
	{
		public string Author { get; set; } // Имя отправителя (ник) – кто написал сообщение
		public string Text { get; set; }   // Текст сообщения (или системное уведомление)
		public DateTime Timestamp { get; set; } // Время отправки сообщения (точность до секунд/миллисекунд)
		public bool IsOwn { get; set; }    // Флаг: true – сообщение принадлежит текущему пользователю (своё), false – чужое (для выравнивания и цвета)
	}
}