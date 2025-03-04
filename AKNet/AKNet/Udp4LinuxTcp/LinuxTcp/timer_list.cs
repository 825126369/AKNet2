﻿/************************************Copyright*****************************************
*        ProjectName:AKNet
*        Web:https://github.com/825126369/AKNet
*        Description:这是一个面向 .Net Standard 2.1 的游戏网络库
*        Author:阿珂
*        ModifyTime:2025/2/27 22:28:11
*        Copyright:MIT软件许可证
************************************Copyright*****************************************/
using System;

namespace AKNet.Udp4LinuxTcp.Common
{
    internal class TimerList
    {
        private readonly TimeOutGenerator _timer = new TimeOutGenerator();
        private tcp_sock tcp_sock_Instance;
        private Action<tcp_sock> _callback;
        private bool bRun = false;

        public TimerList(long period_ms, Action<tcp_sock> callback, tcp_sock tcp_sock_Instance)
        {
            this._timer.SetExpiresTime(period_ms);
            this.tcp_sock_Instance = tcp_sock_Instance;
            this._callback = callback;
            this.bRun = false;
        }

        private long MS_TO_MS(long period_ms)
        {
            return period_ms;
        }

        public void Update(double elapsed)
        {
            if (bRun && _timer.orTimeOut())
            {
                _callback(tcp_sock_Instance);
            }
        }

        private void Start()
        {
            bRun = true;
        }

        public void Stop()
        {
            bRun = false;
        }

        public void ModTimer(long period_ms)
        {
            if (period_ms > 0)
            {
                _timer.SetExpiresTime(period_ms);
                Start();
            }
            else
            {
                Stop();
            }
        }

        public void Reset()
        {
            Stop();
        }
    }
}
