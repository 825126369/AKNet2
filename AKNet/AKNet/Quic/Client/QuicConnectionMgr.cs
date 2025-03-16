/************************************Copyright*****************************************
*        ProjectName:AKNet
*        Web:https://github.com/825126369/AKNet
*        Description:这是一个面向 .Net Standard 2.1 的游戏网络库
*        Author:阿珂
*        ModifyTime:2025/2/27 22:28:11
*        Copyright:MIT软件许可证
************************************Copyright*****************************************/
using AKNet.Common;
using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace AKNet.Quic.Client
{
    internal class QuicConnectionMgr
	{
        private const byte DefaultCloseErrorCode = 0x0A;
        private const byte DefaultStreamErrorCode = 0x0B;

        private Memory<byte> mReceiveBuffer = new byte[1024];
        private Memory<byte> mSendBuffer = new byte[1024];

        private QuicConnection mQuicConnection = null;
		private string ServerIp = "";
		private int nServerPort = 0;
		private IPEndPoint mIPEndPoint = null;
        private ClientPeer mClientPeer;

        public QuicConnectionMgr(ClientPeer mClientPeer)
		{
			this.mClientPeer = mClientPeer;
            mClientPeer.SetSocketState(SOCKET_PEER_STATE.NONE);
        }

		public void ReConnectServer()
		{
            ConnectServer(this.ServerIp, this.nServerPort);
        }

		public async void ConnectServer(string ServerAddr, int ServerPort)
		{
			this.ServerIp = ServerAddr;
			this.nServerPort = ServerPort;

			mClientPeer.SetSocketState(SOCKET_PEER_STATE.CONNECTING);
			NetLog.Log("Client 正在连接服务器: " + this.ServerIp + " | " + this.nServerPort);

			Reset();
			if (!QuicConnection.IsSupported)
			{
				NetLog.LogError("QUIC is not supported.");
				return;
			}

			if (mIPEndPoint == null)
			{
				IPAddress mIPAddress = IPAddress.Parse(ServerAddr);
				mIPEndPoint = new IPEndPoint(mIPAddress, ServerPort);
			}

            try
            {
                mQuicConnection = await QuicConnection.ConnectAsync(GetQuicClientConnectionOptions(mIPEndPoint));
                mClientPeer.SetSocketState(SOCKET_PEER_STATE.CONNECTED);

                NetLog.Log("Client 连接服务器成功: " + this.ServerIp + " | " + this.nServerPort);
                StartProcessReceive();
            }
            catch (Exception e)
            {
                NetLog.LogError(e.ToString());
                mClientPeer.SetSocketState(SOCKET_PEER_STATE.RECONNECTING);
            }
		}

        private QuicClientConnectionOptions GetQuicClientConnectionOptions(IPEndPoint mIPEndPoint)
        {
            var mCert = X509CertTool.GetCert();
            NetLog.Assert(mCert != null, "GetCert() == null");
            var ApplicationProtocols = new List<SslApplicationProtocol>();
            ApplicationProtocols.Add(SslApplicationProtocol.Http11);
            ApplicationProtocols.Add(SslApplicationProtocol.Http2);
            ApplicationProtocols.Add(SslApplicationProtocol.Http3);

            QuicClientConnectionOptions mOption = new QuicClientConnectionOptions();
            mOption.RemoteEndPoint = mIPEndPoint;
            mOption.DefaultCloseErrorCode = DefaultCloseErrorCode;
            mOption.DefaultStreamErrorCode = DefaultStreamErrorCode;
            mOption.ClientAuthenticationOptions = new SslClientAuthenticationOptions();
            mOption.ClientAuthenticationOptions.ApplicationProtocols = ApplicationProtocols;
            mOption.ClientAuthenticationOptions.RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;
            return mOption;
        }

        public bool DisConnectServer()
		{
			MainThreadCheck.Check();
            DisConnectServer2();
            return true;
		}

        private async void DisConnectServer2()
        {
            NetLog.Log("客户端 主动 断开服务器 Begin......");
            await mQuicConnection.CloseAsync(DefaultCloseErrorCode);
            NetLog.Log("客户端 主动 断开服务器 Finish......");
        }

        private async void StartProcessReceive()
		{
            while (mQuicConnection != null)
            {
                QuicStream mQuicStream = await mQuicConnection.AcceptInboundStreamAsync();
                if (mQuicStream != null)
                {
                    int nLength = await mQuicStream.ReadAsync(mReceiveBuffer);
                    mClientPeer.mMsgReceiveMgr.MultiThreadingReceiveSocketStream(mReceiveBuffer.Span.Slice(0, nLength));
                }
            }
        }

        public async void SendNetStream(ReadOnlyMemory<byte> mBufferSegment)
        {
            try
            {
                var stream = await mQuicConnection.OpenOutboundStreamAsync(QuicStreamType.Bidirectional);
                await stream.WriteAsync(mBufferSegment);
            }
            catch (Exception)
            {
                mClientPeer.SetSocketState(SOCKET_PEER_STATE.DISCONNECTED);
            }
        }

        private void DisConnectedWithError()
        {
            var mSocketPeerState = mClientPeer.GetSocketState();
            if (mSocketPeerState == SOCKET_PEER_STATE.DISCONNECTING)
            {
                mClientPeer.SetSocketState(SOCKET_PEER_STATE.DISCONNECTED);
            }
            else if (mSocketPeerState == SOCKET_PEER_STATE.CONNECTED)
            {
                mClientPeer.SetSocketState(SOCKET_PEER_STATE.RECONNECTING);
            }
        }

		public IPEndPoint GetIPEndPoint()
		{
            IPEndPoint mRemoteEndPoint = null;
            try
            {
                if (mQuicConnection != null && mQuicConnection.RemoteEndPoint != null)
                {
                    mRemoteEndPoint = mQuicConnection.RemoteEndPoint as IPEndPoint;
                }
            }
            catch { }

            return mRemoteEndPoint;
        }

		private async void CloseSocket()
		{
            if (mQuicConnection != null)
            {
                QuicConnection mQuicConnection2 = mQuicConnection;
                mQuicConnection = null;
				await mQuicConnection2.CloseAsync(DefaultCloseErrorCode);
            }
        }

		public void Reset()
		{
            //CloseSocket();
        }

		public void Release()
		{
            CloseSocket();
        }
    }
}
