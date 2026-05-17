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

	public partial class HelpWindow : Window
	{
		public HelpWindow(string title, string content)
		{
			InitializeComponent();
			this.Title = Title;
			txtHelpContent.Text = content;
		}

		private void Btn_Ok_Click(object sender, RoutedEventArgs e)
		{
			Close();
		}
	}
}
