using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;

namespace KCPNET
{
    public class KCPSerialize
    {
        public static byte[] Serialize<T>(T msg) where T : KCPMessage
        {
            using (MemoryStream ms = new MemoryStream())
            {
                try
                {
                    BinaryFormatter bf = new BinaryFormatter();
                    bf.Serialize(ms, msg);
                    ms.Seek(0, SeekOrigin.Begin);
                    return ms.ToArray();
                }
                catch (SerializationException e)
                {
                    Console.WriteLine("Failed to serialize.Reason:{0}", e.Message);
                    throw;
                }
            }
        }

        public static T DeSerialize<T>(byte[] bytes) where T : KCPMessage
        {
            using (MemoryStream ms = new MemoryStream(bytes))
            {
                try
                {
                    BinaryFormatter bf = new BinaryFormatter();
                    T msg = bf.Deserialize(ms) as T;
                    return msg;
                }
                catch (SerializationException e)
                {
                    Console.WriteLine("Failed to Deserialize.Reason:{0} bytesLen:{1}", e.Message, bytes.Length);
                    throw;
                }
            }
        }

        public static byte[] Compress(byte[] input)
        {
            using (MemoryStream outMS = new MemoryStream())
            {
                using (GZipStream gzs = new GZipStream(outMS, CompressionMode.Compress, true))
                {
                    gzs.Write(input, 0, input.Length);
                    gzs.Close();
                    return outMS.ToArray();
                }
            }
        }

