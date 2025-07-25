using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Navigation;

namespace PaymentProject.Network
{
    public class Client
    {
        // 서버1 연결용 필드
        private TcpClient? tcpClient;
        private NetworkStream? stream;

        private readonly string serverIp = "10.10.20.99";
        private readonly int serverPort = 12345;

        // 서버2 연결용 필드
        private TcpClient? server2Client;
        private NetworkStream? server2Stream;

        private readonly string server2Ip = "10.10.20.96"; // 서버2 IP
        private readonly int server2Port = 9034;           // 서버2 포트

        // 날짜 포맷 커스텀
        public class CustomDateTimeConverter : IsoDateTimeConverter
        {
            public CustomDateTimeConverter()
            {
                DateTimeFormat = "yyyy-MM-dd HH:mm:ss";
            }
        }

        // 주문 데이터 클래스
        public class ReceivedOrder
        {
            public string? Menu { get; set; }
            [JsonProperty("Count")]
            public int Quantity { get; set; }
            public int Price { get; set; }
            public List<int>? Set_Id { get; set; }
            public string? Request { get; set; } = "";
            public List<string>? Option { get; set; } = new(); // new() 로 기본값 제공
        }

        // 응답용 클래스
        public class ServerResponse
        {
            [JsonProperty("status")]
            public string Status { get; set; }

            [JsonProperty("message")]
            public string Message { get; set; }
        }

        // 묶음 전송용 클래스
        public class GroupedOrderHistory
        {
            public string? Order_num { get; set; }

            [JsonConverter(typeof(CustomDateTimeConverter))]
            public DateTime Date { get; set; }
            public int Total_Price { get; set; }  // 총금액 추가

            public List<ReceivedOrder> Items { get; set; }
        }

        // 서버1 연결
        public async Task<bool> ConnectAsync()
        {
            try
            {
                tcpClient = new TcpClient();
                await tcpClient.ConnectAsync(serverIp, serverPort);

                stream = tcpClient.GetStream();

                var request = new { action = "get_orders" };
                string json = JsonConvert.SerializeObject(request) + "\n";
                byte[] data = Encoding.UTF8.GetBytes(json);
                await stream.WriteAsync(data, 0, data.Length);

                Console.WriteLine("서버1 연결됨");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("\u274c 서버1 연결 실패: " + ex.Message);
                return false;
            }
        }

        // 서버1 요청 전송 및 응답
        public async Task<string> SendAsync(object requestObj)
        {
            if (stream == null || !tcpClient.Connected)
            {
                Console.WriteLine("서버1 연결안됨");
                return null;
            }
            try
            {
                string json = JsonConvert.SerializeObject(requestObj) + "\n";
                byte[] data = Encoding.UTF8.GetBytes(json);
                await stream.WriteAsync(data, 0, data.Length);

                byte[] buffer = new byte[8192];
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                return Encoding.UTF8.GetString(buffer, 0, bytesRead);
            }
            catch (Exception ex)
            {
                Console.WriteLine("서버1 통신 오류: " + ex.Message);
                return null;
            }
        }
        public async Task<List<ReceivedOrder>> GetOrdersAsync()
        {
            try
            {
                if (stream == null || !tcpClient.Connected)
                {
                    Console.WriteLine("서버1에 연결되지 않았습니다.");
                    return null;
                }

                byte[] buffer = new byte[8192];
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);

                string response = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                Console.WriteLine("서버1 응답 수신:\n" + response);

                var rawOrders = JsonConvert.DeserializeObject<List<ReceivedOrder>>(response);

                //옵션 파싱 처리
                foreach (var order in rawOrders)
                {
                    if (order.Option != null && order.Option.Count == 1 && order.Option[0].Contains(","))
                    {
                        var splitOptions = order.Option[0].Split(',').Select(x => x.Trim()).ToList();
                        order.Option = splitOptions;
                    }
                }
                return rawOrders;
            }
            catch (Exception ex)
            {
                Console.WriteLine("서버1 Read 실패: " + ex.Message);
                return null;
            }
        }

        // 서버1 종료
        public void Disconnect()
        {
            stream?.Close();
            tcpClient?.Close();
            Console.WriteLine("서버1 연결 종료");
        }

        // 서버2 연결
        public async Task<bool> ConnectToServer2Async()
        {
            try
            {
                server2Client = new TcpClient();
                await server2Client.ConnectAsync(server2Ip, server2Port);
                server2Stream = server2Client.GetStream();
                Console.WriteLine("서버2 연결 성공");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("서버2 연결 실패: " + ex.Message);
                return false;
            }
        }
        //  GroupOrders 함수 추가
        public GroupedOrderHistory GroupOrders(List<ReceivedOrder> orders)
        {
            return new GroupedOrderHistory
            {
                Order_num = "TEMP", // Page1에서 세팅해줄 것
                Date = DateTime.Now,
                Total_Price = orders.Sum(o => o.Price),
                Items = orders
            };
        }
        //  SendToServer2AndWaitResponse 함수 추가
        public async Task<ServerResponse>SendToServer2AndWaitResponse(GroupedOrderHistory grouped)
        {
            try
            {
                if (server2Stream == null || !server2Client.Connected)
                {
                    bool connected = await ConnectToServer2Async();
                    if (!connected)
                    {
                        Console.WriteLine("서버2 연결 재시도 실패");
                        return null;
                    }
                }

                var payload = new
                {
                    action = "payment",
                    history = grouped
                };

                string json = JsonConvert.SerializeObject(payload, new CustomDateTimeConverter()) + "\n";
                byte[] data = Encoding.UTF8.GetBytes(json);
                await server2Stream.WriteAsync(data, 0, data.Length);

                byte[] buffer = new byte[1024];
                int bytesRead = await server2Stream.ReadAsync(buffer, 0, buffer.Length);
                string responseJson = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                Console.WriteLine("서버2 응답:\n" + responseJson);

                var response = JsonConvert.DeserializeObject<ServerResponse>(responseJson);


                return JsonConvert.DeserializeObject<ServerResponse>(responseJson);
            }
            catch (Exception ex)
            {
                Console.WriteLine("서버2 응답 수신 실패: " + ex.Message);
                return null;
            }
        }

        // 서버2 종료
        public void DisconnectServer2()
        {
            server2Stream?.Close();
            server2Client?.Close();
            Console.WriteLine("서버2 연결 종료");
        }
    }
}
