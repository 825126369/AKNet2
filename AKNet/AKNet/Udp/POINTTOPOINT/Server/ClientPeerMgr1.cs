﻿/************************************Copyright*****************************************
*        ProjectName:AKNet
*        Web:https://github.com/825126369/AKNet
*        Description:这是一个面向 .Net Standard 2.1 的游戏网络库
*        Author:阿珂
*        ModifyTime:2025/2/27 22:28:11
*        Copyright:MIT软件许可证
************************************Copyright*****************************************/
using AKNet.Common;
using AKNet.Udp.POINTTOPOINT.Common;
using System.Collections.Generic;
using System.Net;

namespace AKNet.Udp.POINTTOPOINT.Server
{
    internal class ClientPeerMgr1
    {
        private readonly Dictionary<string, ClientPeer> mClientDic = new Dictionary<string, ClientPeer>();
        private readonly List<string> mRemovePeerList = new List<string>();
        private readonly Queue<NetUdpFixedSizePackage> mPackageQueue = new Queue<NetUdpFixedSizePackage>();
        private UdpServer mNetServer = null;

        public ClientPeerMgr1(UdpServer mNetServer)
        {
            this.mNetServer = mNetServer;
        }

        public void MultiThreading_AddPackage(NetUdpFixedSizePackage mPackage)
        {
            lock (mPackageQueue)
            {
                mPackageQueue.Enqueue(mPackage);
            }
        }

        public void Update(double elapsed)
        {
            //网络流量大的时候，会卡在这，一直while循环
            while (NetPackageExecute())
            {

            }

            foreach (var v in mClientDic)
            {
                ClientPeer clientPeer = v.Value;
                clientPeer.Update(elapsed);
                if (clientPeer.GetSocketState() == SOCKET_PEER_STATE.DISCONNECTED)
                {
                    mRemovePeerList.Add(v.Key);
                }
            }

            foreach (var v in mRemovePeerList)
            {
                ClientPeer mClientPeer = mClientDic[v];
                mClientDic.Remove(v);
                PrintRemoveClientMsg(mClientPeer);
                mClientPeer.CloseSocket();
                mNetServer.GetClientPeerPool().recycle(mClientPeer);
            }
            mRemovePeerList.Clear();
        }

        private bool NetPackageExecute()
        {
            NetUdpFixedSizePackage mPackage = null;
            lock (mPackageQueue)
            {
                mPackageQueue.TryDequeue(out mPackage);
            }

            if (mPackage != null)
            {
                UdpStatistical.AddReceivePackageCount();
                AddClient_And_ReceiveNetPackage(mPackage);
                return true;
            }

            return false;
        }

        private void AddClient_And_ReceiveNetPackage(NetUdpFixedSizePackage mPackage)
        {
            MainThreadCheck.Check();
            IPEndPoint endPoint = (IPEndPoint)mPackage.remoteEndPoint;

            ClientPeer mClientPeer = null;
            string nPeerId = endPoint.ToString();
            if (!mClientDic.TryGetValue(nPeerId, out mClientPeer))
            {
                if (mPackage.nPackageId == UdpNetCommand.COMMAND_DISCONNECT)
                {
                    mNetServer.GetInnerCommandSendMgr().SendInnerNetData(UdpNetCommand.COMMAND_DISCONNECT, endPoint);
                }
                else if (mPackage.nPackageId == UdpNetCommand.COMMAND_CONNECT)
                {
                    if (mClientDic.Count >= mNetServer.GetConfig().MaxPlayerCount)
                    {
#if DEBUG
                        NetLog.Log($"服务器爆满, 客户端总数: {mClientDic.Count}");
#endif
                    }
                    else
                    {
                        mClientPeer = mNetServer.GetClientPeerPool().Pop();
                        mClientDic.Add(nPeerId, mClientPeer);
                        FakeSocket mSocket = new FakeSocket(mNetServer);
                        mSocket.RemoteEndPoint = endPoint;
                        mClientPeer.HandleConnectedSocket(mSocket);
                        PrintAddClientMsg(mClientPeer);
                    }
                }
            }

            if (mClientPeer != null)
            {
                mClientPeer.mUdpCheckPool.ReceiveNetPackage(mPackage);
            }
            else
            {
                mNetServer.GetObjectPoolManager().NetUdpFixedSizePackage_Recycle(mPackage);
            }
        }

        private void PrintAddClientMsg(ClientPeer clientPeer)
        {
#if DEBUG
            var mRemoteEndPoint = clientPeer.GetIPEndPoint();
            if (mRemoteEndPoint != null)
            {
                NetLog.Log($"增加客户端: {mRemoteEndPoint}, 客户端总数: {mClientDic.Count}");
            }
            else
            {
                NetLog.Log($"增加客户端, 客户端总数: {mClientDic.Count}");
            }
#endif
        }

        private void PrintRemoveClientMsg(ClientPeer clientPeer)
        {
#if DEBUG
            var mRemoteEndPoint = clientPeer.GetIPEndPoint();
            if (mRemoteEndPoint != null)
            {
                NetLog.Log($"移除客户端: {mRemoteEndPoint}, 客户端总数: {mClientDic.Count}");
            }
            else
            {
                NetLog.Log($"移除客户端, 客户端总数: {mClientDic.Count}");
            }
#endif
        }

    }
}