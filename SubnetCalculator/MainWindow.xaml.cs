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
	public partial class MainWindow : Window
	{
		public MainWindow()
		{
			InitializeComponent();
		}
		private void OpenSubnetCalculator_Click(object sender, RoutedEventArgs e)
		{
			var calcWindow = new SubnetCalculatorWindow();
			calcWindow.Owner = this;
			calcWindow.ShowDialog();
		}

		private void ShowPlaceholder_Click(object sender, RoutedEventArgs e)
		{
			MessageBox.Show("В разработке.", "Информация",
							MessageBoxButton.OK, MessageBoxImage.Information);
		}
	}
}
