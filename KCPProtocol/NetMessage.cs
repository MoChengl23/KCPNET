using System;
using KCPNET;

namespace KCPProtocol
{
    public enum CMD
    {
        None,
        Relogin,
        NetPing,
    }
    [Serializable]
    public class NetMessage : KCPMessage
    {
        public string info;
        public NetPing netPing;
        public Relogin relogin;
        public CMD cmd;
    }

    [Serializable]
    public class NetPing 
    {
        public bool IsOver;
    }

    [Serializable]
    public class Relogin
    {
        public string accout;
        public string passord;
    }
}
