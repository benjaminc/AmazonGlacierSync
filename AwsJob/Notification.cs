using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AwsJob
{
    public class Notification
    {
        public string Action { get; private set; }
        public Dictionary<string, string> Properties { get; private set; }
        public string MessageId { get; private set; }

        public Notification(string action, Dictionary<string, string> properties)
        {
            Action = action;
            Properties = properties == null ? new Dictionary<string, string>() : new Dictionary<string, string>(properties);
        }

        public void publish(FolderVaultMapping mapping, bool publishToEmailTopic)
        {
            StringBuilder message = new StringBuilder("{\"Action\":");
            message.Append(safeString(Action));
            foreach (KeyValuePair<string, string> pair in Properties)
            {
                message.Append(',');
                message.Append(safeString(pair.Key));
                message.Append(':');
                message.Append(safeString(pair.Value));
            }
            message.Append("}");

            using (AmazonSimpleNotificationServiceClient client = new AmazonSimpleNotificationServiceClient(mapping.AccessKey, mapping.SecretKey, mapping.Endpoint))
            {
                PublishRequest req = new PublishRequest();
                req.TopicArn = publishToEmailTopic ? mapping.EmailTopicARN : mapping.NotificationTopicARN;
                req.Message = message.ToString();
                MessageId = client.Publish(req).PublishResult.MessageId;
            }
        }

        private string safeString(string value)
        {
            return value == null ? "null" : ('"' + value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t") + '"');
        }
    }
}