        public static byte[] DeCompress(byte[] input)
        {
            using (MemoryStream inputMS = new MemoryStream(input))
            {
                using (MemoryStream outMs = new MemoryStream())
                {
                    using (GZipStream gzs = new GZipStream(inputMS, CompressionMode.Decompress))
                    {
                        byte[] bytes = new byte[1024];
                        int len = 0;
                        while ((len = gzs.Read(bytes, 0, bytes.Length)) > 0)
                        {
                            outMs.Write(bytes, 0, len);
                        }
                        gzs.Close();
                        return outMs.ToArray();
                    }
                }
            }
        }
    }
    [Serializable]
    public abstract class KCPMessage {
       

    }
    public class KCPNet<T,K> 
        where T: KCPSession<K> ,new() 
        where K:KCPMessage, new()
    {
        //用于中止线程池的任务
        private CancellationTokenSource cancellationTokenSource;
        private CancellationToken cancellationToken;
        public KCPNet()
        {
            cancellationTokenSource = new CancellationTokenSource();
            cancellationToken = cancellationTokenSource.Token;
        }
        UdpClient udp;
        IPEndPoint remotePoint;

        #region Server
        private Dictionary<uint, T> sessionDic = null;
        public void StartAsServer(string ip, int port)
        {
            sessionDic = new Dictionary<uint, T>();
            udp = new UdpClient(new IPEndPoint(IPAddress.Parse(ip),port));
            remotePoint = new IPEndPoint(IPAddress.Parse(ip), port);
            Console.WriteLine("服务器准备就绪");
            Task.Run(ServerReceive);

        }
        async void ServerReceive()
        {
            UdpReceiveResult result;
            while (true)
            {
                if (cancellationToken.IsCancellationRequested) break;
                try
                {
                    result = await udp.ReceiveAsync();
                   
                   
                        uint sid = BitConverter.ToUInt32(result.Buffer, 0);
                        if (sid == 0)
                        {
                            //给新连接的客户端分配一个全局唯一的sid；
                            sid = GenerateUniqueSessionID();
                            byte[] sid_bytes = BitConverter.GetBytes(sid);
                            byte[] conv_bytes = new byte[8];
                            Array.Copy(sid_bytes, 0, conv_bytes, 4, 4);
                            SendUDPMsg(conv_bytes, result.RemoteEndPoint);

                        }
                        else
                        {
                            if (!sessionDic.TryGetValue(sid, out T session))
                            {
                                session = new T();
                                session.InitSession(sid, SendUDPMsg, result.RemoteEndPoint);
                                session.OnSessionClose = OnServerSessionClose;
                                lock (sessionDic)
                                {
                                    sessionDic.Add(sid, session);
                                }
                            }
                            else
                            {
                                session = sessionDic[sid];
                            }
                            session.InputDataToKCP(result.Buffer);
                        }
                   
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
            }
        }
        public void CloseServer()
        {
            foreach (var item in sessionDic)
            {
                item.Value.CloseSession();
            }
            sessionDic = null;

            if (udp != null)
            {
                udp.Close();
                udp = null;
                cancellationTokenSource.Cancel();
            }
        }

        /// <summary>
        /// 一个连接关闭时，删除字典的键
        /// </summary>
        /// <param name="sid"></param>
        void OnServerSessionClose(uint sid)
        {
            if (sessionDic.ContainsKey(sid))
            {
                lock (sessionDic)
                {
                    sessionDic.Remove(sid);
                    Console.WriteLine("Session:{0} remove from sessionDic.", sid);
                }
            }
            else
            {
                Console.WriteLine("Session:{0} cannot find in sessionDic", sid);
            }
        }

        #endregion

        #region Client
        public T clientSession;

        public void StartAsClient(string ip, int port)
        {
            udp = new UdpClient(0);
            remotePoint = new IPEndPoint(IPAddress.Parse(ip), port);
            Console.WriteLine("客户端准备就绪");
            Task.Run(ClientReceive);
        }
        /// <summary>
        /// 发送四个数。若全是0则表示一个新连接
        /// <para>ConnectServer，即第一次连接，是通过UDP发送，具有不确定性，于是检测发送知道确定连接上了为止</para>
        /// <para>interval表示连接的间隔时间</para>
        /// <para>maxintervalSum表示最大尝试时间</para>
        /// </summary>
        public Task<bool> ConnectServer(int interval, int maxintervalSum = 5000)
        {
            SendUDPMsg(new byte[4], remotePoint);
            int checkTimes = 0;
            //不停地发送连接包，直到IsConnected为true
            Task<bool> task = Task.Run(async () => {
                while (true)
                {
                    await Task.Delay(interval);
                    checkTimes += interval;
                    if (clientSession != null && clientSession.IsConnected)
                    {
                        return true;
                    }
                    else
                    {
                        if (checkTimes > maxintervalSum)
                        {
                            return false;
                        }
                    }
                }
            });
            return task;
        }

        /// <summary>
        /// 收到udp数据
        /// </summary>
        async void ClientReceive()
        {
            UdpReceiveResult result;
            while (true)
            {
                if (cancellationToken.IsCancellationRequested) break;
                try
                {
                    result = await udp.ReceiveAsync();
                    if(Equals(result.RemoteEndPoint, remotePoint))
                    {
                        uint sid =  BitConverter.ToUInt32(result.Buffer, 0);
                        if(sid == 0)
                        {
                            //判断下是否是新连接
                             if(clientSession != null && clientSession.IsConnected)
                            {

                            }
                            else//新连接第一次建立
                            {
                                sid = BitConverter.ToUInt32(result.Buffer, 4);
                                Console.WriteLine("新连接建立");
                                clientSession = new T();
                                clientSession.InitSession(sid, SendUDPMsg, remotePoint);

                            }
                        }
                        else
                        {
                            //信息输入进kcp处理
                            if (clientSession != null && clientSession.IsConnected)
                                clientSession.InputDataToKCP(result.Buffer);
                        }
                    }
                    else
                    {
                        Console.WriteLine("收到错误的信息，该信息不属于我232");
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
            }
        }
        public void ClientClose()
        {
            cancellationTokenSource.Cancel();
            if (udp != null)
            {
                udp.Close();
                udp = null;
            }

            if(clientSession != null)
            {
                clientSession.CloseSession();
            }
        }
        #endregion

        void SendUDPMsg(byte[] bytes, IPEndPoint remotePoint)
        {
            if (udp != null)
            {
                Console.WriteLine("SendUDP");
                udp.SendAsync(bytes, bytes.Length, remotePoint);
            }
        }

        private uint sid = 0;
        public uint GenerateUniqueSessionID()
        {
            lock (sessionDic)
            {
                while (true)
                {
                    ++sid;
                    if (sid == uint.MaxValue)
                    {
                        sid = 1;
                    }
                    if (!sessionDic.ContainsKey(sid))
                    {
                        break;
                    }
                }
            }
            return sid;
        }
        public void BroadCastMsg(K message)
        {
            byte[] bytes = KCPSerialize.Serialize(message);
            foreach (var item in sessionDic)
            {
                item.Value.SendMessage(message);
            }
        }

    }
}
