using AwsJob;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ThirdParty.Json.LitJson;

namespace AwsFileSync
{
    class QueueMessage
    {
        public string ReceiptHandle { get; private set; }
        public string MessageId { get; private set; }
        public string Action { get; private set; }
        public string JobId { get; private set; }
        public string ArchiveId { get; private set; }
        public DateTime? ScheduledAt { get; private set; }
        public JsonData Message { get; private set; }
        public Dictionary<string, string> Properties { get; private set; }
        public JsonData Body { get; private set; }
        public string BodyText { get; private set; }

        public QueueMessage(string receiptHeader, string messageId, string bodyText)
        {
            ReceiptHandle = receiptHeader;
            MessageId = messageId;
            BodyText = bodyText;

            try
            {
                Body = JsonMapper.ToObject(bodyText);
            }
            catch(Exception ex)
            {
                Console.Error.WriteLine(ex);
            }

            if (Body != null && Body.IsObject)
            {
                if (Body["Message"] != null && Body["Message"].IsString)
                {
                    try
                    {
                        Message = JsonMapper.ToObject(Body["Message"].ToString());
                    }
                    catch { }
                }

                if (Message != null && Message.IsObject)
                {
                    Properties = new Dictionary<string,string>();
                    JsonData data;

                    foreach (string key in ((IDictionary)Message).Keys)
                    {
                        data = Message[key];
                        Properties[key] = data == null ? null : data.ToString();
                    }

                    if (Properties.ContainsKey("Action") && Properties["Action"] != null)
                    {
                        Action = Properties["Action"].Trim();
                    }
                    if (Properties.ContainsKey("JobId") && Properties["JobId"] != null)
                    {
                        JobId = Properties["JobId"].Trim();
                    }
                    if (Properties.ContainsKey("ArchiveId") && Properties["ArchiveId"] != null)
                    {
                        ArchiveId = Properties["ArchiveId"].Trim();
                    }
                    if (Properties.ContainsKey("ScheduledAt") && Properties["ScheduledAt"] != null)
                    {
                        try
                        {
                            ScheduledAt = DateFormat.parseDateTime(Properties["ScheduledAt"].Trim());
                        }
                        catch { }
                    }
                }

                if ((Action == null || Action.Trim().Length == 0) && Body["Subject"] != null && Body["Subject"].IsString)
                {
                    Action = Body["Subject"].ToString().Trim();
                }
            }

            Action = Action == null || Action.Length == 0 ? null : Action;
        }
    }
}
