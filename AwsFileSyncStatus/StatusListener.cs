using AwsFileSyncStatus.Properties;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AwsFileSyncStatus
{
    class StatusListener
    {
        private HttpListener listener;

        public StatusListener()
        {
        }

        public void start()
        {
            if (listener == null)
            {
                listener = new HttpListener();
                listener.Prefixes.Add(Settings.Default.ListenUrl);
            }

            if (!listener.IsListening)
            {
                listener.Start();
                listener.BeginGetContext(handleContext, null);
            }
        }

        public void stop()
        {
            if (listener != null && listener.IsListening)
            {
                listener.Abort();
            }

            listener = null;
        }

        private void handleContext(IAsyncResult result)
        {
            HttpListener list = listener;
            if (list != null)
            {
                HttpListenerContext context = list.EndGetContext(result);

                new Thread(() =>
                {
                    StatusHandler handler = new StatusHandler(context);
                    handler.run();
                }).Start();

                list.BeginGetContext(handleContext, null);
            }
        }
    }
}
