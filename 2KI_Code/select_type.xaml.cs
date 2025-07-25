using AzureSpeechDemo;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace real_drive
{
    public partial class select_type : Page
    {
        public event Action<string> select_type_Action;

        public string SelectedSize { get; private set; } = "미디움";

        private JObject data;
        public select_type(JObject obj)
        {
            InitializeComponent();
            this.data = obj;
            Loaded += SelectType_Loaded;
        }
        private async void SelectType_Loaded(object sender, RoutedEventArgs e)
        {
            ListeningStatusText.Text = "미디움 또는 라지라고 말씀해주세요...";

            string text = await AzureSpeechDemo.MainWindow.Myfucs.RecognizeSpeechAsync();

            string cleanText = text.Replace("인식된 텍스트: ", "").Trim();
            cleanText = Regex.Replace(cleanText, @"[.?!]+$", "");
            cleanText = cleanText.ToLower();

            if (cleanText.Contains("라지"))
            {
                SelectedSize = "라지";
                HighlightButton(LargeBtn);
                this.data["select_type"] = "large";
            }
            else
            {
                SelectedSize = "미디움";
                HighlightButton(MediumBtn);
                this.data["select_type"] = "medium";
            }
            ListeningStatusText.Text = $"\"{SelectedSize}\" 세트를 선택하셨습니다.";
            await Task.Delay(1000);

            // ✅ Option 창 바로 띄우기
            var mainWindow = (MainWindow)Application.Current.MainWindow;
            string fullText = "세트 " + SelectedSize;

            Option optionPage = new Option(this.data);

            Window popup = new Window
            {
                Title = "옵션 선택",
                Content = optionPage,
                Owner = mainWindow,
                Width = 800,
                Height = 1000,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                ShowInTaskbar = false
            };
            string selected = null;
            optionPage.OptionSelected += (option) =>
            {
                selected = option;
                popup.DialogResult = true;  // ✅ 여기서 Window의 DialogResult 설정!
            };
            if (popup.ShowDialog() == true)
            {
                Console.WriteLine($"선택된 옵션: {selected}");
                popup.Close();
            }
            JObject tmp = JObject.Parse(selected);
            // ✅ 1. 미리 JArray 준비
            JArray optionArray = new JArray();
            JArray setIdArray = new JArray();
            JArray pricearray = new JArray();
            // ✅ 2. tmp 에서 사이드/음료 MenuName 추출해서 추가
            optionArray.Add(tmp["selected"]["사이드"]?["Item1"]);
            optionArray.Add(tmp["selected"]["음료"]?["Item1"]);

            setIdArray.Add(tmp["selected"]["사이드"]?["Item2"]);
            setIdArray.Add(tmp["selected"]["음료"]?["Item2"]);

            pricearray.Add(tmp["selected"]["사이드"]?["Item3"]);
            pricearray.Add(tmp["selected"]["음료"]?["Item3"]);

            this.data["items"]["orders"][0]["option"] = new JArray();
            this.data["items"]["orders"][0]["Set_id"] = new JArray();

            // ✅ 3. obj 안에 삽입
            this.data["items"]["orders"][0]["option"] = optionArray;
            this.data["items"]["orders"][0]["Set_id"] = setIdArray;
            this.data["items"]["orders"][0]["set_price"] = pricearray;

            string result = this.data.ToString();  // 보기 좋게 출력
  
            Window parentWindow = Window.GetWindow(this);
            if (parentWindow != null)
            {
                select_type_Action.Invoke(result);  // 가능하다면 설정
                parentWindow.Close();              // 창 닫기
            }
        }
        private void HighlightButton(Button btn)
        {
            // 강조된 버튼 배경색: 약간 더 진한 노랑
            btn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFDA49"));

            // 강조 테두리: DarkRed 계열
            btn.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A52814"));
            btn.BorderThickness = new Thickness(3);
        }

    }
}
