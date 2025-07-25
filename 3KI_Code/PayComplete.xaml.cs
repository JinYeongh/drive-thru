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
using System.Windows.Navigation;
using System.Windows.Shapes;


namespace PaymentProject
{
    /// <summary>
    /// PayComplete.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class PayComplete : Page
    {
        private readonly string orderNumber; //주문번호 저장용
        public PayComplete(string orderNum)
        {
            InitializeComponent();

            orderNumber = orderNum;

            // 주문번호를 UI에 출력
            orderNumText.Text = orderNumber;
        }
        // 페이지 다시 전환 (결제완료 -> 주문확인)
        private async void PayComplete_Loaded(object sender, RoutedEventArgs e)
        {
            await Task.Delay(3000); // 3초 대기

            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow != null)
            {
                // Page1 재생성하여 초기화된 상태로 진입
                mainWindow.MainFrame.Navigate(new Page1());
            }
        }
    }
}
