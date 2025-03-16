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

namespace AKNet.Quic.Server
{
    internal class ClientPeerSocketMgr
	{
        private readonly Memory<byte> mReceiveBuffer = new byte[1024];
        private readonly byte[] mSendBuffer = new byte[1024];
        CancellationTokenSource mCancellationTokenSource = new CancellationTokenSource();

        private readonly AkCircularBuffer mSendStreamList = new AkCircularBuffer();
        private bool bSendIOContextUsed = false;
        private QuicStream mSendQuicStream;

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
            mSendQuicStream = await mQuicConnection.OpenOutboundStreamAsync(QuicStreamType.Unidirectional);

            try
			{
				while (mQuicConnection != null)
				{
					QuicStream mQuicStream = await mQuicConnection.AcceptInboundStreamAsync();
                    if (mQuicStream != null)
                    {
                        while (true)
						{
                            int nLength = await mQuicStream.ReadAsync(mReceiveBuffer);
							if (nLength > 0)
							{
                                //NetLog.Log("Receive NetStream: " + nLength);
                                mClientPeer.mMsgReceiveMgr.MultiThreadingReceiveSocketStream(mReceiveBuffer.Span.Slice(0, nLength));
							}
							else
							{
								break;
                            }
                        }
					}
                }
			}
			catch (Exception e)
			{
				//NetLog.LogError(e.ToString());
				this.mClientPeer.SetSocketState(SOCKET_PEER_STATE.DISCONNECTED);
			}
        }

        public void SendNetStream(ReadOnlyMemory<byte> mBufferSegment)
        {
            lock (mSendStreamList)
            {
                mSendStreamList.WriteFrom(mBufferSegment.Span);
            }

            if (!bSendIOContextUsed)
            {
                bSendIOContextUsed = true;
                SendNetStream2();
            }
        }

        private async void SendNetStream2()
        {
            try
            {
                while (mSendStreamList.Length > 0)
                {
                    int nLength = 0;
                    lock (mSendStreamList)
                    {
                        nLength = mSendStreamList.WriteToMax(0, mSendBuffer, 0, mSendBuffer.Length);
                    }
                    await mSendQuicStream.WriteAsync(mSendBuffer, 0, nLength);
                }
                bSendIOContextUsed = false;
            }
            catch (Exception e)
            {
                //NetLog.LogError(e.ToString());
                mClientPeer.SetSocketState(SOCKET_PEER_STATE.DISCONNECTED);
            }
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