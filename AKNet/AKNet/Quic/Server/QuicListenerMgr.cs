using AKNet.Common;
using System.Net;
using System.Net.Mail;
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
                var mQuicListener = await QuicListener.ListenAsync(GetQuicListenerOptions(mIPAddress, nPort));
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

        private QuicListenerOptions GetQuicListenerOptions(IPAddress mIPAddress, int nPort)
        {
            var ApplicationProtocols = new List<SslApplicationProtocol>();
            ApplicationProtocols.Add(SslApplicationProtocol.Http2);

            QuicListenerOptions mOption = new QuicListenerOptions();
            mOption.ListenEndPoint = new IPEndPoint(mIPAddress, nPort);
            mOption.ApplicationProtocols = ApplicationProtocols;
            mOption.ConnectionOptionsCallback = ConnectionOptionsCallback;
            return mOption;
        }

        private async ValueTask<QuicServerConnectionOptions> ConnectionOptionsCallback(QuicConnection mQuicConnection, SslClientHelloInfo mSslClientHelloInfo, CancellationToken mCancellationToken)
        {
            string path =  "server.pfx";
            var serverCertificate = X509Certificate2.CreateFromEncryptedPemFile(path, "123456");
            //var serverCertificate = X509CertificateLoader.LoadCertificateFromFile("path/to/cert.pfx");
            QuicServerConnectionOptions serverConnectionOptions = new QuicServerConnectionOptions();
            var ApplicationProtocols = new List<SslApplicationProtocol>();
            ApplicationProtocols.Add(SslApplicationProtocol.Http3);

            var ServerAuthenticationOptions = new SslServerAuthenticationOptions();
            ServerAuthenticationOptions.ApplicationProtocols = ApplicationProtocols;
            ServerAuthenticationOptions.ServerCertificate = serverCertificate;
            serverConnectionOptions.ServerAuthenticationOptions = ServerAuthenticationOptions;
            return await ValueTask.FromResult(serverConnectionOptions);
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
