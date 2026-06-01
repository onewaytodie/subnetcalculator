using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SubnetCalculator.Chat.Views
{
	// Этот класс-конвертер служит для установки цвета фона сообщения в чате
	// в зависимости от того, принадлежит ли сообщение текущему пользователю (IsOwn).
	// Если IsOwn = true (своё сообщение) – фон синий, иначе – тёмно-серый.
	public class OwnMessageBackgroundConverter : IValueConverter
	{
		// Метод Convert вызывается, когда значение привязки нужно преобразовать из источника в цель.
		// Здесь входное значение – булево свойство IsOwn (true – своё сообщение, false – чужое).
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			// Проверяем, что value является bool, и если да, сохраняем его значение в переменную isOwn.
			// Если value не bool, isOwn = false (считаем сообщение чужим).
			bool isOwn = value is bool b && b;

			// Тернарный оператор: если isOwn == true, возвращаем синюю кисть (для своих сообщений),
			// иначе возвращаем тёмно-серую кисть (для чужих сообщений).
			return isOwn
				? new SolidColorBrush(Color.FromRgb(44, 123, 229)) // Синий цвет (акцент)
				: new SolidColorBrush(Color.FromRgb(62, 62, 66));  // Тёмно-серый цвет (фон чата)
		}

		// ConvertBack не используется (привязка односторонняя), поэтому выбрасываем исключение.
		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
			=> throw new NotImplementedException();
	}
}