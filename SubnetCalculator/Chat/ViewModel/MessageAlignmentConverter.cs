using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SubnetCalculator.Chat.Views
{
	public class MessageAlignmentConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is bool isOwn && isOwn)
				return HorizontalAlignment.Right;
			return HorizontalAlignment.Left;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
			=> throw new NotImplementedException();
	}
}