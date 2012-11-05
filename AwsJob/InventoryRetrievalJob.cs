using Amazon.Glacier.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AwsJob
{
    public class InventoryRetrievalJob : JobExecutor
    {
        public InventoryRetrievalJob(FolderVaultMapping mapping, Func<bool> keepRunning)
            : this(mapping, keepRunning, false)
        {
        }
        public InventoryRetrievalJob(FolderVaultMapping mapping, Func<bool> keepRunning, bool sendJobScheduledNotification)
            : base(mapping, keepRunning, sendJobScheduledNotification)
        {
        }

        protected override JobParameters getJobParameters()
        {
            JobParameters jp = new JobParameters();
            jp.Description = "Inventory Retrieval Job " + DateTime.Now;
            jp.SNSTopic = Mapping.NotificationTopicARN;
            jp.Type = "inventory-retrieval";
            return jp;
        }

        protected override Notification getJobScheduledNotification(string jobId)
        {
            Dictionary<string, string> properties = new Dictionary<string, string>();
            properties["JobId"] = jobId;
            properties["ScheduledAt"] = DateFormat.formatDateTime(DateTime.Now, true);
            return new Notification("InventoryRetrievalStarted", properties);
        }
    }
}
