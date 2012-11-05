using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AwsJob
{
    public static class DataPath
    {
        private const string FILE_DIR = "\\Carleski.com\\AwsFileSync";
        private const string FILE_PATH = "\\VaultSync.xml";
        private static string fileDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + FILE_DIR;
        private static string file = fileDir + FILE_PATH;

        public static string getAppFile(string fileName)
        {
            string path = Path.GetFullPath(fileDir + fileName);
            string dir = Path.GetDirectoryName(path);

            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            return path;
        }

        public static string DataFile
        {
            get
            {
                if (!Directory.Exists(fileDir))
                {
                    Directory.CreateDirectory(fileDir);
                }

                return file;
            }
        }
    }
}
