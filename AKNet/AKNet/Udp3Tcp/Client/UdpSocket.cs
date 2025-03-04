﻿/************************************Copyright*****************************************
*        ProjectName:AKNet
*        Web:https://github.com/825126369/AKNet
*        Description:这是一个面向 .Net Standard 2.1 的游戏网络库
*        Author:阿珂
*        ModifyTime:2025/2/27 22:28:11
*        Copyright:MIT软件许可证
************************************Copyright*****************************************/
using AKNet.Common;
using AKNet.Udp3Tcp.Common;
using System;
using System.Net;
using System.Net.Sockets;

namespace AKNet.Udp3Tcp.Client
{
    internal class SocketUdp
    {
        private readonly SocketAsyncEventArgs ReceiveArgs;
        private readonly SocketAsyncEventArgs SendArgs;
        private readonly object lock_mSocket_object = new object();

        readonly AkCircularSpanBuffer mSendStreamList = null;
        private Socket mSocket = null;
        private IPEndPoint remoteEndPoint = null;
        private string ip;
        private int port;
        
        bool bReceiveIOContexUsed = false;
        bool bSendIOContexUsed = false;

        ClientPeer mClientPeer;
        public SocketUdp(ClientPeer mClientPeer)
        {
            this.mClientPeer = mClientPeer;
            mClientPeer.SetSocketState(SOCKET_PEER_STATE.NONE);

            mSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            mSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, 1);
            NetLog.Log("Default: ReceiveBufferSize: " + mSocket.ReceiveBufferSize);
            mSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveBuffer, mClientPeer.GetConfig().client_socket_receiveBufferSize);
            NetLog.Log("Fix ReceiveBufferSize: " + mSocket.ReceiveBufferSize);

            ReceiveArgs = new SocketAsyncEventArgs();
            ReceiveArgs.SetBuffer(new byte[Config.nUdpPackageFixedSize], 0, Config.nUdpPackageFixedSize);
            ReceiveArgs.Completed += ProcessReceive;

            SendArgs = new SocketAsyncEventArgs();
            SendArgs.SetBuffer(new byte[Config.nUdpPackageFixedSize], 0, Config.nUdpPackageFixedSize);
            SendArgs.Completed += ProcessSend;

            bReceiveIOContexUsed = false;
            bSendIOContexUsed = false;

