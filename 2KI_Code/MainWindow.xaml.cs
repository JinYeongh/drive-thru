using Microsoft.CognitiveServices.Speech;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using real_drive;
using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions; // 꼭 추가
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;


namespace AzureSpeechDemo
{
    class Payload
    {
        public string? action { get; set; }
        public string? data { get; set; }
        public bool? is_set { get; set; }
    }
    public class OrderItem
    {
        public int No { get; set; }
        public string Menu { get; set; } = "-";
        public List<string> Option { get; set; }
        public int Count { get; set; } = 1;
        public int Price { get; set; }
        public List<int> Set_Id { get; set; }
    }

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
            throw new NotImplementedException(); // 역변환 필요 없음
        }
    }
    public partial class MainWindow : Window
    {
        private TcpSignalListener signalListener;
        private List<OrderItem> orderList = new List<OrderItem>();

        private TcpClient client;   //서버와 연결을 관리하는 객체
        private NetworkStream stream;   //서버와 실제 데이터를 주고받는 통로 
        private bool isUserSpeaking = false;
        private DateTime popupOpenedTime = DateTime.MinValue;

        // "2번 삭제", "3 개 지워" 같은 단일 항목 삭제
        private static readonly Regex DeleteOnePattern =
            new(@"(일|이|삼|사|오|육|칠|팔|구|십|\d+)\s*(번|개)?\s*(삭제|지워|취소)(요|해)?", RegexOptions.IgnoreCase);
        private static string NormalizeNumbers(string input)
        {
            return input
                .Replace("일", "1")
                .Replace("이", "2")
                .Replace("삼", "3")
                .Replace("사", "4")
                .Replace("오", "5")
                .Replace("육", "6")
                .Replace("칠", "7")
                .Replace("팔", "8")
                .Replace("구", "9")
                .Replace("영", "0")
                .Replace("십", "10");
        }


        // "전체 취소", "모두 삭제" 같은 전체 삭제
        private static readonly Regex DeleteAllPattern =
            new(@"(전체|모두)\s*(삭제|지워|취소)", RegexOptions.IgnoreCase);

        // 서버에서 JSON으로 오는 응답 데이터를 C# 객체로 변환하기 위한 모델 클래스
        public class ServerResponse
        {
            public string status { get; set; }  //서버가 보낸 JSON에서 "status" 값을 자동으로 넣어줌
        }

        public MainWindow()             //생성자 
        {
            InitializeComponent();      //XAML과 코드를 연결해주는 역할 
            _ = ConnectToServerAsync(); // 앱 시작 시 서버 연결 시도
                                        // _ = : 실행만 하고 결과는 무시함 -> 경고를 방지하기 위해서 
                                        // await랑 비슷한 개념이라고 생각하면 되지만, 아무것도 쓰지않으면 C#이 경고를 날림
            signalListener = new TcpSignalListener();
            signalListener.OnSignalReceivedText = (string msg) =>
            {
                Dispatcher.Invoke(async () =>
                {
                    if (msg == "번호판 감지됨")
                    {
                        plateStatusText.Text = "차량 인식됨"; //  차량 상태 업데이트
                        await StartSpeechLoopAsync();  // 반복으로 변경
                    }
                    else if (msg == "번호판 없음")
                    {
                        plateStatusText.Text = "차량 인식 안됨"; //  차량 상태 업데이트
                        StopMicrophone(); // 마이크 OFF
                    }
                });
            };
            signalListener.StartListening(12345); // 포트 시작
        }

        // 서버 연결
        //try catch : 오류가 발생해도 프로그램이 꺼지지않도록 감싸는 구조 
        private async Task ConnectToServerAsync()
        {
            try
            {
                client = new TcpClient();
                await client.ConnectAsync("10.10.20.96", 9034);
                //클라이언트와 서버사이의 데이터 송수신을 위한 통로를 가져옴 
                stream = client.GetStream();
                Console.WriteLine("서버에 연결되었습니다.");  //콘솔에 출력 
            }
            catch (Exception ex)
            {
                Console.WriteLine("서버 연결 실패: " + ex.Message);
            }
        }

        private bool isListening = false;   // 루프 제어용 플래그
        private System.Timers.Timer silenceTimer;
        private Window idlePopup;
        private async Task StartSpeechLoopAsync()
        {

            if (isListening)
            {
                Console.WriteLine("이미 음성 인식 루프가 실행 중입니다.");
                return;
            }
            else
            {
                Console.WriteLine("음성 인식 루프 시작");
            }

            isListening = true;

            // 타이머 설정: 6초 후 팝업
            silenceTimer = new System.Timers.Timer(3000);
            silenceTimer.Elapsed += async (s, e) =>
            {
                await Dispatcher.InvokeAsync(async () =>
                {
                    if (isUserSpeaking)
                    {
                        Console.WriteLine("사용자 발화 중 → 팝업 생략하고 타이머 재시작");
                        silenceTimer.Stop();
                        silenceTimer.Start();
                        return;
                    }
                    if (idlePopup == null || !idlePopup.IsVisible)
                    {
                        idlePopup = new real_drive.Popup(Mydefines.PopupType.Go)
                        {
                            Owner = this,
                            WindowStartupLocation = WindowStartupLocation.CenterOwner
                        };
                        idlePopup.Show();
                        popupOpenedTime = DateTime.Now;
                    }
                });
            };
            silenceTimer.AutoReset = false;
            while (isListening)
            {
                micStatusText.Text = "마이크 ON";
                micStatusLight.Fill = new SolidColorBrush(Colors.LimeGreen);
                micStatusBorder.Background = new SolidColorBrush(Color.FromRgb(221, 246, 221));

                silenceTimer.Start();

                var text = await Myfucs.RecognizeSpeechAsync();
                silenceTimer.Stop();

                if (idlePopup != null && idlePopup.IsVisible)
                {
                    var elapsed = DateTime.Now - popupOpenedTime;
                    if (elapsed.TotalMilliseconds < 1000)
                    {
                        await Task.Delay(2000 - (int)elapsed.TotalMilliseconds);
                    }
                    idlePopup.Close();
                    idlePopup = null;
                }
                string cleanText = text.Replace("인식된 텍스트: ", "").Trim();
                cleanText = Regex.Replace(cleanText, @"[.?!]+$", "");
                cleanText = NormalizeNumbers(cleanText);

                if (string.IsNullOrWhiteSpace(cleanText) || cleanText.Contains("음성을 인식하지 못했습니다"))
                {
                    speechGuideText.Text = "다시 말씀해주세요.";
                    continue;
                }
                else
                {
                    speechGuideText.Text = "어서오세요.";
                }

                Console.WriteLine($"\n [음성 인식 결과] {cleanText}");

                if (HandleDeleteCommand(cleanText))
                {
                    Console.WriteLine(" [삭제 명령] 처리됨 → 서버 전송 생략");
                    continue;
                }
                bool check = Myfucs.ContainsSetWord(ref cleanText);
                if (!check)
                {
                    Console.WriteLine(" [구분] 일반 주문 → 서버에 order 전송");
                    string serverResult = await SendJsonToServerAsync("order", cleanText);
                    this.make_menu_ui(serverResult); // 서버 응답을 UI에 반영
                    Console.WriteLine($" [서버 응답 - 주문] {serverResult}");
                }
                else
                {
                    Console.WriteLine(" [구분] 세트 메뉴 포success함 → 세트 타입 선택창으로 이동");
                    string serverResult = await SendJsonToServerAsync("setop", cleanText);
                    JObject obj = JObject.Parse(serverResult);
                    if (obj["status"].ToString() != "success")
                    {
                        Console.WriteLine("성공 !");
                    }
                    else
                    {
                        var selectPage = new real_drive.select_type(obj);
                        var popup = new Window
                        {
                            Title = "세트 선택",
                            Content = selectPage,
                            Width = 500,
                            Height = 400,
                            WindowStartupLocation = WindowStartupLocation.CenterOwner,
                            Owner = this,
                            ResizeMode = ResizeMode.NoResize,
                            WindowStyle = WindowStyle.None,
                            Background = Brushes.White,
                            ShowInTaskbar = false
                        };
                        selectPage.select_type_Action += (string resultJson) =>
                        {
                            Console.WriteLine("받은 데이터: " + resultJson);
                            this.make_menu_ui(resultJson); // 서버 응답을 UI에 반영
                            // 원하는 로직 수행 (예: JSON 처리, 저장, 다음 화면 전환 등)
                        };

                        bool? result = popup.ShowDialog();
                        if (result == true)
                        {
                            Console.WriteLine(" [세트 사이즈 선택 실패 또는 취소]");

                            continue;
                        }
                        else
                        {
                            Console.WriteLine("세트옵션 고르기 성공!");
                            continue;
                        }   
                    }
                }
            }
        }
        private void make_menu_ui(string serverResult)
        {
            var jsonObj = JObject.Parse(serverResult);

            var orders = jsonObj["items"]?["orders"] as JArray;

            if (orders != null && orders.Count > 0)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    foreach (var orderToken in orders)
                    {
                        string tmp = jsonObj["select_type"]?.ToString() ?? "";
                        int price = orderToken["price"]?.ToObject<int>() ?? 0;
                        string selectType = "";
                        if (tmp != "")
                        {
                            selectType = "세트";

                            if (tmp == "large")
                            {
                                selectType += "(L)";
                            }
                            else
                            {
                                selectType += "(M)";
                            }
                            foreach (var item1 in orderToken["set_price"])
                                {
                                    Console.WriteLine("세트 가격: " + item1);
                                    price += Convert.ToInt32(item1);
                                }
                        }
                        var item = new OrderItem
                        {
                            No = orderList.Count + 1,
                            Menu = (orderToken["name"]?.ToString() + selectType ?? "(메뉴 없음)"),
                            Option = orderToken["option"]?.ToObject<List<string>>() ?? new List<string> { "없음" },
                            Count = orderToken["qty"]?.ToObject<int>() ?? 1,
                            Price = (price * orderToken["qty"]?.ToObject<int>() ?? 1),
                            Set_Id = orderToken["Set_id"]?.ToObject<List<int>>() ?? new List<int> { 0 },
                        };
                      
                        orderList.Add(item);
                        string json = JsonConvert.SerializeObject(item, Formatting.Indented);
                        Console.WriteLine("📝 OrderItem JSON:\n" + json);
                    }
                    // 리스트 바인딩 다시
                    orderListView.ItemsSource = null;
                    orderListView.ItemsSource = orderList;

                    // 총 수량, 총 가격 계산
                    int totalcount = 0;
                    int totalPrice = 0;
                    foreach (var order in orderList)
                    {
                        totalcount += order.Count;
                        totalPrice += (order.Price);
                    }
                    totalCountText.Text = totalcount + "개";
                    totalPriceText.Text = totalPrice.ToString("N0") + "원";
                });
            }
        }
        private async void StopMicrophone()
        {
            Console.WriteLine("마이크 꺼짐");

            // 마이크 상태 텍스트 및 불 색상 변경
            micStatusText.Text = "마이크 OFF";
            micStatusLight.Fill = new SolidColorBrush(Colors.Red);
            micStatusBorder.Background = new SolidColorBrush(Color.FromRgb(255, 230, 230));  // 연빨강
            //디버깅용 ================지워도 됨
            Console.WriteLine($"주문 개수: {orderList.Count}");
            Console.WriteLine($"차량 상태: {plateStatusText.Text}");
            Console.WriteLine($"스트림 가능 여부: {signalListener.stream_pay?.CanWrite}");
            Console.WriteLine($"클라이언트 연결 여부: {signalListener.payClient?.Connected}");

            if (orderList.Count == 0)
            {
                Console.WriteLine(" 전송할 주문이 없습니다.");
                return;
            }
            // 리스트 → JSON 변환
            string jsonString = JsonConvert.SerializeObject(orderList) + "\n";
            byte[] dataToSend = Encoding.UTF8.GetBytes(jsonString);

            // 차량이 인식안되구, 세번째 키오스크가 연결되어있을때
            if ((signalListener.stream_pay != null) && (this.plateStatusText.Text == "차량 인식 안됨"))
            {
                try
                {
                    //  기존 stream_pay 재사용 (using 제거)
                    if (signalListener.payClient?.Connected == true && signalListener.stream_pay.CanWrite)
                    {
                        await signalListener.stream_pay.WriteAsync(dataToSend, 0, dataToSend.Length);
                        Console.WriteLine(" 주문 내역 서버로 전송 완료");

                        //  전송 성공 → 주문 초기화
                        orderList.Clear();
                        RefreshGrid();
                        Console.WriteLine(" 주문 리스트 초기화 완료");

                        //ui도 초기화
                        totalCountText.Text = "0개";
                        totalPriceText.Text = "0원";
                    }
                    else
                    {
                        Console.WriteLine(" stream_pay 또는 payClient가 닫혀 있거나 유효하지 않습니다.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($" 주문 전송 중 오류 발생: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine(" stream_pay가 null입니다. 전송 실패");
            }

            isListening = false; //  루프 정지
        }
        //  서버로 JSON 전송 후 응답 받기
        public async Task<string> SendJsonToServerAsync(string signal, string text)
        {
            string status;

            // 서버 연결 확인
            if (client == null || stream == null)
                return " 서버에 연결되어 있지 않습니다.";

            try
            {
                //  전송할 JSON 만들기
                var payload = new Payload
                {
                    action = signal,
                    data = text,
                    is_set = false
                };

                if (signal == "setop")
                {
                    payload.action = "order";  // ✅ 가능!
                    payload.is_set = true;
                }

                string jsonString = JsonConvert.SerializeObject(payload) + "\n";
                byte[] dataToSend = Encoding.UTF8.GetBytes(jsonString);

                Console.WriteLine($"\n [서버 전송] {jsonString.Trim()}");

                //  서버로 전송
                await stream.WriteAsync(dataToSend, 0, dataToSend.Length);
                Console.WriteLine(" 전송 완료, 응답 대기 중...");

                // 응답 읽기
                List<byte> fullData = new List<byte>();
                byte[] buffer = new byte[1024];

                while (true)
                {
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0) break; // 연결 끊김

                    fullData.AddRange(buffer.Take(bytesRead));

                    // 종료 조건: 줄바꿈으로 응답 끝났다고 판단
                    if (Encoding.UTF8.GetString(fullData.ToArray()).Contains('\n')) break;
                }

                string result = Encoding.UTF8.GetString(fullData.ToArray()).Trim();
                Console.WriteLine($" [서버 응답] {result}");

                // 응답 분기 처리
                switch (signal)
                {
                    case "order":
                        Console.WriteLine(" [오더 요청]");
                        return result;

                    case "setop":
                        Console.WriteLine(" [옵션 세트 요청]");
                        return result;

                    default:
                        Console.WriteLine($" [알 수 없는 시그널]: {signal}");
                        return "unknown_action";
                }
            }
            catch (Exception ex)
            {
                return $"❗ 통신 오류: {ex.Message}";
            }
        }
        internal class Myfucs
        {
            public static bool ContainsSetWord(ref string input)
            {
                List<string> wordList = Mydefines.Set.ToList(); // consts/define.cs에서 정의한 Set 문자열 배열을 리스트로 변환

                // 3. 포함된 단어를 찾아서 삭제
                foreach (string word in wordList)
                {
                    if (input.Contains(word))
                    {
                        input = input.Replace(word, "");
                        Console.WriteLine($"포함된 단어: {word} 단어뺸 문장 : {input}");

                        return true;
                    }
                }
                return false; // 반복문 안들어가면 무조건 거짓!
            }
            // Azure 음성 인식 새로 수정한 부분
            public static async Task<string> RecognizeSpeechAsync()
            {
                Console.WriteLine($"키: {Mydefines.return_key}");
                Console.WriteLine($"리전: {Mydefines.return_region}");

                var config = SpeechConfig.FromSubscription(Mydefines.return_key, Mydefines.return_region);
                config.SpeechRecognitionLanguage = "ko-KR";
                //  마이크 감도 및 무음 처리 설정
                config.SetProperty("SpeechServiceConnection.InitialSilenceTimeoutMs", "3000");
                config.SetProperty("SpeechServiceConnection.EndSilenceTimeoutMs", "1500");

                using var recognizer = new SpeechRecognizer(config);

                // Recognizing 중에 true
                recognizer.Recognizing += (s, e) =>
                {
                    Console.WriteLine($"🔊 음성 입력 중: {e.Result.Text}");
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        var mainWindow = (MainWindow)Application.Current.MainWindow;
                        mainWindow.isUserSpeaking = true;
                        mainWindow.silenceTimer?.Stop(); // 사용자가 말하면 타이머 중단
                    });
                };
                // RecognizeOnceAsync() 끝난 뒤 false로 설정 (반드시 호출됨 보장)
                var result = await recognizer.RecognizeOnceAsync();

                recognizer.Dispose(); // ✔️ 여기서 바로 해제

                Application.Current.Dispatcher.Invoke(() =>
                {
                    var mainWindow = (MainWindow)Application.Current.MainWindow;
                    mainWindow.isUserSpeaking = false;
                });
                if (result.Reason == ResultReason.RecognizedSpeech)
                {
                    Console.WriteLine($" 인식된 텍스트: \"{result.Text}\"");
                    return $"인식된 텍스트: {result.Text}";
                }
                else if (result.Reason == ResultReason.NoMatch)
                {
                    Console.WriteLine("음성을 인식하지 못했습니다 ");
                    return "음성을 인식하지 못했습니다.";
                }
                else if (result.Reason == ResultReason.Canceled)
                {
                    var cancellation = CancellationDetails.FromResult(result);
                    return $"취소됨. 이유: {cancellation.Reason}, 상세: {cancellation.ErrorDetails}";
                }
                return "알 수 없는 오류 발생.";
            }
        }

        private bool HandleDeleteCommand(string cleanText)
        {
            Console.WriteLine($" 삭제명령 검사중: {cleanText}");

            if (DeleteAllPattern.IsMatch(cleanText))
            {
                Console.WriteLine(" 전체 삭제 명령 인식됨!");
                orderList.Clear();
                RefreshGrid();
                return true;
            }

            var m = DeleteOnePattern.Match(cleanText);
            if (m.Success && int.TryParse(m.Groups[1].Value, out int no))
            {
                Console.WriteLine($" 개별 삭제 명령 인식됨! → No: {no}");
                var target = orderList.FirstOrDefault(o => o.No == no);
                if (target != null)
                {
                    orderList.Remove(target);
                    for (int i = 0; i < orderList.Count; i++)
                        orderList[i].No = i + 1;

                    RefreshGrid();
                }
                return true;
            }
            Console.WriteLine(" 삭제 명령 아님.");
            return false;
        }

        private void RefreshGrid()
        {
            orderListView.ItemsSource = null;
            orderListView.ItemsSource = orderList;

            // ✅ 총 개수와 가격을 업데이트
            int totalCount = 0;
            int totalPrice = 0;

            foreach (var item in orderList)
            {
                totalCount += item.Count;
                totalPrice += item.Price;
            }
            totalCountText.Text = totalCount + "개";
            totalPriceText.Text = totalPrice.ToString("N0") + "원";
        }
        // 창 닫을 때 리소스 정리
        protected override void OnClosed(EventArgs e)
        {
            // "?"가 붙는 이유 : 혹시 stream이 살아있으면 닫아주고 없으면 조용히 넘어가줘~~ 
            // 없다면 : 앱이 강제 종료될 수도 있다
            stream?.Close();
            client?.Close();
            base.OnClosed(e);
        }
    }


    public static class Mydefines
    {
        //하팀장 코드 
        private static readonly string subscriptionKey = Environment.GetEnvironmentVariable("AZURE_KEY") ?? "KEY_NOT_FOUND";
        private const string region = "koreacentral";   //Azure Speech 서비스가 위치한 데이터센터의 지역 지정
        public class OptionItem
        {
            public string B_SET_CAT { get; set; }
            public string B_SET_OP_NAME { get; set; }
            public int B_SET_LOP_PRICE { get; set; } = 0;
            public int B_SET_OP_PRICE { get; set; } = 0;
        }
        public static string return_key
        {
            get { return subscriptionKey; }
        }
        public static string return_region
        {
            get { return region; }
        }
        public enum WebRtcVadMode
        {
            Quality = 0,      // 낮은 민감도 (잡음에도 덜 민감)
            Normal = 1,
            Aggressive = 2,
            VeryAggressive = 3 // 매우 민감 (조금만 말해도 인식)
        }
        public enum PopupType
        {
            YesNo,
            reOrder,
            Go,
            None
        };
        public static string[] Ok = new string[]
        {
    "네",
    "예",
    "넵",
    "나",
    "좋아요",
    "그럴게요",
    "알겠습니다",
    "오케이",
    "ㅇㅋ",
    "응",
    "그래",
    "그래요",
    "좋지",
    "물론이죠",
    "당연하죠",
    "그럼요",
    "오케바리",
    "OK",
    "콜",
    "감사합니다"
        };

        public static string[] No = new string[]
        {
    "아니요",
    "아니",
    "아니야",
    "안 돼요",
    "싫어요",
    "그건 좀...",
    "힘들 것 같아요",
    "안 할래요",
    "어렵습니다",
    "죄송하지만 어렵겠습니다",
    "생각해볼게요",
    "지금은 힘들 것 같아요",
    "다음에 기회가 되면요",
    "절대 안 돼",
    "안 할 거야",
    "그건 못 하지",
    "전혀 아니야",
    "그런 건 아닌데요",
    "그렇게 생각하지 않아요",
    "그건 좀 다르죠"
        };

        public static string[] Set = new string[]
        {
        "셑트",
        "센트",
        "세트",
        "세터",
        "세티",
        "세띠",
        "세토",
        "세투",
        "쎄트",
        "쎄투",
        "셋",
        "셋트",
        "섯트",
        "셋업",
        "셋팅",
        "세티업",
        "세텁"
        };
    }
}