using Amazon.Glacier;
using Amazon.Glacier.Model;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AwsJob
{
    public abstract class JobExecutor
    {
        public FolderVaultMapping Mapping { get; private set; }
        public Func<bool> KeepRunning { get; private set; }
        public bool SendJobScheduledNotification { get; private set; }

        protected JobExecutor(FolderVaultMapping mapping, Func<bool> keepRunning, bool sendJobScheduledNotification)
        {
            Mapping = mapping;
            KeepRunning = keepRunning;
            SendJobScheduledNotification = sendJobScheduledNotification;
        }

        public string run()
        {
            if (!KeepRunning())
            {
                return null;
            }

            string jobId;
            using (AmazonGlacierClient client = new AmazonGlacierClient(Mapping.AccessKey, Mapping.SecretKey, Mapping.Endpoint))
            {
                InitiateJobRequest iReq = new InitiateJobRequest();

                iReq.AccountId = "-";
                iReq.VaultName = Mapping.VaultName;
                iReq.JobParameters = getJobParameters();

                try
                {
                    jobId = client.InitiateJob(iReq).InitiateJobResult.JobId;
                    if (SendJobScheduledNotification)
                    {
                        Notification notice = getJobScheduledNotification(jobId);
                        if (notice != null)
                        {
                            notice.publish(Mapping, false);
                        }
                    }
                }
                catch (ResourceNotFoundException)
                {
                    jobId = null;
                }
            }

            return jobId;
        }

        protected abstract JobParameters getJobParameters();
        protected abstract Notification getJobScheduledNotification(string jobId);

        public static List<GlacierJobDescription> listJobs(FolderVaultMapping mapping)
        {
            using (AmazonGlacierClient client = new AmazonGlacierClient(mapping.AccessKey, mapping.SecretKey, mapping.Endpoint))
            {
                ListJobsRequest req = new ListJobsRequest();
                req.AccountId = "-";
                req.Completed = false;
                req.VaultName = mapping.VaultName;

                return client.ListJobs(req).ListJobsResult.JobList;
            }
        }

        public static DescribeJobResult describeJob(FolderVaultMapping mapping, string jobId)
        {
            using (AmazonGlacierClient client = new AmazonGlacierClient(mapping.AccessKey, mapping.SecretKey, mapping.Endpoint))
            {
                DescribeJobRequest req = new DescribeJobRequest();
                req.AccountId = "-";
                req.JobId = jobId;
                req.VaultName = mapping.VaultName;

                return client.DescribeJob(req).DescribeJobResult;
            }
        }
    }
}
