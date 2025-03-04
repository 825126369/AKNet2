﻿/************************************Copyright*****************************************
*        ProjectName:AKNet
*        Web:https://github.com/825126369/AKNet
*        Description:这是一个面向 .Net Standard 2.1 的游戏网络库
*        Author:阿珂
*        ModifyTime:2025/2/27 22:28:11
*        Copyright:MIT软件许可证
************************************Copyright*****************************************/
using AKNet.Common;
using System;
using System.Collections.Generic;

namespace AKNet.Udp3Tcp.Common
{
    internal partial class ReSendPackageMgr : ReSendPackageMgrInterface
    {
        private UdpClientPeerCommonBase mClientPeer;
        private UdpCheckMgr mUdpCheckMgr;

        private readonly TcpSlidingWindow mTcpSlidingWindow = new TcpSlidingWindow();
        private readonly Queue<NetUdpSendFixedSizePackage> mWaitCheckSendQueue = new Queue<NetUdpSendFixedSizePackage>();
        public uint nCurrentWaitSendOrderId;

        private long nLastRequestOrderIdTime = 0;
        private uint nLastRequestOrderId = 0;
        private int nContinueSameRequestOrderIdCount = 0;
        private double nLastFrameTime = 0;
        private int nSearchCount = 0;

        private const int nMinSearchCount = 10;
        private int nMaxSearchCount = int.MaxValue;
        private int nRemainNeedSureCount = 0;

        public ReSendPackageMgr(UdpClientPeerCommonBase mClientPeer, UdpCheckMgr mUdpCheckMgr)
        {
            this.mClientPeer = mClientPeer;
            this.mUdpCheckMgr = mUdpCheckMgr;

            this.nSearchCount = nMinSearchCount;
            nMaxSearchCount = this.nSearchCount * 2;
            nCurrentWaitSendOrderId = Config.nUdpMinOrderId;

            InitRTO();
        }

        public void AddTcpStream(ReadOnlySpan<byte> buffer)
        {
            mTcpSlidingWindow.WriteFrom(buffer);
        }

        private void AddSendPackageOrderId(int nLength)
        {
            nCurrentWaitSendOrderId = OrderIdHelper.AddOrderId(nCurrentWaitSendOrderId, nLength);
        }

        void DoTcpSlidingWindowForward(uint nRequestOrderId)
        {
            mTcpSlidingWindow.DoWindowForward(nRequestOrderId);
            AddPackage();
        }

        private void AddPackage()
        {
            int nOffset = mTcpSlidingWindow.GetWindowOffset(nCurrentWaitSendOrderId);
            while (nOffset < mTcpSlidingWindow.Length)
            {
                NetLog.Assert(nOffset >= 0);

                var mPackage = mClientPeer.GetObjectPoolManager().UdpSendPackage_Pop();
                mPackage.mTcpSlidingWindow = this.mTcpSlidingWindow;
                mPackage.nOrderId = nCurrentWaitSendOrderId;

                int nRemainLength = mTcpSlidingWindow.Length - nOffset;
                NetLog.Assert(nRemainLength >= 0);

                if (Config.nUdpPackageFixedBodySize <= nRemainLength)
                {
                    mPackage.nBodyLength = Config.nUdpPackageFixedBodySize;
                }
                else
                {
                    mPackage.nBodyLength = (ushort)nRemainLength;
                }

                mWaitCheckSendQueue.Enqueue(mPackage);
                AddSendPackageOrderId(mPackage.nBodyLength);

                nOffset = mTcpSlidingWindow.GetWindowOffset(nCurrentWaitSendOrderId);
            }
        }

        public void Update(double elapsed)
        {
            UdpStatistical.AddSearchCount(this.nSearchCount);
            UdpStatistical.AddFrameCount();
            nLastFrameTime = elapsed;

            AddPackage();
            if (mWaitCheckSendQueue.Count == 0) return;

            bool bTimeOut = false;
            int nSearchCount = this.nSearchCount;
            foreach (var mPackage in mWaitCheckSendQueue)
            {
                if (mPackage.nSendCount > 0)
                {
                    if (mPackage.mTimeOutGenerator_ReSend.orTimeOut(elapsed))
                    {
                        UdpStatistical.AddReSendCheckPackageCount();
                        SendNetPackage(mPackage);
                        ArrangeReSendTimeOut(mPackage);
                        mPackage.nSendCount++;
                        bTimeOut = true;
                    }
                }
                else
                {
                    UdpStatistical.AddFirstSendCheckPackageCount();
                    SendNetPackage(mPackage);
                    ArrangeReSendTimeOut(mPackage);
                    mPackage.mTcpStanardRTOTimer.BeginRtt();
                    mPackage.nSendCount++;
                }

                if (--nSearchCount <= 0)
                {
                    break;
                }
            }

            if (bTimeOut)
            {
                this.nSearchCount = Math.Max(this.nSearchCount / 2 + 1, nMinSearchCount);
            }
        }

