
using KCPNET;
using System;
using KCPProtocol;
using System.Threading.Tasks;

namespace KCPClient
{
    public class ClientStart
    {
        static KCPNet<ClientSession, NetMessage> client;
        static Task<bool> checkTask = null;
        static void Main(string[] args)
        {
            string ip = "192.168.1.101";
            client = new KCPNet<ClientSession, NetMessage>();
            client.StartAsClient(ip, 6666);
            checkTask = client.ConnectServer(200, 5000);
            Task.Run(ConnectCheck);

            while (true)
            {
                string ipt = Console.ReadLine();
                if (ipt == "quit")
                {
                    client.ClientClose();
                    break;
                }
                else
                {
                    client.clientSession.SendMessage(new NetMessage
                    {
                        info = ipt
                    });
                }
            }

            Console.ReadKey();
        }

        private static int counter = 0;
        static async void ConnectCheck()
        {
            while (true)
            {
                await Task.Delay(3000);
                if (checkTask != null && checkTask.IsCompleted)
                {
                    if (checkTask.Result)
                    {
                        Console.WriteLine("ConnectServer Success.");
                        checkTask = null;
                        await Task.Run(SendPingMsg);
                    }
                    else
                    {
                        ++counter;
                        if (counter > 4)
                        {
                            Console.WriteLine("Connect Failed {0} Times,Check Your Network Connection.", counter);
                            checkTask = null;
                            break;
                        }
                        else
                        {
                            Console.WriteLine("Connect Faild {0} Times.Retry...", counter);
                            checkTask = client.ConnectServer(200, 5000);
                        }
                    }
                }
            }
        }

        static async void SendPingMsg()
        {
            while (true)
            {
                await Task.Delay(5000);
                if (client != null && client.clientSession != null)
                {
                    client.clientSession.SendMessage(new NetMessage
                    {
                        cmd = CMD.NetPing,
                        netPing = new NetPing
                        {
                            IsOver = false
                        }
                    });
                    Console.WriteLine("Client Send Ping Message.");
                }
                else
                {
                    Console.WriteLine("Ping Task Cancel");
                    break;
                }
            }
        }
    }
}
