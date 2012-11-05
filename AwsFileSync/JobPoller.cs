using Amazon.Glacier;
using Amazon.Glacier.Model;
using Amazon.Glacier.Transfer;
using Amazon.SQS;
using Amazon.SQS.Model;
using AwsFileSync.Properties;
using AwsJob;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ThirdParty.Json.LitJson;

namespace AwsFileSync
{
    class JobPoller
    {
        private static List<JobPoller> activePollers = new List<JobPoller>();
        public static IList<JobPoller> ActivePollers { get { return activePollers.AsReadOnly(); } }

        private Thread runner;
        private volatile bool active = true;

        public VaultSync Sync { get; private set; }
        public VaultContext Context { get; private set; }
        public Func<bool> KeepRunning { get; private set; }
        public bool IsListening
        {
            get { return active; }
            set { active = value; }
        }

        public JobPoller(VaultSync sync, VaultContext context, Func<bool> keepRunning, bool startActive)
        {
            Sync = sync;
            Context = context;
            KeepRunning = keepRunning;
            active = startActive;
        }

        public void start()
        {
            if (!KeepRunning())
            {
                return;
            }
            if (runner == null || runner.ThreadState == ThreadState.Aborted ||
                runner.ThreadState == ThreadState.AbortRequested ||
                runner.ThreadState == ThreadState.Stopped ||
                runner.ThreadState == ThreadState.StopRequested)
            {
                runner = new Thread(execute);
                runner.Start();

                activePollers.Add(this);
            }
        }

