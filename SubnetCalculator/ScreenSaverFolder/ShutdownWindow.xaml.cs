using System;
using System.Windows;
using System.Windows.Threading;

namespace SubnetCalculator
{
	public partial class ShutdownWindow : Window
	{
		public ShutdownWindow()
		{
			InitializeComponent();
			DispatcherTimer timer = new DispatcherTimer();
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
