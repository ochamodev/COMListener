using System;
using System.IO.Ports;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
    

namespace COMListener
{
    internal class Program
    {
        static StringBuilder MessageBuilder = new StringBuilder();
        static string ApiUrl = "http://localhost:5000/received_router_message"; // Change this to your API endpoint
        private static TcpListener tcpListener;
        private static CancellationTokenSource cancellationTokenSource;
        private static SerialPort SerialPort;
        private static int SocketPort = 7070;

        static void Main(string[] args)
        {
            string comPort = "COM6"; // Change this to your COM port
            int baudRate = 115200;

            using (SerialPort = new SerialPort(comPort))
            {
                SerialPort.BaudRate = baudRate; // Change the baud rate if needed
                SerialPort.DataReceived += SerialPortDataReceived;
                cancellationTokenSource = new CancellationTokenSource();
                tcpListener = new TcpListener(IPAddress.Any, SocketPort);

                try
                {
                    Task.Run(() => StartSocketServer(cancellationTokenSource.Token));
                    SerialPort.Open();
                    Console.WriteLine($"Listening on {comPort}. Press any key to exit.");
                    Console.ReadKey();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error opening {comPort}: {ex.Message}");
                }
            }
        }

        private static async Task StartSocketServer(CancellationToken cancellationToken)
        {
            tcpListener.Start();

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    TcpClient tcpClient = await tcpListener.AcceptTcpClientAsync();
                    _ = Task.Run(() => HandleClient(tcpClient), cancellationToken);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in socket server: {ex.Message}");
            }
        }

        private static async Task HandleClient(TcpClient tcpClient)
        {
            try
            {
                using (NetworkStream stream = tcpClient.GetStream())
                {
                    byte[] buffer = new byte[4096];
                    int bytesRead;

                    while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        string receivedData = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        Console.WriteLine($"Received from client: {receivedData}");

                        // Send the received data through COM12
                        if (SerialPort.IsOpen)
                        {
                            SerialPort.Write(receivedData);
                            Console.WriteLine($"Sent to COM12: {receivedData}");
                        }
                        else
                        {
                            Console.WriteLine("COM12 port is not open. Make sure it is properly configured.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling client: {ex.Message}");
            }
            finally
            {
                tcpClient.Close();
            }
        }

        private static async void SerialPortDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            SerialPort serialPort = (SerialPort)sender;
            string receivedData = serialPort.ReadExisting();

            MessageBuilder.Append(receivedData);

            if (IsMessageComplete(MessageBuilder.ToString()))
            {
                await SendDataToApi(MessageBuilder.ToString());

                MessageBuilder.Clear();
            }

        }

        private static bool IsMessageComplete(string message)
        {
            return message.Contains("\n");
        }

        private static async Task SendDataToApi(string data)
        {
            // Console.WriteLine($"Mensaje {data}");

            var payload = new { message = data };
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(payload);

            using (var httpClient = new HttpClient())
            {
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await httpClient.PostAsync(ApiUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    //Console.WriteLine("Data sent successfully!");
                }
                else
                {
                    Console.WriteLine($"Error sending data: {response.StatusCode}");
                }
            }
        }

    }
}