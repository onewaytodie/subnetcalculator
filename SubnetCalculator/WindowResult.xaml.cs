using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace SubnetCalculator
{
	public partial class WindowResult : Window
	{
		public WindowResult(char ipClass, string description, string range)
		{
			InitializeComponent();

			txtClassLetter.Text = ipClass.ToString();
			switch (ipClass)
			{
				case 'A':
					txtClassLetter.Foreground = System.Windows.Media.Brushes.Red;
					break;
				case 'B':
					txtClassLetter.Foreground = System.Windows.Media.Brushes.Green;
					break;
				case 'C':
					txtClassLetter.Foreground = System.Windows.Media.Brushes.Blue;
					break;
				default:
					txtClassLetter.Foreground = System.Windows.Media.Brushes.Gray;
					break;
			}

			txtDescription.Text = description;
			txtRange.Text = range;
		}

		private void OkButton_Click(object sender, RoutedEventArgs e)
		{
			this.Close();
		}
	}
}
