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
	//===============================================================================Окно заставки==============================================================================================//
	public partial class ScreenSaver : Window
	{
		public ScreenSaver()
		{
			InitializeComponent();

			var timer = new DispatcherTimer();              //Создаю таймер для автоматического закрытия заставки
			timer.Interval = TimeSpan.FromSeconds(5);       //Устанавливаю интервал – 5 секунд
			timer.Tick += (s, e) =>                         //Подписываюсь на событие Tick (срабатывает каждый раз по истечении интервала)
			{
				timer.Stop();                               //Останавливаю таймер, чтобы событие не сработало повторно
				this.Close();                               //Закрываю окно заставки
			};
			timer.Start();                                  //Запускаю таймер – отсчёт пошёл
		}
	}
}
