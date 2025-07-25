using AzureSpeechDemo;
using System;
using System.Collections.Generic;
using System.Linq;
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
using static AzureSpeechDemo.MainWindow;

namespace real_drive
{
    public partial class Popup : Window
    {
        List<string> OK = Mydefines.Ok.ToList(); // consts/define.cs에서 정의한 OK 문자열 배열을 리스트로 변환
        List<string> NO = Mydefines.No.ToList(); // consts/define.cs에서 정의한 No 문자열 배열을 리스트로 변환
        public bool? UserChoice { get; private set; }

        public Popup(Mydefines.PopupType Type)
        {
            InitializeComponent();
            Console.WriteLine("팝업이 생성되었습니다.");// 페이지가 로드될 때 사용자에게 선택을 요청하는 메시지를 표시
            switch (Type)
            {
                case Mydefines.PopupType.YesNo:
                    Title = "확인해주세요";
                    TextBlock_Text.Text = "세트매뉴옵션을 변경하시려면\r\n네, 아니요 중 하나를 선택해주세요";
                    TextBlock_Text.FontSize = 30;
                    TextBlock_Text.FontWeight = FontWeights.Bold;
                    // 마이크 열어   
                    this.GetSpeechInputAsync(); // 음성 인식 시작
                    break;

                case Mydefines.PopupType.reOrder:
                    Title = "다시주문해주세요~";
                    TextBlock_Text.Text = "다시 주문해주세요";
                    TextBlock_Text.FontSize = 30;
                    TextBlock_Text.FontWeight = FontWeights.Bold;
                    this.StartFadeOutTimer(); // 팝업이 뜨고 5초 후에 자동으로 닫히도록 타이머 시작;
                    // 시간두고 종료하기
                    break;

                case Mydefines.PopupType.Go:
                    Title = "주문 완료하셨으면\r\n앞으로 이동해주세요";
                    TextBlock_Text.Text = "주문 완료하셨으면\r\n앞으로 이동해주세요";
                    TextBlock_Text.FontSize = 24;
                    TextBlock_Text.FontWeight = FontWeights.Bold;
                    this.StartFadeOutTimer(); // 팝업이 뜨고 5초 후에 자동으로 닫히도록 타이머 시작;
                    break;
            }
        }

        private async void GetSpeechInputAsync()
        {
            // 음성 인식 결과를 반환하는 메서드
            // 실제 음성 인식 로직을 구현
            string text = await Myfucs.RecognizeSpeechAsync();
            Console.WriteLine("🗣 인식된 텍스트: " + text);
            // 예시: OK 리스트와 비교
            if (OK.Contains(text))
            {
                UserChoice = true;
                this.Close();
            }
            else if (NO.Contains(text))
            {
                UserChoice = false;
                this.Close();
            }
            else
            {
                TextBlock_Text.Text = "잘 못들었어요. 다시 말씀해주세요!";
            }
        }

        private async void StartFadeOutTimer()
        {
            await Task.Delay(5000); // 5초 기다렸다가

            const int fadeDurationMs = 1000;
            const int intervalMs = 50;
            double steps = fadeDurationMs / intervalMs;
            double delta = 1.0 / steps;

            for (int i = 0; i < steps; i++)
            {
                this.Opacity -= delta;
                await Task.Delay(intervalMs);
            }

            this.Close(); // 페이드 완료 후 닫기
        }
        private void YesButton_Click(object sender, RoutedEventArgs e)
        {
            UserChoice = true;        
            this.Close();
        }

        private void NoButton_Click(object sender, RoutedEventArgs e)
        {
            UserChoice = false;
            this.Close();
        }
    }
}
