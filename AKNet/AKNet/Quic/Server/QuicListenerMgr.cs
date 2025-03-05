using AKNet.Common;
using System.Net;
using System.Net.Quic;
using System.Net.Security;

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
            mState = SOCKET_SERVER_STATE.NORMAL;

            var serverConnectionOptions = new QuicServerConnectionOptions
            {
                DefaultStreamErrorCode = 0x0A,
                DefaultCloseErrorCode = 0x0B,

                ServerAuthenticationOptions = new SslServerAuthenticationOptions
                {
                    ApplicationProtocols = [new SslApplicationProtocol("protocol-name")],
                   // ServerCertificate = serverCertificate
                }
            };

            mQuicListener = await QuicListener.ListenAsync(new QuicListenerOptions
            {
                ListenEndPoint = new IPEndPoint(mIPAddress, nPort),
                ApplicationProtocols = [new SslApplicationProtocol("protocol-name")],
                ConnectionOptionsCallback = (_, _, _) => ValueTask.FromResult(serverConnectionOptions)
            });
            StartProcessAccept();
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
