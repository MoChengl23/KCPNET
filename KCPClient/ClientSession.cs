
using KCPProtocol;
using KCPNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KCPClient
{
    class ClientSession : KCPSession<NetMessage>
    {
        protected override void OnConnected()
        {
        }

        protected override void OnDisConnected()
        {
        }

        protected override void OnReciveMessage(NetMessage msg)
        {
            Console.WriteLine("Sid:{0},RcvServer:{1}", m_sid, msg.info);
        }

        protected override void OnUpdate(DateTime now)
        {
        }
    }
}
