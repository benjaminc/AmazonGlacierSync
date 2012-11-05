using AwsJob;
using AwsJob.Properties;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace AwsFileSync
{
    class VaultSync
    {
        private static object fileLock = new object();

        private Thread runner;
        private List<JobPoller> pollers = new List<JobPoller>();
        private Dictionary<string, Vault> vaults = new Dictionary<string, Vault>();
        private TimeSpan maxRunTime;
        private volatile bool running;
        private volatile bool stopCalled;
        private volatile bool paused;
        private volatile bool saveRequested;
        private volatile bool isDirty;

        public DateTime? StartTime { get; private set; }
        public DateTime? ScheduledStopTime { get; private set; }
        public DateTime? StopTime { get; private set; }
        public int ProcessId { get; private set; }
        public bool SynchronizingWithServer { get; set; }
        public bool StartPollerInactive { get; set; }
        public event Action<object, EventArgs> RunCompleted;
        public bool IsDirty
        {
            get
            {
                if (!isDirty)
                {
                    foreach (Vault v in vaults.Values)
                    {
                        if (v.IsDirty)
                        {
                            return true;
                        }
                    }
                }

                return isDirty;
            }
        }

        public RunStatus RunStatus
        {
            get
            {
                if (runner == null || !runner.IsAlive)
                {
                    return running ? RunStatus.Starting : RunStatus.Stopped;
                }
                else if (!running)
                {
                    return RunStatus.Stopping;
                }
                else
                {
                    return paused ? RunStatus.Paused : RunStatus.Running;
                }
            }
        }
        public IList<Vault> Vaults
        {
            get
            {
                return new List<Vault>(vaults.Values).AsReadOnly();
            }
        }

        public VaultSync(TimeSpan maxRunTime)
        {
            this.maxRunTime = maxRunTime;
            ProcessId = Process.GetCurrentProcess().Id;
        }

        public void start()
        {
            if (runner == null)
            {
                running = true;
                runner = new Thread(run);
                runner.Start();
            }
        }

        public void pause()
        {
            paused = true;
        }

        public void resume()
        {
            paused = false;
        }

        public void stop()
        {
            if (!stopCalled)
            {
                stopCalled = true;
                running = false;

                if (runner != null && runner.IsAlive)
                {
                    runner.Join();
                }

                foreach (JobPoller poller in pollers)
                {
                    poller.sendStatusEmail();
                }
                pollers.Clear();

                StopTime = DateTime.Now;
                isDirty = true;
                save();

                if (RunCompleted != null)
                {
                    RunCompleted(this, new EventArgs());
                }
            }
        }

        private void run()
        {
            StartTime = DateTime.Now;
            ScheduledStopTime = maxRunTime == TimeSpan.MaxValue ? DateTime.MaxValue : DateTime.Now.Add(maxRunTime);
            isDirty = true;

            load();

            List<VaultContext> contexts = getActiveContexts();
            contexts.ForEach((vc) => { new JobPoller(this, vc, keepRunning, !StartPollerInactive).start(); });

            do
            {
                if (SynchronizingWithServer)
                {
                    contexts.ForEach((vc) => { vc.Vault.beginLoad(vc, keepRunning); });
                    new FileSync().syncFiles(contexts, keepRunning);
                }
                else
                {
                    Thread.Sleep(1000);
                }
            }
            while (keepRunning() && !SynchronizingWithServer);

            if (!stopCalled)
            {
                runner = null;
                stop();
            }
        }

        public bool keepRunning()
        {
            while (running && paused)
            {
                try
                {
                    Thread.Sleep(1000);
                }
                catch (ThreadInterruptedException)
                {
                }
            }

            if (saveRequested)
            {
                save();
                saveRequested = false;
            }

            bool pastDue = ScheduledStopTime != null && ScheduledStopTime.HasValue && DateTime.Now >= ScheduledStopTime.Value;

            return running && !pastDue;
        }

        public void requestSave()
        {
            saveRequested = true;
        }

        private void load()
        {
            string path = DataPath.DataFile;

            if (!File.Exists(path))
            {
                return;
            }

            Vault vault;

            using (FileStream file = File.OpenRead(path))
            {
                using (XmlReader xml = XmlReader.Create(file))
                {
                    while (xml.Read() && !xml.EOF)
                    {
                        if (xml.IsStartElement())
                        {
                            if (xml.LocalName == "Vaults" && !xml.IsEmptyElement)
                            {
                                while (xml.Read() && !xml.EOF)
                                {
                                    if (xml.IsStartElement() && xml.LocalName == "Vault" && !xml.IsEmptyElement)
                                    {
                                        vault = Vault.parse(xml);
                                        if (vault != null)
                                        {
                                            vaults[vault.VaultName] = vault;
                                        }
                                    }
                                    else if (xml.NodeType == XmlNodeType.EndElement && xml.LocalName == "Vaults")
                                    {
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private void save()
        {
            string path = DataPath.DataFile;
            List<VaultContext> active = getActiveContexts();
            FolderVaultMapping mapping;

            try
            {
                if (!IsDirty)
                {
                    return;
                }

                lock (fileLock)
                {
                    using (FileStream file = File.OpenWrite(path + ".tmp"))
                    {
                        XmlWriterSettings settings = new XmlWriterSettings();
                        settings.Indent = true;
                        using (XmlWriter writer = XmlWriter.Create(file, settings))
                        {
                            writer.WriteStartDocument();
                            writer.WriteStartElement("VaultSync");
                            writer.WriteElementString("StartTime", DateFormat.formatDateTime(StartTime, true));
                            writer.WriteElementString("ScheduledStopTime", DateFormat.formatDateTime(ScheduledStopTime, true));
                            writer.WriteElementString("StopTime", DateFormat.formatDateTime(StopTime, true));
                            writer.WriteElementString("ProcessId", ProcessId.ToString());
                            writer.WriteElementString("RunStatus", RunStatus.ToString());
                            writer.WriteStartElement("Vaults");
                            foreach (Vault vault in vaults.Values)
                            {
                                mapping = null;
                                foreach(VaultContext vc in active)
                                {
                                    if(vc.Vault == vault)
                                    {
                                        mapping = vc.Mapping;
                                    }
                                }

                                vault.save(writer, mapping);
                            }
                            writer.WriteEndDocument();
                        }
                    }

                    File.Replace(path + ".tmp", path, path + ".bak", true);
                    isDirty = false;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
            }
        }

        public List<VaultContext> getActiveContexts()
        {
            Settings props = Settings.Default;

            return props.Vaults.ConvertAll((fv) =>
            {
                return new VaultContext(fv, vaults.ContainsKey(fv.VaultName) ? vaults[fv.VaultName] : null);
            });
        }
    }

    public enum RunStatus
    {
        Starting,
        Running,
        Paused,
        Stopping,
        Stopped
    }
}
