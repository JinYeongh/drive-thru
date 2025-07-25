using PaymentProject.Network;
using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media.Animation;
using System.Collections.Generic;

namespace PaymentProject
{
    public partial class Page1 : Page
    {
        private Client client; // 클라이언트 재사용

        public Page1()
        {
            try
            {
                InitializeComponent();
                client = new Client();
            }
            catch (Exception ex)
            {
                MessageBox.Show("XAML 초기화 오류: " + ex.Message);
            }
        }
        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            // 서버1 연결 시도
            bool connected = await client.ConnectAsync();
            if (!connected)
            {
                MessageBox.Show("서버1 연결 실패");
                return;
            }

            // 서버2 연결 시도
            bool server2Connected = await client.ConnectToServer2Async();
            if (!server2Connected)
            {
                MessageBox.Show("서버2 연결 실패");
                return;
            }

            // 주문번호 한 번만 생성
            string datePart = DateTime.Now.ToString("yyyyMMdd");
            string sequence = new Random().Next(1000, 9999).ToString();
            string sharedOrderNum = $"ORD-{datePart}-{sequence}";
            DateTime sharedOrderDate = DateTime.Now;

            // 서버1 주문 목록 받기
            var orders = await client.GetOrdersAsync();

            if (orders == null || orders.Count == 0)
            {
                MessageBox.Show("주문 정보 수신 실패");
                return;
            }

            // UI 바인딩
            OrderListControl.ItemsSource = orders;


            // 총 수량, 금액 표시
            int totalCount = orders.Sum(o => o.Quantity);
            int totalPrice = orders.Sum(o => o.Price);
            totalCountText.Text = $"{totalCount}개";
            totalPriceText.Text = $"{totalPrice:N0}원";

            // 변경된 부분 시작 --------------------------
            var grouped = client.GroupOrders(orders); // 그룹화
            grouped.Order_num = sharedOrderNum; // 주문번호 세팅
            grouped.Date = sharedOrderDate;     // 날짜 세팅

            // 서버2 전송 + 응답 확인
            var response = await client.SendToServer2AndWaitResponse(grouped);

            if (response != null && response.Status == "success")
            {
                Console.WriteLine($"서버2 응답: {response.Status} / {response.Message}");
                await Task.Delay(4000);

                // 결제 텍스트 애니메이션
                ProcessingText.Visibility = Visibility.Visible;
                var blinkAnim = new DoubleAnimation
                {
                    From = 1.0,
                    To = 0.2,
                    Duration = TimeSpan.FromSeconds(1),
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever
                };
                ProcessingText.BeginAnimation(UIElement.OpacityProperty, blinkAnim);

                // 결제 팝업
                PaymentPopup.Visibility = Visibility.Visible;
                var progressAnim = new DoubleAnimation
                {
                    From = 0,
                    To = 100,
                    Duration = TimeSpan.FromSeconds(3),
                    FillBehavior = FillBehavior.HoldEnd
                };
                CardProgressBar.BeginAnimation(ProgressBar.ValueProperty, progressAnim);

                await Task.Delay(4000);

                // 결제 완료 페이지로 이동
                var mainWindow = Application.Current.MainWindow as MainWindow;
                mainWindow?.MainFrame.Navigate(new PayComplete(sharedOrderNum));

                //// 서버 연결 종료
                //client.Disconnect();
                //client.DisconnectServer2();
            }
            else
            {
                string msg = (response == null)
                    ? "서버2 응답을 받지 못했습니다."
                    : $"서버2 응답 실패: {response.Message}";

                MessageBox.Show("❌ " + msg);
                return;
            }
        }
    }

    // UI 옵션띄우는 클래스
    public class OptionListToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var list = value as List<string>;
            if (list != null && list.Count > 0)
                return string.Join(", ", list);

            return "(옵션 없음)";
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException(); // 역변환은 안 씀
        }
    }
}