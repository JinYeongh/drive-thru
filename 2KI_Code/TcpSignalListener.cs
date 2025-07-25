using AzureSpeechDemo;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;



//새로 수정한거
public class TcpSignalListener
{
    public TcpClient payClient
    {
        get; private set;
    }
    public NetworkStream stream_pay
    {
        get; private set;
    }
private TcpListener listener;
    public Action<string> OnSignalReceivedText;  // 문자열 메시지 수신 이벤트

    public class sendItem
    {
        public int Id { get; set; }
        public string Menu { get; set; } = "-";
        public string Option { get; set; } = "-";
        public int Quantity { get; set; } = 1;
        public int Price { get; set; }
    }
    public void StartListening(int port)
    {
        listener = new TcpListener(IPAddress.Any, port);
        listener.Start();

        Task.Run(async () =>
        {
            Console.WriteLine($"🚀 서버 시작됨, 포트: {port}");

            while (true)
            {
                var client = await listener.AcceptTcpClientAsync();
                Console.WriteLine($"✅ 클라이언트 연결됨: {client.Client.RemoteEndPoint}");
                _ = HandleClientAsync(client);
            }
        });
    }
    private async Task HandleClientAsync(TcpClient client)
    {
        try
        {
            var stream = client.GetStream();
            var reader = new StreamReader(stream, Encoding.UTF8);
            Console.WriteLine("메시지받는주웅");
            string message = await reader.ReadLineAsync();
            Console.WriteLine($"📥수신된 메시지: {message}");

            JObject tmp;
            try
            {
                tmp = JObject.Parse(message);
                if ((string)tmp["action"] == "get_orders")
                {
                    this.stream_pay = stream;
                    this.payClient = client; // 클라이언트도 저장

                    Console.WriteLine("결제 키오스크 연결완료");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ 받은 데이터가 JSON가 아니에요 : {ex.Message}");
                OnSignalReceivedText?.Invoke(message);
                return;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 클라이언트 처리 중 오류 발생: {ex.GetType()} - {ex.Message}");
        }
    }
    public NetworkStream connectedStream
    {
        get; set;
    }
}
