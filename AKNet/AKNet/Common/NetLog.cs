﻿/************************************Copyright*****************************************
*        ProjectName:AKNet
*        Web:https://github.com/825126369/AKNet
*        Description:这是一个面向 .Net Standard 2.1 的游戏网络库
*        Author:阿珂
*        ModifyTime:2025/2/27 22:28:11
*        Copyright:MIT软件许可证
************************************Copyright*****************************************/
using System;
using System.Diagnostics;
using System.IO;

namespace AKNet.Common
{
    public static class NetLogMgr
    {
        public static void SetOrPrintLog(bool bPrintLog)
        {
            NetLog.bPrintLog = bPrintLog;
        }

        public static void AddLogFunc(Action<string> LogFunc, Action<string> LogErrorFunc, Action<string> LogWarningFunc)
        {
            NetLog.LogFunc += LogFunc;
            NetLog.LogErrorFunc += LogErrorFunc;
            NetLog.LogWarningFunc += LogWarningFunc;
        }

        public static void AddConsoleLog()
        {
#if !DEBUG
            Action<string> LogFunc = (string message)=>
            {
                Console.ForegroundColor = ConsoleColor.DarkGreen;
                Console.WriteLine(message);
            };

            Action<string> LogErrorFunc = (string message) =>
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(message);
            };

            Action<string> LogWarningFunc = (string message) =>
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(message);
            };

            AddLogFunc(LogFunc, LogErrorFunc, LogWarningFunc);
#endif
        }
    }

    internal static class NetLog
    {
        public static bool bPrintLog = true;
        public static event Action<string> LogFunc;
        public static event Action<string> LogWarningFunc;
        public static event Action<string> LogErrorFunc;
        private const string logFilePath = "aknet_Log.txt";

        static NetLog()
        {
            File.Delete(logFilePath);
            System.AppDomain.CurrentDomain.UnhandledException += _OnUncaughtExceptionHandler;
            LogErrorFunc += LogErrorToFile;

#if DEBUG
            try
            {
                // 在使用ProcessStartInfo 的重定向输出输入流 时，这里报错
                Console.Clear();
            }catch { }
#endif
        }

        public static void Init()
        {

        }

        public static void LogToFile(string filePath, string Message)
        {
            using (StreamWriter writer = new StreamWriter(filePath, true))
            {
                writer.WriteLine(Message);
            }
        }

        static void LogErrorToFile(string Message)
        {
            LogToFile(logFilePath, Message);
        }

        private static void _OnUncaughtExceptionHandler(object sender, System.UnhandledExceptionEventArgs args)
        {
            Exception exception = args.ExceptionObject as Exception;
            LogErrorToFile(GetMsgStr("_OnUncaughtExceptionHandler", exception.Message, exception.StackTrace));
        }

        private static string GetMsgStr(string logTag, object msgObj, string StackTraceObj)
        {
            string message = msgObj != null ? msgObj.ToString() : string.Empty;
            string StackTraceInfo = StackTraceObj != null ? "\n" + StackTraceObj : string.Empty;
            return $"{DateTime.Now.ToString()}  {logTag}: {message} {StackTraceInfo}";
        }

        private static string GetAssertMsg(object msgObj, string StackTraceInfo)
        {
            return GetMsgStr("Assert Error", msgObj, StackTraceInfo);
        }

        private static string GetExceptionMsg(object msgObj, string StackTraceInfo)
        {
            return GetMsgStr("Exception", msgObj, StackTraceInfo);
        }

        private static string GetErrorMsg(object msgObj, string StackTraceInfo)
        {
            return GetMsgStr("Error", msgObj, StackTraceInfo);
        }

        private static string GetLogMsg(object msgObj, string StackTraceInfo = null)
        {
            return GetMsgStr("Log", msgObj, StackTraceInfo);
        }

        private static string GetWarningMsg(object msgObj, string StackTraceInfo = null)
        {
            return GetMsgStr("Warning", msgObj, StackTraceInfo);
        }

        private static string GetStackTraceInfo()
        {
            StackTrace st = new StackTrace(true);
            return st.ToString();
        }

        internal static void Log(object message)
        {
            if (!bPrintLog) return;
#if DEBUG
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.WriteLine(GetLogMsg(message));
#endif
            if (LogFunc != null)
            {
                LogFunc(GetLogMsg(message));
            }
        }

        internal static void LogWarning(object message)
        {
            if (!bPrintLog) return;
#if DEBUG
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine(GetWarningMsg(message));
#endif
            if (LogWarningFunc != null)
            {
                LogWarningFunc(GetWarningMsg(message));
            }
        }

        internal static void LogException(Exception e)
        {
            if (!bPrintLog) return;
#if DEBUG
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(GetExceptionMsg(e.Message, e.StackTrace));
#endif
            if (LogErrorFunc != null)
            {
                LogErrorFunc(GetExceptionMsg(e.Message, e.StackTrace));
            }
        }

        internal static void LogError(object message)
        {
            if (!bPrintLog) return;
#if DEBUG
            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.WriteLine(GetErrorMsg(message, GetStackTraceInfo()));
#endif
            if (LogErrorFunc != null)
            {
                LogErrorFunc(GetErrorMsg(message, GetStackTraceInfo()));
            }
        }

        internal static void Assert(bool bTrue, object message = null)
        {
            if (!bPrintLog) return;
            if (!bTrue)
            {
#if DEBUG
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine(GetAssertMsg(message, GetStackTraceInfo()));
#endif
                if (LogErrorFunc != null)
                {
                    LogErrorFunc(GetAssertMsg(message, GetStackTraceInfo()));
                }
            }
        }
    }
}
