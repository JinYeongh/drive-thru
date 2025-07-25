using AzureSpeechDemo;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
using static System.Net.Mime.MediaTypeNames;
using static System.Windows.Forms.LinkLabel;

namespace real_drive
{
    /// <summary>
    /// Option.xaml에 대한 상호 작용 논리
    /// </summary>
    /// 
    public class Samestr
    {
        // key: 표준 메뉴 이름
        // value: 유사 발화 리스트
        public readonly Dictionary<string, List<string>> SynonymMap = new()
        {
            { "후렌치 후라이", new List<string>{ "후렌치 후라이", "후렌치후라이", "감자튀김", "프렌치프라이", "감튀", "후렌치", "후렌치프라이", "프렌치 후라이", "프라이", "감자튀김 하나" } },
            { "코울슬로", new List<string>{ "코울슬로", "콜슬로", "코올슬로", "콜우슬로", "양배추 샐러드", "코우슬로", "코울 슬로", "슬로", "코울슬로 하나", "코울샐러드" } },
            { "코카 콜라", new List<string>{ "코카 콜라", "코카콜라", "코크", "콜라", "콜라 하나", "코카콜라 하나", "코카 콜라 주세요", "콜라 주세요", "콜라 음료", "코크 하나" } },
            { "환타 라지", new List<string>{ "환타 라지", "환타 L", "환타 라지 사이즈", "라지 환타", "큰 환타", "환타 큰 거", "환타 라지 주세요", "환타 큰 사이즈", "환타 L 사이즈", "라지사이즈 환타" } },
            { "스프라이트", new List<string>{ "스프라이트", "사이다", "스프라이트 하나", "스프라이트 주세요", "스프라이트 음료", "청량음료", "사이다 하나", "스프라이트 한 잔", "스프라이트 주세용", "스프라이트요" } },
            { "코카 콜라 제로", new List<string> { "코카콜라 제로", "콜라 제로", "코크 제로", "코카 제로", "콜라제로", "코카콜라 0", "제로 콜라", "콜라 0", "코크제로", "제로콜라 하나" } },
            { "바닐라 쉐이크", new List<string> { "바닐라 쉐이크", "바닐라 셰이크", "바닐라 쉐이크요", "바닐라 셰이커", "바닐라쉐이크", "바닐라스무디", "바닐라 음료", "바닐라 쉐익", "바닐라 드링크", "바닐라 밀크쉐이크" } },
            { "딸기 쉐이크", new List<string> { "딸기 쉐이크", "딸기셰이크", "딸기 쉐이커", "딸기 쉐익", "딸기스무디", "딸기 음료", "딸기쉐이크", "딸기 드링크", "딸기 밀크쉐이크", "딸기 셰이크" } },
            { "초코 쉐이크", new List<string> { "초코 쉐이크", "초코셰이크", "초콜릿 쉐이크", "초코쉐이크", "초코 셰이커", "초콜릿 셰이크", "초코 음료", "초코 드링크", "초코 쉐이커", "초콜렛 쉐이크" } },
            { "드립 커피", new List<string> { "드립 커피", "드립커피", "드립커피 주세요", "드립 커피 한 잔", "드립", "드립 커피 하나", "드립 커피 음료", "드립 커피요", "커피 드립", "드립 한 잔" } },
            { "아이스 드립 커피", new List<string> { "아이스 드립 커피", "아이스 드립", "드립 아이스 커피", "시원한 드립 커피", "아이스 드립커피", "드립 커피 아이스", "아이스 드립커피요", "드립 아이스", "아이스 커피 드립", "드립커피 차가운 거" } },
            { "아메리카노", new List<string> { "아메리카노", "아메", "아메리카노 주세요", "아메 한 잔", "아메리카노 한 잔", "아메리카노 음료", "아메요", "아메리카노 커피", "아메리카노요", "커피 아메" } },
            { "아이스 아메리카노", new List<string> { "아이스 아메리카노", "아이스 아메", "시원한 아메리카노", "아아", "아아 주세요", "아이스 아메리카노요", "아아 한 잔", "아메리카노 아이스", "아메 아이스", "아이스 커피 아메" } },
            { "카푸치노", new List<string> { "카푸치노", "카푸치노 커피", "카푸치노 한 잔", "카푸", "카푸치노 주세요", "카푸치노요", "커피 카푸치노", "부드러운 커피", "카푸 치노", "카푸치노 음료" } },
            { "카페라떼", new List<string> { "카페라떼", "라떼", "카페 라떼", "라떼 주세요", "라떼 한 잔", "카페라떼요", "밀크커피", "라떼 커피", "라떼요", "라떼 음료" } },
            { "아이스 카페라떼", new List<string> { "아이스 카페라떼", "아이스 라떼", "카페라떼 아이스", "차가운 라떼", "아이스라떼", "아이스 밀크커피", "라떼 아이스", "아라떼", "아이스 카페라떼요", "아이스 우유커피" } },
            { "바닐라 라떼", new List<string> { "바닐라 라떼", "바닐라라떼", "바닐라 커피", "바닐라 우유 커피", "바닐라 라떼 한 잔", "바닐라 라떼요", "바닐라 맛 라떼", "바닐라 밀크커피", "바닐라라떼 주세요", "라떼 바닐라" } },
            { "아이스 바닐라 라떼", new List<string> { "아이스 바닐라 라떼", "바닐라 아이스 라떼", "바닐라 라떼 아이스", "바닐라 라떼 차가운 거", "아이스 바닐라라떼", "시원한 바닐라 라떼", "아이스 라떼 바닐라", "아이스 바닐라 커피", "아바라", "바닐라 라떼요" } },
            { "피치 아이스티", new List<string> { "피치 아이스티", "복숭아 아이스티", "아이스티 피치", "복숭아티", "아이스 피치티", "피치티", "복숭아 차", "피치 아이스 티", "피치아이스티", "복숭아 음료" } },
            { "망고 피치 아이스티", new List<string> { "망고 피치 아이스티", "망고 복숭아 아이스티", "피치 망고 아이스티", "망고 피치티", "망고 아이스티", "피치 망고 티", "망고 복숭아 티", "망고 피치 음료", "망피치티", "망고 피치 아이스 티" } }
        };
        // 정확히 일치하거나 포함하는 유사어가 있는 경우 가장 적합한 키를 반환
        public string FindBestMatch(string input)
        {
            foreach (var pair in SynonymMap)
            {
                if (input.Contains(pair.Key))
                    return pair.Key;

                foreach (var alias in pair.Value)
                {
                    if (input.Contains(alias))
                        return pair.Key;
                }
            }
            return null; // 매칭 실패
        }
    }
    public class PickCat
    {
        // 카테고리 이름 → (선택된 메뉴 이름, 선택된 번호)
        public Dictionary<string, (string MenuName, int MenuIndex, int Menu_price)> SelectedOptions { get; set; } = new();
        public string select_type = "";
    }
    public partial class Option : Page
    {
        private PickCat user_pick = new PickCat();