            mSendStreamList = new AkCircularSpanBuffer();
        }

        public void ConnectServer(string ip, int nPort)
        {
            this.port = nPort;
            this.ip = ip;
            remoteEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);
            ReceiveArgs.RemoteEndPoint = remoteEndPoint;
            SendArgs.RemoteEndPoint = remoteEndPoint;

            ConnectServer();
            StartReceiveEventArg();
        }

        public void ConnectServer()
        {
            mClientPeer.mUDPLikeTCPMgr.SendConnect();
        }

        public void ReConnectServer()
        {
            mClientPeer.mUDPLikeTCPMgr.SendConnect();
        }

        public IPEndPoint GetIPEndPoint()
        {
            return remoteEndPoint;
        }

        public bool DisConnectServer()
        {
            var mSocketPeerState = mClientPeer.GetSocketState();
            if (mSocketPeerState == SOCKET_PEER_STATE.CONNECTED || mSocketPeerState == SOCKET_PEER_STATE.CONNECTING)
            {
                mClientPeer.mUDPLikeTCPMgr.SendDisConnect();
                return false;
            }
            else
            {
                return true;
            }
        }

        private void StartReceiveEventArg()
        {
            bool bIOSyncCompleted = false;
            if (mSocket != null)
            {
                try
                {
                    bIOSyncCompleted = !mSocket.ReceiveFromAsync(ReceiveArgs);
                }
                catch (Exception e)
                {
                    bReceiveIOContexUsed = false;
                    DisConnectedWithException(e);
                }
            }
            else
            {
                bReceiveIOContexUsed = false;
            }
            
            if (bIOSyncCompleted)
            {
                ProcessReceive(null, ReceiveArgs);
            }
        }

        private void StartSendEventArg()
        {
            bool bIOSyncCompleted = false;

            if (mSocket != null)
            {
                try
                {
                    bIOSyncCompleted = !mSocket.SendToAsync(SendArgs);
                }
                catch (Exception e)
                {
                    bSendIOContexUsed = false;
                    DisConnectedWithException(e);
                }
            }
            else
            {
                bSendIOContexUsed = false;
            }
                
            if (bIOSyncCompleted)
            {
                ProcessSend(null, SendArgs);
            }
        }

        private void ProcessReceive(object sender, SocketAsyncEventArgs e)
        {
            if (e.SocketError == SocketError.Success && e.BytesTransferred > 0)
            {
                mClientPeer.mMsgReceiveMgr.MultiThreading_ReceiveWaitCheckNetPackage(e);
            }
            
            StartReceiveEventArg();
        }

        private void ProcessSend(object sender, SocketAsyncEventArgs e)
        {
            if (e.SocketError == SocketError.Success)
            {
                SendNetStream2(e.BytesTransferred);
            }
            else
            {
                bSendIOContexUsed = false;
                DisConnectedWithSocketError(e.SocketError);
            }
        }

        public void SendNetPackage(NetUdpSendFixedSizePackage mPackage)
        {
            MainThreadCheck.Check();

            lock (mSendStreamList)
            {
                mSendStreamList.BeginSpan();
                mSendStreamList.WriteFrom(mClientPeer.GetCryptoMgr().EncodeHead(mPackage));
                mSendStreamList.WriteFrom(mPackage.WindowBuff, mPackage.WindowOffset, mPackage.WindowLength);
                mSendStreamList.FinishSpan();
            }

            if (!bSendIOContexUsed)
            {
                bSendIOContexUsed = true;
                SendNetStream2();
            }
        }

        int nLastSendBytesCount = 0;
        private void SendNetStream2(int BytesTransferred = -1)
        {
            if (BytesTransferred >= 0)
            {
                if (BytesTransferred != nLastSendBytesCount)
                {
                    NetLog.LogError("UDP 发生短写");
                }
            }

            var mSendArgSpan = SendArgs.Buffer.AsSpan();
            int nSendBytesCount = 0;
            lock (mSendStreamList)
            {
                nSendBytesCount += mSendStreamList.WriteToMax(mSendArgSpan);
            }

            if (nSendBytesCount > 0)
            {
                nLastSendBytesCount = nSendBytesCount;
                SendArgs.SetBuffer(0, nSendBytesCount);
                StartSendEventArg();
            }
            else
            {
                bSendIOContexUsed = false;
            }
        }

        public void DisConnectedWithNormal()
        {
            NetLog.Log("客户端 正常 断开服务器 ");
            mClientPeer.SetSocketState(SOCKET_PEER_STATE.DISCONNECTED);
        }

        private void DisConnectedWithException(Exception e)
        {
            if (mSocket != null)
            {
                NetLog.LogException(e);
            }
            DisConnectedWithError();
        }

        private void DisConnectedWithSocketError(SocketError e)
        {
            DisConnectedWithError();
        }

        private void DisConnectedWithError()
        {
            var mSocketPeerState = mClientPeer.GetSocketState();
            if (mSocketPeerState == SOCKET_PEER_STATE.DISCONNECTING)
            {
                mClientPeer.SetSocketState(SOCKET_PEER_STATE.DISCONNECTED);
            }
            else if (mSocketPeerState == SOCKET_PEER_STATE.CONNECTED || mSocketPeerState == SOCKET_PEER_STATE.CONNECTING)
            {
                mClientPeer.SetSocketState(SOCKET_PEER_STATE.RECONNECTING);
            }
        }

        private void CloseSocket()
        {
            if (mSocket != null)
            {
                Socket mSocket2 = mSocket;
                mSocket = null;

                try
                {
                    mSocket2.Close();
                }
                catch (Exception) { }
            }
        }

        public void Reset()
        {
            lock (mSendStreamList)
            {
                mSendStreamList.reset();
            }
        }

        public void Release()
        {
            DisConnectServer();
            CloseSocket();
            NetLog.Log("--------------- Client Release ----------------");
        }
    }
}









