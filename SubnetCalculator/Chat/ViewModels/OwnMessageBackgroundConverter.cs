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
			bool isOwn = value is bool b && b;
			return isOwn ? new SolidColorBrush(Color.FromRgb(44, 123, 229)) : new SolidColorBrush(Color.FromRgb(62, 62, 66));
		}
		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
	}
}