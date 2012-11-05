using AwsJob;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AwsFileSync
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            using (StreamWriter log = new StreamWriter(DataPath.getAppFile("VaultSync.log")))
            {
                Console.SetOut(log);
                Console.SetError(log);

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                CustomAppContext context = getCustomContext(args);
                Application.Run(context);
            }
        }

        private static CustomAppContext getCustomContext(string[] args)
        {
            string command = args != null && args.Length > 0 ? args[0] : null;
            string[] newArgs = new string[args != null && args.Length > 0 ? args.Length - 1 : 0];

            for (int i = 0; i < newArgs.Length; i++)
            {
                newArgs[i] = args[i - 1];
            }

            if (command == "SyncVaults")
            {
                return getRunContext(true, false, newArgs);
            }
            else if (command == "Poll")
            {
                return getRunContext(false, false, newArgs);
            }
            else if (command == "InactivePoll")
            {
                return getRunContext(false, true, newArgs);
            }
            else
            {
                return new CustomAppContext();
            }
        }

        private static CustomAppContext getRunContext(bool synchronizeWithServer, bool startPollersInactive, string[] args)
        {
            Console.WriteLine("Starting run at " + DateTime.Now);

            TimeSpan max = args.Length > 0 ? TimeSpan.Parse(args[0]) : TimeSpan.MaxValue;
            return new CustomAppContext(max, synchronizeWithServer, startPollersInactive);
        }
    }
}
