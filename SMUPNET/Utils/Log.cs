using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;

namespace SMUPNET.Utils
{
    public class Log
    {
        private StreamWriter _fileStream = null;
        private object _lockObj = new object();

        public Log(bool toFile)
        {
            if (!toFile) {
                return;
            }

            try {
                var currDir = Directory.GetCurrentDirectory();
                var logsDir = Path.Combine(currDir, "Logs");
                var filePath = Path.Combine(logsDir, $"{DateTime.Now.ToString("yyyy-M-dd")}.txt");

                Directory.CreateDirectory(logsDir);

                _fileStream = File.AppendText(filePath);
                _fileStream.AutoFlush = true;
            }
            catch (Exception ex) { Exception(ex, false); }
        }

        public void Info(string msg, bool exitApp = false, params object[] args)
        {
            ProcessMsg(msg, ConsoleColor.Gray, exitApp, args: args);
        }

        public void Success(string msg, bool exitApp = false, params object[] args)
        {
            ProcessMsg(msg, ConsoleColor.Green, exitApp, args: args);
        }

        public void Warning(string msg, bool exitApp = false, params object[] args)
        {
            ProcessMsg(msg, ConsoleColor.Magenta, exitApp, args: args);
        }

        public void Error(string msg, bool exitApp = false, params object[] args)
        {
            ProcessMsg(msg, ConsoleColor.Red, exitApp, args: args);
        }

        public void Exception(Exception ex, bool exitApp)
        {
            ProcessMsg("Oops! A wild exception appeared!\n\nType: {0}\nMessage: {1}\n\nStacktrace:\n{2}",
                ConsoleColor.DarkRed,
                exitApp,
                args: new object[] {
                    ex.GetType().FullName,
                    ex.Message,
                    ex.StackTrace
                });
        }

        [Conditional("DEBUG")]
        public void Debug(string msg, bool exitApp = false, params object[] args)
        {
            ProcessMsg(msg, ConsoleColor.Cyan, exitApp, args: args);
        }

        private void ProcessMsg(string msg, ConsoleColor clr, bool exitApp, [CallerMemberName] string caller = "", params object[] args)
        {
            if (msg == null) {
                msg = string.Empty;
            }

            lock (_lockObj) {
                msg = $"[{DateTime.Now.ToLongTimeString()}]{caller.PadLeft(10, ' ')}| {string.Format(msg, args)}";

                Console.ForegroundColor = clr;
                Console.WriteLine(msg);
                Console.ResetColor();

                _fileStream?.WriteLine(msg);

                if (exitApp) {
                    Environment.Exit(1);
                }
            }
        }
    }
}
