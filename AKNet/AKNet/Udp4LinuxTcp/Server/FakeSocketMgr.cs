﻿/************************************Copyright*****************************************
*        ProjectName:AKNet
*        Web:https://github.com/825126369/AKNet
*        Description:这是一个面向 .Net Standard 2.1 的游戏网络库
*        Author:阿珂
*        ModifyTime:2025/2/27 22:28:11
*        Copyright:MIT软件许可证
************************************Copyright*****************************************/
using AKNet.Common;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace AKNet.Udp4LinuxTcp.Server
{
    internal class FakeSocketMgr
    {
        private UdpServer mNetServer = null;
        private readonly Dictionary<string, FakeSocket> mAcceptSocketDic = null;
        private readonly FakeSocketPool mFakeSocketPool = null;
        private readonly int nMaxPlayerCount = 0;

        public FakeSocketMgr(UdpServer mNetServer)
        {
            this.mNetServer = mNetServer;
            nMaxPlayerCount = mNetServer.GetConfig().MaxPlayerCount;
            mFakeSocketPool = new FakeSocketPool(mNetServer, nMaxPlayerCount, nMaxPlayerCount);
            mAcceptSocketDic = new Dictionary<string, FakeSocket>(nMaxPlayerCount);
        }

        public void MultiThreadingReceiveNetPackage(SocketAsyncEventArgs e)
        {
            IPEndPoint endPoint = (IPEndPoint)e.RemoteEndPoint;
            FakeSocket mFakeSocket = null;
            string nPeerId = endPoint.ToString();

            lock (mAcceptSocketDic)
            {
                mAcceptSocketDic.TryGetValue(nPeerId, out mFakeSocket);
            }

            if (mFakeSocket == null)
            {
                if (mAcceptSocketDic.Count >= nMaxPlayerCount)
                {
#if DEBUG
                    NetLog.Log($"服务器爆满, 客户端总数: {mAcceptSocketDic.Count}");
#endif
                }
                else
                {
                    mFakeSocket = mFakeSocketPool.Pop();
                    mFakeSocket.RemoteEndPoint = endPoint;
                    mNetServer.GetClientPeerMgr().MultiThreadingHandleConnectedSocket(mFakeSocket);

                    lock (mAcceptSocketDic)
                    {
                        mAcceptSocketDic.Add(nPeerId, mFakeSocket);
                    }

                    PrintAddFakeSocketMsg(mFakeSocket);
                }
            }

            if (mFakeSocket != null)
            {
                mFakeSocket.MultiThreadingReceiveNetPackage(e);
            }
        }

        public void RemoveFakeSocket(FakeSocket mFakeSocket)
        {
            string peerId = mFakeSocket.RemoteEndPoint.ToString();

            lock (mAcceptSocketDic)
            {
                mAcceptSocketDic.Remove(peerId);
            }

            mFakeSocketPool.recycle(mFakeSocket);
            PrintRemoveFakeSocketMsg(mFakeSocket);
        }

        private void PrintAddFakeSocketMsg(FakeSocket mSocket)
        {
#if DEBUG
            var mRemoteEndPoint = mSocket.RemoteEndPoint;
            if (mRemoteEndPoint != null)
            {
                NetLog.Log($"增加FakeSocket: {mRemoteEndPoint}, FakeSocket总数: {mAcceptSocketDic.Count}");
            }
            else
            {
                NetLog.Log($"增加FakeSocket, FakeSocket总数: {mAcceptSocketDic.Count}");
            }
#endif
        }

        private void PrintRemoveFakeSocketMsg(FakeSocket mSocket)
        {
#if DEBUG
            var mRemoteEndPoint = mSocket.RemoteEndPoint;
            if (mRemoteEndPoint != null)
            {
                NetLog.Log($"移除FakeSocket: {mRemoteEndPoint}, FakeSocket总数: {mAcceptSocketDic.Count}");
            }
            else
            {
                NetLog.Log($"移除FakeSocket, FakeSocket总数: {mAcceptSocketDic.Count}");
            }
#endif
        }
    }
}