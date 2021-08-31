using KCPNET;
using KCPProtocol;
using System;
using System.Collections.Generic;
using System.Text;

namespace KCPServer
{
    class ServerStart
    {
        static void Main(string[] args)
        {
            string ip = "192.168.1.101";
            KCPNet<ServerSession, NetMessage> server = new KCPNet<ServerSession, NetMessage>();
            server.StartAsServer(ip, 6666);

            while (true)
            {
                string ipt = Console.ReadLine();
                if (ipt == "quit")
                {
                    server.CloseServer();
                    break;
                }
                else
                {
                    server.BroadCastMsg(new NetMessage { info = ipt });
                }
            }

            Console.ReadKey();
        }
    }
}
