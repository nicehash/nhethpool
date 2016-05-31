using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace nhethpool
{
    class Logging
    {
        private static FileStream file;

        public static void StartFileLogging()
        {
            if (file != null)
                file.Close();
            string path = Config.ConfigData.LogFileFolder + "\\log_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".txt";
            try
            {
                Directory.CreateDirectory(Config.ConfigData.LogFileFolder);
                file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to open logging file; " + ex.Message);
                file = null;
            }
        }

        public static void EndFileLogging()
        {
            if (file == null) return;
            file.Close();
            file = null;
        }

        public static void Log(int level, string text)
        {
            text = "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss:fff") + "] " + text;

            if (level <= Config.ConfigData.LogConsoleLevel)
                Console.WriteLine(text);

            if (level <= Config.ConfigData.LogFileLevel && file != null)
            {
                if (file.Position >= (2048 * 1024)) StartFileLogging();
                if (file == null) return;
                byte[] buffer = ASCIIEncoding.ASCII.GetBytes(text + "\r\n");
                file.Write(buffer, 0, buffer.Length);
                file.FlushAsync();
            }
        }
    }
}