        public event Action<string> OptionSelected;

        private struct menu
        {
            public string name;
            public int price;
            public int qty;
            public string type;
        }
        List<Border> borders = new List<Border>(); // 동적으로성되는 Border들을 저장할 리스트
        private int cat_count = 0;
        JObject cat = new JObject();
        private string select_type = "";
        public Option(JObject data)
        {
            this.cat["cat"] = new JArray();
            InitializeComponent();
            ListenForOptionCommand();
            Console.WriteLine($"옵션 페이지가 생성되었습니다. 받은 데이터 :  {data}");

            //고른 타입 저장
            this.select_type = data["select_type"].ToString();
            this.user_pick.select_type = this.select_type; // 선택한 타입 저장

            //받은 매뉴개수만큼 Grid_options에 구역나누고 추가하기
            //JArray arr_menus = data["option"] as JArray; // 이게 가장 깔끔하고 안전해요.
            JArray arr_menu = (JArray)data["items"]["orders"];
            List<menu> menu = arr_menu.ToObject<List<menu>>();
            this.TextBlock_menu.Text = menu[0].name + "옵션\r\n옵션을 말해주세요🎤\r\n(예. 코울슬로, 사이다 M)";

            JArray arr_menus = (JArray)data["set_options"]; // 이게 가장 깔끔하고 안전해요.

            List<Mydefines.OptionItem> tmp = arr_menus.ToObject<List<Mydefines.OptionItem>>();
            // 유아이 세팅!
            this.GenerateOptionUI(tmp, ref borders);
            // HighlightBestMatch 메서드를 호출하여 음성 인식 결과와 가장 유사한 메뉴를 강조 표시
            //JArray arr_menus = JArray.Parse((string)data["option"]);
            this.MicOn();
        }
        // 마이크켜!
        public async Task MicOn()
        {
            // 마이크켜서 텍스트로 변환
            bool cat_count = true;
            while (cat_count)
            {
                var text = await MainWindow.Myfucs.RecognizeSpeechAsync();
                string cleanText = text.Replace("인식된 텍스트: ", "").Trim();
                cleanText = Regex.Replace(cleanText, @"[.?!]+$", "");

                VoiceCommandHelper.HandleScrollCommand(OptionScrollViewer, "내려");
                VoiceCommandHelper.HandleScrollCommand(OptionScrollViewer, "올려");
                VoiceCommandHelper.HandleScrollCommand(OptionScrollViewer, "끝까지 내려");
                VoiceCommandHelper.HandleScrollCommand(OptionScrollViewer, "끝까지 올려");
                VoiceCommandHelper.HandleScrollCommand(OptionScrollViewer, "처음으로");
                HighlightBestMatch(cleanText, this.borders);
                this.check_borders(ref cat_count);
            }
        }
        private async void ListenForOptionCommand()
        {
            string result = await Myfucs.RecognizeSpeechAsync();

            if (!string.IsNullOrWhiteSpace(result))
            {
                // 1. "인식된 텍스트: " 제거, 공백 제거, 소문자로
                string rawText = result.Replace("인식된 텍스트: ", "").Trim().ToLower();

                // 2. 문장 끝의 특수문자 제거 (.!? 등)
                string scrollCommandText = System.Text.RegularExpressions.Regex.Replace(rawText, @"[.?!]+$", "");

                // 3. 스크롤 명령어 실행
                VoiceCommandHelper.HandleScrollCommand(OptionScrollViewer, scrollCommandText);

                // (선택) 이후 text로 다른 처리하려면 여기서 사용
                string text = rawText;  // or scrollCommandText
            }
        }

