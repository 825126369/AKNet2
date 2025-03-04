﻿/************************************Copyright*****************************************
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
using System.Net.Sockets;

namespace AKNet.Quic.Server
{
    internal class ClientPeerSocketMgr
	{
        private Memory<byte> mReceiveBuffer = new byte[1024];
        private Memory<byte> mSendBuffer = new byte[1024];

        private readonly AkCircularBuffer mSendStreamList = new AkCircularBuffer();
		private QuicConnection mQuicConnection;
        private ClientPeer mClientPeer;
		private QuicServer mQuicServer;
		
		public ClientPeerSocketMgr(ClientPeer mClientPeer, QuicServer mQuicServer)
		{
			this.mClientPeer = mClientPeer;
			this.mQuicServer = mQuicServer;
			mClientPeer.SetSocketState(SOCKET_PEER_STATE.DISCONNECTED);
		}

		public void HandleConnectedSocket(QuicConnection connection)
		{
			MainThreadCheck.Check();

			this.mQuicConnection = connection;
			this.mClientPeer.SetSocketState(SOCKET_PEER_STATE.CONNECTED);

            StartProcessReceive();
		}

        public IPEndPoint GetIPEndPoint()
        {
			IPEndPoint mRemoteEndPoint = null;
            if (mQuicConnection != null)
            {
                mRemoteEndPoint = mQuicConnection.RemoteEndPoint as IPEndPoint;
            }
            return mRemoteEndPoint;
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
			QuicStream mStream = await mQuicConnection.OpenOutboundStreamAsync(QuicStreamType.Bidirectional);
			await mStream.WriteAsync(mBufferSegment);
        }

        private void DisConnectedWithNormal()
        {
            mClientPeer.SetSocketState(SOCKET_PEER_STATE.DISCONNECTED);
        }

        private void DisConnectedWithException(Exception e)
		{
			mClientPeer.SetSocketState(SOCKET_PEER_STATE.DISCONNECTED);
		}

		private void DisConnectedWithSocketError(SocketError mError)
		{
            mClientPeer.SetSocketState(SOCKET_PEER_STATE.DISCONNECTED);
        }

		private async void CloseSocket()
		{
			if (mQuicConnection != null)
			{
				var mQuicConnection2 = mQuicConnection;
				mQuicConnection = null;
				await mQuicConnection2.CloseAsync(0);
			}
		}

		public void Reset()
		{
			CloseSocket();
			lock (mSendStreamList)
			{
				mSendStreamList.reset();
			}
		}
	}

}