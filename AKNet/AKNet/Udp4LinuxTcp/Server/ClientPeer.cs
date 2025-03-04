﻿/************************************Copyright*****************************************
*        ProjectName:AKNet
*        Web:https://github.com/825126369/AKNet
*        Description:这是一个面向 .Net Standard 2.1 的游戏网络库
*        Author:阿珂
*        ModifyTime:2025/2/27 22:28:11
*        Copyright:MIT软件许可证
************************************Copyright*****************************************/
using AKNet.Common;
using AKNet.Udp4LinuxTcp.Common;
using System;
using System.Net;

namespace AKNet.Udp4LinuxTcp.Server
{
    internal class ClientPeer : UdpClientPeerCommonBase, ClientPeerBase
	{
        internal MsgSendMgr mMsgSendMgr;
        internal MsgReceiveMgr mMsgReceiveMgr;
        internal ClientPeerSocketMgr mSocketMgr;

        private readonly ObjectPoolManager mObjectPoolManager;
        internal UdpCheckMgr mUdpCheckPool = null;
		internal UDPLikeTCPMgr mUDPLikeTCPMgr = null;
        private SOCKET_PEER_STATE mSocketPeerState = SOCKET_PEER_STATE.NONE;
        private UdpServer mNetServer;
        private bool b_SOCKET_PEER_STATE_Changed = false;

        public ClientPeer(UdpServer mNetServer)
        {
            this.mNetServer = mNetServer;
            mSocketMgr = new ClientPeerSocketMgr(mNetServer, this);
            mMsgReceiveMgr = new MsgReceiveMgr(mNetServer, this);
            mMsgSendMgr = new MsgSendMgr(mNetServer, this);
            mUdpCheckPool = new UdpCheckMgr(this);
            mUDPLikeTCPMgr = new UDPLikeTCPMgr(mNetServer, this);

            mObjectPoolManager = new ObjectPoolManager();
            SetSocketState(SOCKET_PEER_STATE.NONE);
        }

        public void Update(double elapsed)
        {
            if (b_SOCKET_PEER_STATE_Changed)
            {
                b_SOCKET_PEER_STATE_Changed = false;
                this.mNetServer.OnSocketStateChanged(this);
            }

            mMsgReceiveMgr.Update(elapsed);
            mUDPLikeTCPMgr.Update(elapsed);
            mUdpCheckPool.Update(elapsed);
        }

        public void SetSocketState(SOCKET_PEER_STATE mState)
        {
            if (this.mSocketPeerState != mState)
            {
                this.mSocketPeerState = mState;
                if (MainThreadCheck.orInMainThread())
                {
                    this.mNetServer.OnSocketStateChanged(this);
                }
                else
                {
                    b_SOCKET_PEER_STATE_Changed = true;
                }
            }
        }

        public SOCKET_PEER_STATE GetSocketState()
		{
			return mSocketPeerState;
		}

        public void Reset()
        {
            mUDPLikeTCPMgr.Reset();
            mMsgReceiveMgr.Reset();
            mUdpCheckPool.Reset();
            mSocketMgr.Reset();
        }

        public void CloseSocket()
        {
            mSocketMgr.CloseSocket();
        }

        public void HandleConnectedSocket(FakeSocket mSocket)
        {
            SetSocketState(SOCKET_PEER_STATE.CONNECTED);
            mSocketMgr.HandleConnectedSocket(mSocket);
            mSocket.SetClientPeer(this);
        }

        public IPEndPoint GetIPEndPoint()
        {
            return mSocketMgr.GetIPEndPoint();
        }

        public string GetIPAddress()
        {
            return GetIPEndPoint().Address.ToString();
        }

        public void SendNetPackage(sk_buff skb)
        {
            mUDPLikeTCPMgr.ResetSendHeartBeatCdTime();
            this.mSocketMgr.SendNetPackage(skb.GetSendBuffer());
        }

        public void SendInnerNetData(byte id)
        {
            mMsgSendMgr.SendInnerNetData(id);
        }

        public void SendNetData(ushort nPackageId)
        {
            mMsgSendMgr.SendNetData(nPackageId);
        }

        public void SendNetData(ushort nPackageId, byte[] data)
        {
            mMsgSendMgr.SendNetData(nPackageId, data);
        }

        public void SendNetData(NetPackage mNetPackage)
        {
            mMsgSendMgr.SendNetData(mNetPackage);
        }

        public void SendNetData(ushort nPackageId, ReadOnlySpan<byte> buffer)
        {
            mMsgSendMgr.SendNetData(nPackageId, buffer);
        }

        public void ResetSendHeartBeatCdTime()
        {
            this.mUDPLikeTCPMgr.ResetSendHeartBeatCdTime();
        }

        public void ReceiveHeartBeat()
        {
            this.mUDPLikeTCPMgr.ReceiveHeartBeat();
        }

        public void ReceiveConnect(sk_buff skb)
        {
            this.mUDPLikeTCPMgr.ReceiveConnect(skb);
        }

        public void ReceiveDisConnect()
        {
            this.mUDPLikeTCPMgr.ReceiveDisConnect();
        }

        public ObjectPoolManager GetObjectPoolManager()
        {
            return mObjectPoolManager;
        }

        public Config GetConfig()
        {
            return mNetServer.GetConfig();
        }

        public void NetPackageExecute(NetPackage mPackage)
        {
            mNetServer.GetPackageManager().NetPackageExecute(this, mPackage);
        }
    }
}
