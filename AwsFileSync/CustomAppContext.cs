using AwsFileSync.Properties;
using AwsJob;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AwsFileSync
{
    class CustomAppContext : ApplicationContext
    {
        private Timer timer;
        private VaultSync sync;
        private bool listening = true;
        private Container components;
        private StatusForm form;
        private NotifyIcon syncIcon;
        private ContextMenu menu;
        private MenuItem startSync;
        private MenuItem status;
        private MenuItem listJobs;
        private MenuItem pauseResume;
        private MenuItem stopStartListening;
        private MenuItem exit;

        public CustomAppContext()
        {
            InitializeComponents(true);

            sync = new VaultSync(TimeSpan.MaxValue);
            sync.SynchronizingWithServer = false;
            sync.start();
        }

        public CustomAppContext(TimeSpan duration, bool synchronizingWithServer, bool startPollerInactive)
        {
            InitializeComponents(false);

            sync = new VaultSync(duration);
            sync.RunCompleted += sync_RunCompleted;
            sync.SynchronizingWithServer = synchronizingWithServer;
            sync.StartPollerInactive = startPollerInactive;
            sync.start();
        }

        void sync_RunCompleted(object arg1, EventArgs arg2)
        {
            ExitThread();
        }

        private void InitializeComponents(bool useNotifyIcon)
        {
            components = new Container();

            timer = new Timer(components)
            {
                Interval = 20000,
                Enabled = true
            };
            timer.Tick += timer_Tick;

            if (useNotifyIcon)
            {
                startSync = new MenuItem("&Start");
                startSync.Click += startSync_Click;

                status = new MenuItem("&Status");
                status.Click += status_Click;
                status.DefaultItem = true;

                listJobs = new MenuItem("List &Jobs");
                listJobs.Click += listJobs_Click;
                listJobs.Enabled = false;

                pauseResume = new MenuItem("&Pause");
                pauseResume.Click += pauseResume_Click;
                pauseResume.Enabled = false;

                stopStartListening = new MenuItem("Stop &Listening");
                stopStartListening.Click += stopStartListening_Click;

                exit = new MenuItem("E&xit");
                exit.Click += exit_Click;

                menu = new ContextMenu(new MenuItem[] { startSync, status, listJobs, pauseResume, stopStartListening, exit });
                syncIcon = new NotifyIcon(components)
                {
                    ContextMenu = menu,
                    Text = "AWS File Sync",
                    Icon = Resources.FileSync,
                    Visible = true
                };
                syncIcon.DoubleClick += syncIcon_DoubleClick;
            }
        }

        void timer_Tick(object sender, EventArgs e)
        {
            sync.requestSave();
        }

        void syncIcon_DoubleClick(object sender, EventArgs e)
        {
            if (startSync.Enabled)
            {
                startSync_Click(sender, e);
            }
            else
            {
                status_Click(sender, e);
            }
        }

        void startSync_Click(object sender, EventArgs e)
        {
            bool starting = startSync.Text.EndsWith("Start");

            sync.stop();
            sync = new VaultSync(TimeSpan.MaxValue);

            sync.SynchronizingWithServer = starting;
            sync.StartPollerInactive = !listening;
            sync.start();

            listJobs.Enabled = starting;
            pauseResume.Enabled = starting;
        }

        void listJobs_Click(object sender, EventArgs e)
        {
            Dictionary<string, Dictionary<string, string>[]> details = new Dictionary<string, Dictionary<string, string>[]>();
            FolderVaultMapping m;

            foreach (JobPoller poller in JobPoller.ActivePollers)
            {
                m = poller.Context.Mapping;

                details[m.LocalFolder + " - " + m.VaultName] = poller.listJobs();
            }

            showDetails(details);
        }

        void exit_Click(object sender, EventArgs e)
        {
            ExitThread();
        }

        void stopStartListening_Click(object sender, EventArgs e)
        {
            if (listening)
            {
                stopStartListening.Text = "Start &Listening";
                listening = false;
            }
            else
            {
                stopStartListening.Text = "Stop &Listening";
                listening = true;
            }

            foreach (JobPoller poller in JobPoller.ActivePollers)
            {
                poller.IsListening = listening;
            }
        }

        void status_Click(object sender, EventArgs e)
        {
            Dictionary<string, Dictionary<string, string>[]> details = new Dictionary<string, Dictionary<string, string>[]>();
            FolderVaultMapping m;

            foreach (JobPoller poller in JobPoller.ActivePollers)
            {
                m = poller.Context.Mapping;

                details[m.LocalFolder + " - " + m.VaultName] = poller.showStatus();
            }

            showDetails(details);
        }

        private void showDetails(Dictionary<string, Dictionary<string, string>[]> details)
        {
            StringBuilder sb = new StringBuilder();

            foreach (KeyValuePair<string, Dictionary<string, string>[]> detail in details)
            {
                sb.AppendLine(detail.Key);
                foreach (Dictionary<string, string> map in detail.Value)
                {
                    sb.AppendLine("    " + map["ConsoleTitle"]);

                    foreach (KeyValuePair<string, string> pair in map)
                    {
                        if (pair.Key != "ConsoleTitle")
                        {
                            sb.AppendLine("        " + pair.Key + ": " + pair.Value);
                        }
                    }
                }
            }

            showForm(sb.ToString());
        }

        void pauseResume_Click(object sender, EventArgs e)
        {
            if (pauseResume.Text.EndsWith("Pause"))
            {
                pauseResume.Text = "&Resume";
                sync.pause();
            }
            else
            {
                pauseResume.Text = "&Pause";
                sync.resume();
            }
        }

        private void showForm(string status)
        {
            if (form == null)
            {
                form = new StatusForm();
                form.FormClosed += form_FormClosed;
                form.SizeChanged += form_SizeChanged;
                form.Status = status;
                form.Show();
            }
            else
            {
                form.Status = status;
                form.Activate();
            }
        }

        void form_SizeChanged(object sender, EventArgs e)
        {
            if (form.WindowState == FormWindowState.Minimized)
            {
                form.Close();
            }
        }

        void form_FormClosed(object sender, FormClosedEventArgs e)
        {
            form = null;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null)
            {
                components.Dispose();
                components = null;
            }
            base.Dispose(disposing);
        }
        protected override void ExitThreadCore()
        {
            if (timer != null)
            {
                timer.Stop();
            }
            if (form != null)
            {
                form.Close();
            }
            if (syncIcon != null)
            {
                syncIcon.Visible = false;
                Application.DoEvents();
            }
            sync.stop();

            base.ExitThreadCore();
        }
    }
}
