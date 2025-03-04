﻿/************************************Copyright*****************************************
*        ProjectName:AKNet
*        Web:https://github.com/825126369/AKNet
*        Description:这是一个面向 .Net Standard 2.1 的游戏网络库
*        Author:阿珂
*        ModifyTime:2025/2/27 22:28:11
*        Copyright:MIT软件许可证
************************************Copyright*****************************************/
using System;
using System.Linq;

namespace AKNet.Common
{
    internal static class NetLogHelper
    {
        internal static void PrintByteArray(string tag, byte[] message)
        {
            string data = tag + ": " + string.Join(' ', message);
            NetLog.Log(data);
        }

        internal static void PrintByteArray(string tag, ReadOnlySpan<byte> message)
        {
            string data = tag + ": " + string.Join(' ', message.ToArray());
            NetLog.Log(data);
        }
    }
}
