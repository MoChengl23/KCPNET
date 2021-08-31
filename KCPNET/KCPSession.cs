
using System;
using System.Net;
using System.Net.Sockets.Kcp;
using System.Threading;
using System.Threading.Tasks;

namespace KCPNET
{
    

   

    public enum SessionState
    {
        None,
        Connected,
        DisConnected
    }


    public abstract class KCPSession<T> where T : KCPMessage, new()
    {
        /// <summary>
        /// input先将数据输入kcp处理
        /// send发送出去
        /// recv接受数据
        /// update监听是否有新数据发过来
        /// </summary>
        /// 
        public Kcp m_kcp;
        protected uint m_sid;
        Action<byte[], IPEndPoint> m_udpSender;
        private IPEndPoint m_remotePoint;

        protected SessionState m_sessionState = SessionState.None;
        public bool IsConnected { get { return m_sessionState == SessionState.Connected; } }



        public KCPHandle m_handle;

        private CancellationTokenSource cancellationTokenSource;
        private CancellationToken cancellationToken;
        /// <summary>
        /// <para> sid = 区分会话标识</para>
        /// <para>udpsender = 发送的udp</para>
        /// ip = 玩家的ip
        /// </summary>
        /// <param name="sid"></param>
        /// <param name="udpSender"></param>
        /// <param name="remotePoint"></param>
        public void InitSession(uint sid, Action<byte[], IPEndPoint> udpSender, IPEndPoint remotePoint)
        {
            /// <summary>
            /// public partial class KCPNET
            /// </summary>
            m_sid = sid;
            m_udpSender = udpSender;
            m_remotePoint = remotePoint;
            m_sessionState = SessionState.Connected;


            m_handle = new KCPHandle();
            m_kcp = new Kcp(sid, m_handle);
            m_kcp.NoDelay(1, 10, 2, 1);
            m_kcp.WndSize(64, 64);
            m_kcp.SetMtu(512);

            m_handle.Out = (Memory<byte> buffer) =>
            {
                byte[] bytes = buffer.ToArray();
                m_udpSender(bytes, m_remotePoint);
            };
            m_handle.Recv = (byte[] buffer) =>
            {
                buffer = KCPSerialize.DeCompress(buffer);
                T message = KCPSerialize.DeSerialize<T>(buffer);
                if (message != null)
                {
                    OnReciveMessage(message);
                }
                OnConnected();
            };

            cancellationTokenSource = new CancellationTokenSource();
            cancellationToken = cancellationTokenSource.Token;
            Task.Run(Update, cancellationToken);
        }

        public void InputDataToKCP(byte[] buffer)
        {
            Console.WriteLine("Input data into KCP" );
            m_kcp.Input(buffer.AsSpan());
        }
        async void Update()
        {
            try
            {
                while (true)
                {
                    DateTime now = DateTime.UtcNow;
                    OnUpdate(now);
                    if (cancellationToken.IsCancellationRequested) break;
                    else
                    {
                        m_kcp.Update(now);
                        int len;
                        while ((len = m_kcp.PeekSize()) > 0)
                        {
                            var buffer = new byte[len];
                            if (m_kcp.Recv(buffer) >= 0)
                            {
                               
                                m_handle.Recive(buffer);
                            }
                        }
                        await Task.Delay(10);
                    }
                }
            }
            catch (Exception e)
            {

            }
        }


        public void SendMessage(T message)
        {
            if (IsConnected)
            {
                byte[] bytes = KCPSerialize.Serialize(message);
                bytes = KCPSerialize.Compress(bytes);
                m_kcp.Send(bytes.AsSpan());
            }
        }
        public Action<uint> OnSessionClose;
        public void CloseSession()
        {
            cancellationTokenSource.Cancel();
            OnDisConnected();


            OnSessionClose?.Invoke(m_sid);
            OnSessionClose = null;

            m_sessionState = SessionState.DisConnected;
            m_remotePoint = null;
            m_udpSender = null;
            m_sid = 0;

            m_handle = null;
            m_kcp = null;
            cancellationTokenSource = null;
        }

        /// <summary>
        /// kcp接受从UDP传过来的数据
        /// </summary>
        /// <param name="buffer"></param>
        //  public void ReceiveData(byte[] buffer)
        //  {
        //    m_kcp.Input(buffer.AsSpan());
        // }

        protected abstract void OnConnected();
        protected abstract void OnDisConnected();
        protected abstract void OnUpdate(DateTime now);
        protected abstract void OnReciveMessage(T message);

        public override int GetHashCode()
        {
            return m_sid.GetHashCode();
        }
        public uint GetSessionID()
        {
            return m_sid;
        }


    }
    }