        public void Reset()
        {
            MainThreadCheck.Check();
            nCurrentWaitSendOrderId = Config.nUdpMinOrderId;
            mTcpSlidingWindow.WindowReset();

            foreach (var mRemovePackage in mWaitCheckSendQueue)
            {
                mClientPeer.GetObjectPoolManager().UdpSendPackage_Recycle(mRemovePackage);
            }
            mWaitCheckSendQueue.Clear();
        }

        private void ArrangeReSendTimeOut(NetUdpSendFixedSizePackage mPackage)
        {
            long nTimeOutTime = GetRTOTime();
            double fTimeOutTime = nTimeOutTime / 1000.0;

            UdpStatistical.AddRTO(nTimeOutTime);
            mPackage.mTimeOutGenerator_ReSend.SetInternalTime(fTimeOutTime);
        }

        //快速重传
        private void QuickReSend(uint nRequestOrderId)
        {
            if (nRequestOrderId != nLastRequestOrderId)
            {
                nContinueSameRequestOrderIdCount = 0;
                nLastRequestOrderId = nRequestOrderId;
            }

            nContinueSameRequestOrderIdCount++;
            if (nContinueSameRequestOrderIdCount >= 3)
            {
                nContinueSameRequestOrderIdCount = 0;
                // if (UdpStaticCommon.GetNowTime() - nLastRequestOrderIdTime > 5)
                {
                    nLastRequestOrderIdTime = UdpStaticCommon.GetNowTime();
                    foreach (var mPackage in mWaitCheckSendQueue)
                    {
                        if (mPackage.nOrderId == nRequestOrderId)
                        {
                            SendNetPackage(mPackage);
                            mPackage.nSendCount++;
                            UdpStatistical.AddQuickReSendCount();


                            //this.nMaxSearchCount = Math.Max(nMinSearchCount, this.nSearchCount / 2);
                            //this.nSearchCount = this.nMaxSearchCount + 3;
                            //this.nSearchCount = Math.Max(nMinSearchCount, this.nSearchCount / 2);
                            break;
                        }
                    }
                }
            }
        }

        public void ReceiveOrderIdRequestPackage(uint nRequestOrderId)
        {
            AddPackage();

            bool bHit = false;
            int nRemoveCount = 0;
            foreach (var mPackage in mWaitCheckSendQueue)
            {
                if (mPackage.nOrderId == nRequestOrderId)
                {
                    bHit = true;
                    break;
                }
                else
                {
                    nRemoveCount++;
                }
            }

            if (bHit)
            {
                bool bHaveRemove = nRemoveCount > 0;
                while (nRemoveCount-- > 0)
                {
                    var mPackage = mWaitCheckSendQueue.Dequeue();
                    if (mPackage.nSendCount == 1)
                    {
                        mPackage.mTcpStanardRTOTimer.FinishRtt(this);
                    }
                    mClientPeer.GetObjectPoolManager().UdpSendPackage_Recycle(mPackage);
                    Sure();
                }

                if (bHaveRemove)
                {
                    DoTcpSlidingWindowForward(nRequestOrderId);
                }
                else
                {
                    QuickReSend(nRequestOrderId);
                }

                //DoTcpSlidingWindowForward(nRequestOrderId);
                //QuickReSend(nRequestOrderId);
            }
        }

        private void Sure()
        {
            this.nRemainNeedSureCount--;
            if (this.nSearchCount < nMaxSearchCount)
            {
                this.nSearchCount = (this.nSearchCount + this.nMaxSearchCount) / 2 + 1;
                this.nSearchCount = Math.Max(nMinSearchCount, this.nSearchCount);
            }
            else if (this.nRemainNeedSureCount <= 0)
            {
                this.nSearchCount = this.nSearchCount + 1;
                this.nMaxSearchCount = Math.Max(this.nSearchCount / 2 + 1, this.nMaxSearchCount);

                this.nRemainNeedSureCount = this.nMaxSearchCount / 3 + 1;
            }
        }

        private void SendNetPackage(NetUdpSendFixedSizePackage mCheckPackage)
        {
            mClientPeer.SendNetPackage(mCheckPackage);
        }
    }

}