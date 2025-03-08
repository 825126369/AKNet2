using AKNet.Common;
using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace AKNet.Quic.Server
{
    internal class QuicListenerMgr
    {
        QuicListener mQuicListener = null;
        QuicServer mQuicServer = null;
        private SOCKET_SERVER_STATE mState = SOCKET_SERVER_STATE.NONE;
        private int nPort;

        public QuicListenerMgr(QuicServer mQuicServer)
        {
            this.mQuicServer = mQuicServer;
        }

        public void InitNet()
        {
            List<int> mPortList = IPAddressHelper.GetAvailableTcpPortList();
            int nTryBindCount = 100;
            while (nTryBindCount-- > 0)
            {
                if (mPortList.Count > 0)
                {
                    int nPort = mPortList[RandomTool.RandomArrayIndex(0, mPortList.Count)];
                    InitNet(nPort);
                    mPortList.Remove(nPort);
                    if (GetServerState() == SOCKET_SERVER_STATE.NORMAL)
                    {
                        break;
                    }
                }
            }

            if (GetServerState() != SOCKET_SERVER_STATE.NORMAL)
            {
                NetLog.LogError("Udp Server 自动查找可用端口 失败！！！");
            }
        }

        public void InitNet(int nPort)
        {
            InitNet(IPAddress.Any, nPort);
        }

        public void InitNet(string Ip, int nPort)
        {
            InitNet(IPAddress.Parse(Ip), nPort);
        }

        private async void InitNet(IPAddress mIPAddress, int nPort)
        {
            if (!QuicListener.IsSupported)
            {
                NetLog.LogError("QUIC is not supported.");
                return;
            }

            this.nPort = nPort;
            this.mState = SOCKET_SERVER_STATE.NORMAL;

            try
            {
                var options = GetQuicListenerOptions(mIPAddress, nPort);
                var mQuicListener = await QuicListener.ListenAsync(options);
                if (mQuicListener != null)
                {
                    StartProcessAccept();
                }
                else
                {
                   
                }
            }
            catch (QuicException e)
            {
                this.mState = SOCKET_SERVER_STATE.EXCEPTION;
                NetLog.LogError(e.QuicError + " | " + e.StackTrace);
            }
            catch (Exception e)
            {
                this.mState = SOCKET_SERVER_STATE.EXCEPTION;
                NetLog.LogError(e.Message + " | " + e.StackTrace);
            }
        }

        private static QuicListenerOptions GetQuicListenerOptions(IPAddress mIPAddress, int nPort)
        {
            var ApplicationProtocols = new List<SslApplicationProtocol>();
            ApplicationProtocols.Add(new SslApplicationProtocol("test"));

            QuicListenerOptions mOption = new QuicListenerOptions();
            mOption.ListenEndPoint = new IPEndPoint(IPAddress.Loopback, 12345);
            mOption.ApplicationProtocols = ApplicationProtocols;
            mOption.ConnectionOptionsCallback = ConnectionOptionsCallback;
            mOption.ListenBacklog = 10;
            return mOption;
        }

        private static ValueTask<QuicServerConnectionOptions> ConnectionOptionsCallback(QuicConnection mQuicConnection, SslClientHelloInfo mSslClientHelloInfo, CancellationToken mCancellationToken)
        {
            QuicServerConnectionOptions serverConnectionOptions = new QuicServerConnectionOptions();
            var ApplicationProtocols = new List<SslApplicationProtocol>();
            ApplicationProtocols.Add(SslApplicationProtocol.Http3);
            ApplicationProtocols.Add(SslApplicationProtocol.Http11);
            ApplicationProtocols.Add(SslApplicationProtocol.Http2);

            var ServerAuthenticationOptions = new SslServerAuthenticationOptions();
            ServerAuthenticationOptions.ApplicationProtocols = ApplicationProtocols;
            ServerAuthenticationOptions.ServerCertificate = X509CertTool.GetCert();
            serverConnectionOptions.ServerAuthenticationOptions = ServerAuthenticationOptions;
            serverConnectionOptions.DefaultCloseErrorCode = 0x0A;
            serverConnectionOptions.DefaultStreamErrorCode = 0x0B;

            return ValueTask.FromResult(serverConnectionOptions);
        }

        private async void StartProcessAccept()
        {
            while (mQuicListener != null)
            {
                QuicConnection connection = await mQuicListener.AcceptConnectionAsync();
                mQuicServer.mClientPeerManager.MultiThreadingHandleConnectedSocket(connection);
            }
        }

        public int GetPort()
        {
            return this.nPort;
        }

        public SOCKET_SERVER_STATE GetServerState()
        {
            return mState;
        }

        public async void CloseNet()
        {
            MainThreadCheck.Check();
            if (mQuicListener != null)
            {
                var mQuicListener2 = mQuicListener;
                mQuicListener = null;
                await mQuicListener2.DisposeAsync();
            }
        }
    }

}
