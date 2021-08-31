using KCPNET;
using KCPProtocol;
using System;
using System.Collections.Generic;
using System.Text;

namespace KCPServer
{
    class ServerSession : KCPSession<NetMessage>
    {
        protected override void OnConnected()
        {
        //    Console.WriteLine("Client Online,Sid:{0}", m_sid);
        }

        protected override void OnDisConnected()
        {
         //   Console.WriteLine("Client Offline,Sid:{0}", m_sid);
        }

        protected override void OnReciveMessage(NetMessage msg)
        {
           Console.WriteLine("Sid:{0},RcvClient,CMD:{1} {2}", m_sid, msg.cmd.ToString(), msg.info);
            if (msg.cmd == CMD.NetPing)
            {
                if (msg.netPing.IsOver)
                {
                    CloseSession();
                }
                else
                {
                    //收到ping请求，则重置检查计数，并回复ping消息到客户端
                    checkCounter = 0;
                    NetMessage pingMsg = new NetMessage
                    {
                        cmd = CMD.NetPing,
                        netPing = new NetPing
                        {
                            IsOver = false
                        }
                    };
                    SendMessage(pingMsg);
                }
            }
        }

        private int checkCounter;
        DateTime checkTime = DateTime.UtcNow.AddSeconds(5);
        protected override void OnUpdate(DateTime now)
        {
            if (now > checkTime)
            {
                checkTime = now.AddSeconds(5);
                checkCounter++;
                if (checkCounter > 3)
                {
                    NetMessage pingMsg = new NetMessage
                    {
                        cmd = CMD.NetPing,
                        netPing = new NetPing { IsOver = true }
                    };
                    OnReciveMessage(pingMsg);
                }
            }
        }
    }
}
