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
		//=========================================================Обработчик кнопки "Калькулятор подсетей"=====================================================================================//
		private void OpenSubnetCalculator_Click(object sender, RoutedEventArgs e)
		{
			SubnetCalculatorWindow calcWindow = new SubnetCalculatorWindow();	//Создаю новое окно калькулятора подсетей
			calcWindow.Owner = this;                                            //Устанавливаю владельца – текущее главное окно (чтобы калькулятор был поверх него и модально блокировал)
			calcWindow.ShowDialog();                                            // Открываем окно модально – пользователь не может вернуться в главное окно, пока не закроет калькулятор
		}
		//=========================================================Обработчик "кнопок-заглушек" (задел ну будущие домашки)======================================================================//
		private void ShowPlaceholder_Click(object sender, RoutedEventArgs e)
		{
			MessageBox.Show("В разработке.", "Информация",
							MessageBoxButton.OK, MessageBoxImage.Information);  // Вывожу  сообщение с информацией о том, что проект ещё в разработке
		}
	}
}
