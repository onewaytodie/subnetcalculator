using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SubnetCalculator.Chat.Views
{
	public class OwnMessageBackgroundConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is bool isOwn && isOwn)
				return new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4));
			return new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33));
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
			=> throw new NotImplementedException();
	}
}