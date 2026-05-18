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
using System.Windows.Threading;

namespace SubnetCalculator
{
	public partial class ScreenSaver : Window
	{
		public ScreenSaver()
		{
			InitializeComponent();

			var timer = new DispatcherTimer();
			timer.Interval = TimeSpan.FromSeconds(5);
			timer.Tick += (s, e) =>
			{
				timer.Stop();
				this.Close();
			};
			timer.Start();
		}
	}
}
