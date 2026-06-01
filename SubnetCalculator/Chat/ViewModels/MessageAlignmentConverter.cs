using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SubnetCalculator.Chat.Views
{
	public class MessageAlignmentConverter : IValueConverter
	{
		// Convert вызывается при привязке свойства HorizontalAlignment
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			// value – свойство IsOwn (bool)
			// Если сообщение своё, возвращаю HorizontalAlignment.Right, иначе Left
			return (value is bool isOwn && isOwn) ? HorizontalAlignment.Right : HorizontalAlignment.Left;
		}

		// Обратное преобразование не требуется
		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
	}
}