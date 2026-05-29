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
		//Переопределяю метод OnStartup – он вызывается при запуске приложения ДО отображения главного окна
		protected override void OnStartup(StartupEventArgs e)
		{
			base.OnStartup(e);								//Вызов базовой реализации

			ScreenSaver screen = new ScreenSaver();         //Создаю и показываю окно заставки
			screen.Show();                                  //Показываю заставку (окно появится на экране)

			//Создаю таймер для автоматического закрытия заставки и открытия главного окна
			DispatcherTimer timer = new DispatcherTimer();  //Таймер, работающий в потоке UI
			timer.Interval = TimeSpan.FromSeconds(3);       //время между закрытием заставки и открытием приложухи(должно быть +-одинаковым с временем заставки)
			timer.Tick += (s, args) =>                      //Подписка на событие "тик" таймера
			{
				timer.Stop();                               //Останавливаю таймер, чтобы событие не повторялось
				screen.Close();                             //Закрываю окно заставки

				MainWindow main = new MainWindow();         //После закрытия заставки создаю и показываю главное окно приложения
				main.Show();                                //Отображаю главное окно

															//Когда пользователь закроет главное окно, вызовется Shutdown() для завершения приложения
			};
			timer.Start();                                  //Запускаю таймер – начинается отсчёт 5 секунд
		}
	}
}