        private void execute()
        {
            FolderVaultMapping mapping = Context.Mapping;
            ReceiveMessageRequest req = new ReceiveMessageRequest() { QueueUrl = mapping.NotificationQueueURL, MaxNumberOfMessages = 10 };
            List<QueueMessage> messages = new List<QueueMessage>();
            DeleteMessageBatchRequest deleter;
            ReceiveMessageResult result;
            List<Message> resultMessages;
            HashSet<string> exceptions = new HashSet<string>();
            long id = 0;

            using (AmazonSQSClient client = new AmazonSQSClient(mapping.AccessKey, mapping.SecretKey, mapping.Endpoint))
            {
                while (KeepRunning())
                {
                    try
                    {
                        messages.Clear();
                        result = client.ReceiveMessage(req).ReceiveMessageResult;
                        resultMessages = new List<Message>(result.Message);

                        for(int i = 0; i < resultMessages.Count; i++)
                        {
                            if (exceptions.Contains(resultMessages[i].MessageId))
                            {
                                resultMessages.RemoveAt(i);
                                i--;
                            }
                        }

                        if (resultMessages.Count > 0)
                        {
                            foreach (Message m in resultMessages)
                            {
                                try
                                {
                                    messages.Add(new QueueMessage(m.ReceiptHandle, m.MessageId, m.Body));
                                }
                                catch (InvalidDataException ex)
                                {
                                    Console.Error.WriteLine("Received an error parsing " + m.MessageId);
                                    Console.Error.WriteLine(ex);
                                    exceptions.Add(m.MessageId);
                                }
                            }

                            try
                            {
                                deleter = new DeleteMessageBatchRequest() { QueueUrl = mapping.NotificationQueueURL };

                                foreach (QueueMessage message in messages)
                                {
                                    try
                                    {
                                        Console.WriteLine("Handling a " + message.Action + " message with ID " + message.MessageId);
                                        if (message.Action == "ResultNotice" || handleMessage(message))
                                        {
                                            deleter.Entries.Add(new DeleteMessageBatchRequestEntry() { Id = "Delete" + (id++), ReceiptHandle = message.ReceiptHandle });
                                        }
                                        else
                                        {
                                            Console.WriteLine("    Unknown action");
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.Error.WriteLine(ex);
                                        exceptions.Add(message.MessageId);
                                    }
                                }

                                if (deleter.Entries.Count > 0)
                                {
                                    client.DeleteMessageBatch(deleter);
                                }
                            }
                            catch(Exception ex)
                            {
                                Console.Error.WriteLine(ex);
                            }
                        }
                        else
                        {
                            for (int i = 0; i < Settings.Default.PollInterval.TotalSeconds && KeepRunning(); i++)
                            {
                                Thread.Sleep(1000);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine(ex);
                    }
                }
            }

            activePollers.Remove(this);
        }

        private bool handleMessage(QueueMessage message)
        {
            if(!KeepRunning())
            {
                return false;
            }

            if (active)
            {
                if ((message.Action == "InventoryRetrieval" || message.Action == "ArchiveRetrieval") && message.JobId != null)
                {
                    completeJob(message.Properties, message.Action);
                    return true;
                }
                else if (message.Action == "ReuploadArchive" && message.ArchiveId != null)
                {
                    using (ArchiveTransferManager manager = new ArchiveTransferManager(Context.Mapping.AccessKey, Context.Mapping.SecretKey, Context.Mapping.Endpoint))
                    {
                        Context.Vault.reuploadArchive(manager, message.ArchiveId, Context.Mapping.LocalFolder);
                    }
                    return true;
                }
                else if (message.Action == "DeleteArchive" && message.ArchiveId != null)
                {
                    using (ArchiveTransferManager manager = new ArchiveTransferManager(Context.Mapping.AccessKey, Context.Mapping.SecretKey, Context.Mapping.Endpoint))
                    {
                        Context.Vault.deleteArchive(manager, message.ArchiveId);
                    }
                    return true;
                }
                else if (message.Action == "ArchiveRetrievalStarted" && message.ArchiveId != null)
                {
                    DateTime scheduledAt = message.ScheduledAt != null && message.ScheduledAt.HasValue ? message.ScheduledAt.Value : DateTime.Now;
                    Context.Vault.recordDownloadScheduled(message.ArchiveId, message.JobId, scheduledAt);
                    return true;
                }
                else if (message.Action == "StartSynchronizing")
                {
                    Sync.SynchronizingWithServer = true;
                    return true;
                }
                else if (message.Action == "Stop")
                {
                    Sync.stop();
                    return true;
                }
                else if (message.Action == "ListJobs")
                {
                    showResults(message, listJobs());
                    return true;
                }
                else if (message.Action == "DescribeJob" && message.JobId != null)
                {
                    showResults(message, describeJob(message.JobId));
                    return true;
                }
            }

            if (message.Action == "StatusCheck")
            {
                showResults(message, showStatus());
                return true;
            }
            else if (message.Action == "Inactivate")
            {
                active = false;
                return true;
            }
            else if (message.Action == "Activate")
            {
                active = true;
                return true;
            }

            return false;
        }

        public Dictionary<string, string>[] showStatus()
        {
            Dictionary<string, string>[] results = new Dictionary<string,string>[1];

            results[0] = new Dictionary<string, string>();
            results[0]["ConsoleTitle"] = "Job Poller Status Check";
            results[0]["Status As Of"] = "" + DateTime.Now;
            results[0]["Active"] = "" + active;
            results[0]["Folder"] = Context.Mapping.LocalFolder;
            results[0]["Vault"] = Context.Mapping.VaultName;
            results[0]["Should Sync"] = "" + Sync.SynchronizingWithServer;
            results[0]["Is Synchronizing"] = "" + (Context.Client != null || Context.Manager != null);
            results[0]["Topic ARN"] = Context.Mapping.NotificationTopicARN;
            results[0]["Queue URL"] = Context.Mapping.NotificationQueueURL;
            results[0]["Vault ARN"] = Context.Vault.VaultARN;
            results[0]["Inventory Date"] = "" + Context.Vault.InventoryDate;
            results[0]["Archive Count"] = "" + Context.Vault.Uploaded.Count;
            results[0]["Upload Count"] = "" + Context.Vault.UploadCount;
            results[0]["Upload Bytes"] = "" + Context.Vault.UploadBytes;
            results[0]["Upload Errors"] = "" + Context.Vault.UploadErrors.Count;
            results[0]["Pending Upload"] = "" + Context.Vault.ToUpload.Count;
            results[0]["Download Count"] = "" + Context.Vault.DownloadCount;
            results[0]["Download Bytes"] = "" + Context.Vault.DownloadBytes;
            results[0]["Pending Download"] = "" + Context.Vault.ToDownload.Count;
            results[0]["Delete Count"] = "" + Context.Vault.DeleteCount;
            results[0]["Delete Errors"] = "" + Context.Vault.DeleteErrors.Count;
            results[0]["Pending Delete"] = "" + Context.Vault.ToDelete.Count;

            return results;
        }

        public void sendStatusEmail()
        {
            showResults(true, showStatus());
        }

        private void completeJob(Dictionary<string, string> properties, string action)
        {
            FolderVaultMapping mapping = Context.Mapping;
            string jobId = properties.ContainsKey("JobId") ? properties["JobId"] : null;

            if (jobId != null)
            {
                using (AmazonGlacierClient client = new AmazonGlacierClient(mapping.AccessKey, mapping.SecretKey, mapping.Endpoint))
                {
                    GetJobOutputRequest gReq = new GetJobOutputRequest();
                    GetJobOutputResponse gResp;
                    GetJobOutputResult gResult;

                    gReq.AccountId = "-";
                    gReq.JobId = jobId;
                    gReq.VaultName = mapping.VaultName;
                    gResp = client.GetJobOutput(gReq);
                    gResult = gResp.GetJobOutputResult;

                    using (Stream input = gResult.Body)
                    {
                        if (action == "InventoryRetrieval")
                        {
                            handleInventory(properties, input);
                        }
                        else if (action == "ArchiveRetrieval")
                        {
                            handleArchive(properties, input);
                        }
                    }
                }
            }
        }

        private void handleInventory(Dictionary<string, string> properties, Stream body)
        {
            JsonData val = null;

            using (StreamReader reader = new StreamReader(body))
            {
                val = JsonMapper.ToObject(reader);
            }

            Context.Vault.updateInventoryFromServer(val);
        }

        private void handleArchive(Dictionary<string, string> properties, Stream body)
        {
            string archiveId = properties.ContainsKey("ArchiveId") ? properties["ArchiveId"] : null;
            string jobId = properties.ContainsKey("JobId") ? properties["JobId"] : null;
            string tBytes = properties.ContainsKey("ArchiveSizeInBytes") ? properties["ArchiveSizeInBytes"] : null;

            long totalBytes;
            long.TryParse(tBytes, out totalBytes);

            Context.Vault.completeDownload(Context.Mapping, archiveId, jobId, totalBytes, body);
        }

        public Dictionary<string, string>[] listJobs()
        {
            List<GlacierJobDescription> jobs = JobExecutor.listJobs(Context.Mapping);
            Dictionary<string, string>[] results = new Dictionary<string,string>[jobs.Count];
            GlacierJobDescription job;

            for (int i = 0; i < jobs.Count; i++)
            {
                job = jobs[i];
                results[i] = new Dictionary<string, string>();
                results[i]["ConsoleTitle"] = "Job - " + job.JobId;
                results[i]["Description"] = job.JobDescription;
                results[i]["Action"] = job.Action;
                results[i]["ArchiveId"] = job.ArchiveId;
                results[i]["Archive Size"] = "" + job.ArchiveSizeInBytes;
                results[i]["Completed"] = "" + job.Completed;
                results[i]["Completed At"] = "" + job.CompletionDate;
                results[i]["Created At"] = "" + job.CreationDate;
                results[i]["Inventory Size"] = "" + job.InventorySizeInBytes;
                results[i]["Tree Hash"] = job.SHA256TreeHash;
                results[i]["SNS Topic"] = job.SNSTopic;
                results[i]["Status Code"] = job.StatusCode;
                results[i]["Status Message"] = job.StatusMessage;
                results[i]["Vault ARN"] = job.VaultARN;
            }

            return results;
        }

        private Dictionary<string, string>[] describeJob(string jobId)
        {
            Dictionary<string, string>[] results = new Dictionary<string, string>[1];
            DescribeJobResult job = JobExecutor.describeJob(Context.Mapping, jobId);

            results[0] = new Dictionary<string, string>();
            results[0]["ConsoleTitle"] = "Job - " + job.JobId;
            results[0]["Description"] = job.JobDescription;
            results[0]["Action"] = job.Action;
            results[0]["ArchiveId"] = job.ArchiveId;
            results[0]["Archive Size"] = "" + job.ArchiveSizeInBytes;
            results[0]["Completed"] = "" + job.Completed;
            results[0]["Completed At"] = "" + job.CompletionDate;
            results[0]["Created At"] = "" + job.CreationDate;
            results[0]["Inventory Size"] = "" + job.InventorySizeInBytes;
            results[0]["Tree Hash"] = job.SHA256TreeHash;
            results[0]["SNS Topic"] = job.SNSTopic;
            results[0]["Status Code"] = job.StatusCode;
            results[0]["Status Message"] = job.StatusMessage;
            results[0]["Vault ARN"] = job.VaultARN;

            return results;
        }

        private void showResults(QueueMessage message, Dictionary<string, string>[] results)
        {
            showResults(message.Properties["PublishToEmail"].Equals("true", StringComparison.InvariantCultureIgnoreCase) , results);
        }

        private void showResults(bool publishToEmail, Dictionary<string, string>[] results)
        {
            FolderVaultMapping mapping = Context.Mapping;

            foreach(Dictionary<string, string> result in results)
            {
                if(mapping == null)
                {
                    Console.WriteLine(result["ConsoleTitle"]);
                    foreach(KeyValuePair<string, string> pair in result)
                    {
                        if(pair.Key != "ConsoleTitle")
                        {
                            Console.WriteLine("    " + pair.Key + ": " + pair.Value);
                        }
                    }
                    Console.WriteLine();
                }
                else
                {
                    result.Remove("ConsoleTitle");
                    new Notification("ResultNotice", result).publish(mapping, publishToEmail);
                }
            }
        }
    }
}
