﻿/************************************Copyright*****************************************
*        ProjectName:AKNet
*        Web:https://github.com/825126369/AKNet
*        Description:这是一个面向 .Net Standard 2.1 的游戏网络库
*        Author:阿珂
*        ModifyTime:2025/2/27 22:28:11
*        Copyright:MIT软件许可证
************************************Copyright*****************************************/
namespace AKNet.Udp4LinuxTcp.Common
{
    //这个超时器，是以毫秒为单位的
    internal class TimeOutGenerator
    {
        long fTimeOutTime = 0;
        public void SetExpiresTime(long fTimeOutTime)
        {
            this.fTimeOutTime = fTimeOutTime;
        }

        private void Stop()
        {
            this.fTimeOutTime = 0;
        }

        public bool orTimeOut()
        {
            if (this.fTimeOutTime <= 0L) { return false; }

            if (LinuxTcpFunc.tcp_jiffies32 >= fTimeOutTime)
            {
                this.Stop();
                return true;
            }
            return false;
        }
    }
}