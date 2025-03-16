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
                mQuicListener = await QuicListener.ListenAsync(options);
                NetLog.Log("服务器 初始化成功: " + mIPAddress + " | " + nPort);
                StartProcessAccept();
            }
            catch (Exception e)
            {
                this.mState = SOCKET_SERVER_STATE.EXCEPTION;
                NetLog.LogError(e.ToString());
            }
        }

        private static QuicListenerOptions GetQuicListenerOptions(IPAddress mIPAddress, int nPort)
        {
            var ApplicationProtocols = new List<SslApplicationProtocol>();
            ApplicationProtocols.Add(SslApplicationProtocol.Http11);
            ApplicationProtocols.Add(SslApplicationProtocol.Http2);
            ApplicationProtocols.Add(SslApplicationProtocol.Http3);

            QuicListenerOptions mOption = new QuicListenerOptions();
            mOption.ListenEndPoint = new IPEndPoint(mIPAddress, nPort);
            mOption.ApplicationProtocols = ApplicationProtocols;
            mOption.ConnectionOptionsCallback = ConnectionOptionsCallback;
            return mOption;
        }

        private static ValueTask<QuicServerConnectionOptions> ConnectionOptionsCallback(QuicConnection mQuicConnection, SslClientHelloInfo mSslClientHelloInfo, CancellationToken mCancellationToken)
        {
            string hash = "5F375C6C1F53F9B0E669462D4F4D41CF592CAED1";
            var mCert = X509CertTool.GetCert();

            mCert = X509CertificateLoader.LoadCertificateFromFile("D:\\Me\\OpenSource\\AKNet2\\cert.pfx");
            NetLog.Assert(mCert != null, "GetCert() == null");
            mCert = null;

            var ApplicationProtocols = new List<SslApplicationProtocol>();
            ApplicationProtocols.Add(SslApplicationProtocol.Http11);
            ApplicationProtocols.Add(SslApplicationProtocol.Http2);
            ApplicationProtocols.Add(SslApplicationProtocol.Http3);

            var ServerAuthenticationOptions = new SslServerAuthenticationOptions();
            ServerAuthenticationOptions.ApplicationProtocols = ApplicationProtocols;
            ServerAuthenticationOptions.ServerCertificate = mCert;
            ServerAuthenticationOptions.RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;
            
            QuicServerConnectionOptions serverConnectionOptions = new QuicServerConnectionOptions();
            serverConnectionOptions.ServerAuthenticationOptions = ServerAuthenticationOptions;
            serverConnectionOptions.DefaultCloseErrorCode = 0x0A;
            serverConnectionOptions.DefaultStreamErrorCode = 0x0B;
            return ValueTask.FromResult(serverConnectionOptions);
        }

        private async void StartProcessAccept()
        {
            while (mQuicListener != null)
            {
                try
                {
                    QuicConnection connection = await mQuicListener.AcceptConnectionAsync();
                    mQuicServer.mClientPeerManager.MultiThreadingHandleConnectedSocket(connection);
                }
                catch (Exception e)
                {
                    NetLog.LogError(e.ToString());
                }
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
