using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace SubnetCalculator
{
	public partial class App : Application
	{
		protected override void OnStartup(StartupEventArgs e)
		{
			base.OnStartup(e);

			ScreenSaver screen = new ScreenSaver();
			screen.Show();

			DispatcherTimer timer = new DispatcherTimer();
			timer.Interval = TimeSpan.FromSeconds(3);
			timer.Tick += (s, args) =>
			{
				timer.Stop();
				screen.Close();     

				MainWindow main = new MainWindow();
				main.Show();

				main.Closed += (o, ev) => this.Shutdown();
			};
			timer.Start();
		}
	}
}
