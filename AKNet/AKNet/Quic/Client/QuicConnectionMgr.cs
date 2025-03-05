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

namespace AKNet.Quic.Client
{
    internal class QuicConnectionMgr
	{
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

			var clientOptions = new QuicClientConnectionOptions
			{
				RemoteEndPoint = mIPEndPoint
			};

			mQuicConnection = await QuicConnection.ConnectAsync(clientOptions);
			if (mQuicConnection == null)
			{
				mClientPeer.SetSocketState(SOCKET_PEER_STATE.RECONNECTING);
			}
			else
			{
				mClientPeer.SetSocketState(SOCKET_PEER_STATE.CONNECTED);
				StartProcessReceive();

            }
		}

		public bool DisConnectServer()
		{
			NetLog.Log("客户端 主动 断开服务器 Begin......");
			MainThreadCheck.Check();
			mQuicConnection.DisposeAsync();
			return true;
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
			var stream = await mQuicConnection.OpenOutboundStreamAsync(QuicStreamType.Bidirectional);
			await stream.WriteAsync(mBufferSegment);
			stream.CompleteWrites();
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

		private async Task CloseSocket()
		{
            if (mQuicConnection != null)
            {
                QuicConnection mQuicConnection2 = mQuicConnection;
                mQuicConnection = null;
				await mQuicConnection2.DisposeAsync();
            }
        }

		public void Reset()
		{
            CloseSocket();
		}

		public void Release()
		{
            CloseSocket();
        }
    }
}