        private void check_borders(ref bool catcount)
        {
            int count = 0;

            if (this.user_pick.SelectedOptions.Count == cat_count)
            {
                var resultJson = JObject.FromObject(new
                {
                    selected = this.user_pick.SelectedOptions
                });
                catcount = false;
                OptionSelected?.Invoke(resultJson.ToString());
            }
        }

        // 각 옵션 아이템에 따라 가격 표시 문자열 생성
        private string PriceLabel(Mydefines.OptionItem item)
        {
            if (this.select_type == "medium")
            {
                return $" +{item.B_SET_OP_PRICE:#,##0}원";
            }
            else
            {
                return $" +{item.B_SET_LOP_PRICE:#,##0}원";
            }
        }
        // 문자열 유사도 계산 함수 (0.0 ~ 1.0)
        public static double Similarity(string s1, string s2)
        {
            int dist = LevenshteinDistance(s1, s2);
            int maxLen = Math.Max(s1.Length, s2.Length);
            return 1.0 - (double)dist / maxLen; // 1.0이면 완전일치
        }
        // 레벤슈타인 거리 계산 (두 문자열 간 최소 편집 횟수)
        public static int LevenshteinDistance(string s, string t)
        {
            int n = s.Length, m = t.Length;
            int[,] dp = new int[n + 1, m + 1];
            // 초기화
            for (int i = 0; i <= n; i++) dp[i, 0] = i;
            for (int j = 0; j <= m; j++) dp[0, j] = j;
            // DP로 거리 계산
            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = (s[i - 1] == t[j - 1]) ? 0 : 1;
                    dp[i, j] = Math.Min(
                        Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1),
                        dp[i - 1, j - 1] + cost);
                }
            }
            return dp[n, m];
        }
        // 주어진 문자열과 가장 유사한 메뉴명 찾아주는 함수
        public static string FindClosestMenu(string input, Dictionary<string, List<string>> synonymMap)
        {
            string bestMatch = null;
            int minDistance = int.MaxValue;

            foreach (var pair in synonymMap)
            {
                // 표준어 먼저 검사
                int dist = LevenshteinDistance(input, pair.Key);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    bestMatch = pair.Key;
                }

                // 유사어도 검사
                foreach (var alias in pair.Value)
                {
                    dist = LevenshteinDistance(input, alias);
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        bestMatch = pair.Key;
                    }
                }
            }
            return bestMatch;
        }
        // 음성 텍스트와 가장 유사한 메뉴를 찾아 UI에서 강조
        public void HighlightBestMatch(string recognizedText, List<Border> borders)
        {
            string cleanText = recognizedText.Trim();
            var synonymMap = new Samestr().SynonymMap;

            string bestMatch = null;
            double bestScore = 0.0;

            foreach (var pair in synonymMap)
            {
                double mainSim = Similarity(cleanText, pair.Key);
                if (mainSim > bestScore)
                {
                    bestScore = mainSim;
                    bestMatch = pair.Key;
                }

                foreach (var alias in pair.Value)
                {
                    double sim = Similarity(cleanText, alias);
                    if (sim > bestScore)
                    {
                        bestScore = sim;
                        bestMatch = pair.Key;
                    }
                }
            }
            Console.WriteLine($"유사도 최고값: {bestScore:F2}, 매칭된 항목: {bestMatch}");

            if (bestScore < 0.7)
            {
                Console.WriteLine("유사도가 낮아 매칭되지 않음");
                return;
            }

            foreach (var border in borders)
            {
                // 기본 배경, 테두리 초기화
                border.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF8F0"));
                border.BorderBrush = (Brush)new BrushConverter().ConvertFromString("#FFCC00");
                border.BorderThickness = new Thickness(1);

                // 내부 자식 구조 파고들기 (Grid → StackPanel → TextBlock)
                if (border.Child is Grid grid)
                {
                    if (grid.Children[0] is StackPanel stack)
                    {
                        if (stack.Children[0] is TextBlock nameText)
                        {
                            if (nameText.Text.Contains(bestMatch))
                            {
                                // 강조 스타일 적용
                                border.Background = (Brush)new BrushConverter().ConvertFromString("#FFE6B0");
                                border.BorderBrush = (Brush)new BrushConverter().ConvertFromString("#FFCC00");
                                border.BorderThickness = new Thickness(2);

                                // 같은 카테고리 내부 다른 보더 초기화
                                string category = (border.Tag as string) ?? "";
                                foreach (var b in borders.Where(b => (b.Tag as string) == category))
                                {
                                    if (b != border)
                                        b.Background = (Brush)new BrushConverter().ConvertFromString("#FFF3DC");
                                }
                                // 가격 파싱
                                int price = 0;
                                TextBlock priceTextBlock = stack.Children[1] as TextBlock;
                                if (priceTextBlock != null)
                                {
                                    price = Convert.ToInt32(priceTextBlock.Tag);
                                }
                                // DB 에서는 1번부터 시작하니까 +1해주기
                                this.user_pick.SelectedOptions[category] = (nameText.Text, (borders.IndexOf(border) + 1), price);
                                Console.WriteLine(border);
                                return;
                            }
                        }
                    }
                }
            }
        }
        public string SelectedOption { get; private set; }
        public static class VoiceCommandHelper
        {
            public static void HandleScrollCommand(ScrollViewer scroll, string recognizedText)
            {
                string cleanText = recognizedText.Trim().ToLower();

                double currentOffset = scroll.VerticalOffset;
                double maxOffset = scroll.ScrollableHeight;

                if (cleanText.Contains("끝까지 내려"))
                    scroll.ScrollToEnd();
                else if (cleanText.Contains("끝까지 올려") || cleanText.Contains("처음으로"))
                    scroll.ScrollToHome();
                else if (cleanText.Contains("내려"))
                    scroll.ScrollToVerticalOffset(Math.Min(currentOffset + 200, maxOffset));
                else if (cleanText.Contains("올려"))
                    scroll.ScrollToVerticalOffset(Math.Max(currentOffset - 200, 0));
            }
        }
        public void GenerateOptionUI(List<Mydefines.OptionItem> items, ref List<Border> borders)
        {
            // 카테고리 목록 추출 (중복 제거)
            var categories = items.Select(i => i.B_SET_CAT).Distinct();

            foreach (var category in categories)
            {
                // 카테고리별 외곽 박스
                var outerBorder = new Border
                {
                    BorderBrush = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    CornerRadius = new CornerRadius(10),
                    Margin = new Thickness(10),
                    Padding = new Thickness(10)
                };

                var panel = new StackPanel();

                // 카테고리 제목 텍스트
                var title = new TextBlock
                {
                    Text = category,
                    FontSize = 22,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 0, 0, 10)
                };
                panel.Children.Add(title);

                var wrap = new WrapPanel
                {
                    Orientation = Orientation.Horizontal
                };

                foreach (var item in items.Where(i => i.B_SET_CAT == category))
                {
                    // 가격 태그 추출
                    var tagValue = (this.select_type == "medium") ? item.B_SET_OP_PRICE : item.B_SET_LOP_PRICE;

                    int name_length = item.B_SET_OP_NAME.Length;
                    Console.WriteLine($"옵션 아이템: {item.B_SET_OP_NAME} 이름의 길이 : {name_length}");

                    // 각 옵션 항목 카드
                    var innerBorder = new Border
                    {
                        Width = 120,
                        Height = 80,
                        BorderThickness = new Thickness(1),
                        BorderBrush = (Brush)new BrushConverter().ConvertFromString("#FFCC00"),
                        Margin = new Thickness(5),
                        Tag = category, // 카테고리를 보더에 저장
                        CornerRadius = new CornerRadius(8),
                        Background = (Brush)new BrushConverter().ConvertFromString("#FFF3DC"),
                        Child = new Grid
                        {
                            VerticalAlignment = VerticalAlignment.Center,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Children =
                    {
                        new StackPanel
                        {
                            VerticalAlignment = VerticalAlignment.Center,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Children =
                            {
                                new TextBlock
                                {
                                    Text = item.B_SET_OP_NAME,
                                    FontSize = 16,
                                    FontWeight = FontWeights.SemiBold,
                                    TextAlignment = TextAlignment.Center,
                                    TextWrapping = TextWrapping.Wrap,
                                    Margin = new Thickness(0, 0, 0, 5),
                                    HorizontalAlignment = HorizontalAlignment.Center
                                },
                                new TextBlock
                                {
                                    Text = PriceLabel(item),
                                    Tag = tagValue, // 가격을 Tag에 저장
                                    FontSize = 14,
                                    Foreground = Brushes.Gray,
                                    TextAlignment = TextAlignment.Center,
                                    HorizontalAlignment = HorizontalAlignment.Center
                                }
                            }
                        }
                    }
                        }
                    };
                    borders.Add(innerBorder);
                    wrap.Children.Add(innerBorder);
                }
                panel.Children.Add(wrap);
                outerBorder.Child = panel;
                this.cat_count++;
                this.MainStackPanel.Children.Add(outerBorder);
            }
        }

    }
}