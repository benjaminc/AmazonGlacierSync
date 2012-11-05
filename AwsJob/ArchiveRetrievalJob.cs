using Amazon.Glacier.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AwsJob
{
    public class ArchiveRetrievalJob : JobExecutor
    {
        public string ArchiveId { get; private set; }

        public ArchiveRetrievalJob(FolderVaultMapping mapping, Func<bool> keepRunning, string archiveId)
            : this(mapping, keepRunning, archiveId, false)
        {
        }
        public ArchiveRetrievalJob(FolderVaultMapping mapping, Func<bool> keepRunning, string archiveId, bool sendJobScheduledNotification)
            : base(mapping, keepRunning, sendJobScheduledNotification)
        {
            ArchiveId = archiveId;
        }
        protected override JobParameters getJobParameters()
        {
            JobParameters jp = new JobParameters();
            jp.ArchiveId = ArchiveId;
            jp.Description = "Archive Retrieval Job " + DateTime.Now + "-" + ArchiveId;
            jp.SNSTopic = Mapping.NotificationTopicARN;
            jp.Type = "archive-retrieval";
            return jp;
        }

        protected override Notification getJobScheduledNotification(string jobId)
        {
            Dictionary<string, string> properties = new Dictionary<string, string>();
            properties["ArchiveId"] = ArchiveId;
            properties["JobId"] = jobId;
            properties["ScheduledAt"] = DateFormat.formatDateTime(DateTime.Now, true);
            return new Notification("ArchiveRetrievalStarted", properties);
        }
    }
}
