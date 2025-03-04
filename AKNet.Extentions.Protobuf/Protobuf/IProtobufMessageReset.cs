﻿/************************************Copyright*****************************************
*        ProjectName:AKNet
*        Web:https://github.com/825126369/AKNet
*        Description:这是一个面向 .Net Standard 2.1 的游戏网络库
*        Author:阿珂
*        CreateTime:2024/11/28 7:14:05
*        Copyright:MIT软件许可证
************************************Copyright*****************************************/
using AKNet.Extentions.Protobuf;

namespace TestProtocol
{
    public sealed partial class TESTChatMessage : IProtobufResetInterface
    {
        public void Reset()
        {
            NClientId = 0;
            NSortId = 0;
            TalkMsg = string.Empty;
        }
    }
}

