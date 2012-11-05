using Amazon.Glacier;
using Amazon.Glacier.Model;
using Amazon.Glacier.Transfer;
using Amazon.Runtime;
using AwsJob;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using ThirdParty.Json.LitJson;

namespace AwsFileSync
{
    class Archive : FileDetail
    {
        private DateTime? deletionDate;

        public static Archive parse(XmlReader xml)
        {
            string content, archiveId = null, archiveDescription = null, sha256TreeHash = null;
            DateTime? creationDate = null, deletionDate = null;
            decimal? size = null;
            decimal tryVal;

            while (xml.Read() && !xml.EOF)
            {
                if (xml.IsStartElement())
                {
                    if (xml.LocalName == "ArchiveId")
                    {
                        archiveId = xml.IsEmptyElement ? null : xml.ReadElementContentAsString();
                    }
                    else if (xml.LocalName == "ArchiveDescription")
                    {
                        archiveDescription = xml.IsEmptyElement ? null : xml.ReadElementContentAsString();
                    }
                    else if (xml.LocalName == "CreationDate")
                    {
                        creationDate = DateFormat.parseDateTime(xml.IsEmptyElement ? null : xml.ReadElementContentAsString());
                    }
                    else if (xml.LocalName == "Size")
                    {
                        content = xml.IsEmptyElement ? null : xml.ReadElementContentAsString();
                        if (decimal.TryParse(content, out tryVal))
                        {
                            size = tryVal;
                        }
                    }
                    else if (xml.LocalName == "SHA256TreeHash")
                    {
                        sha256TreeHash = xml.IsEmptyElement ? null : xml.ReadElementContentAsString();
                    }
                    else if (xml.LocalName == "DeletionDate")
                    {
                        deletionDate = DateFormat.parseDateTime(xml.IsEmptyElement ? null : xml.ReadElementContentAsString());
                    }
                }
                else if (xml.NodeType == XmlNodeType.EndElement && xml.LocalName == "Archive")
                {
                    break;
                }
            }

            if (archiveDescription == null || creationDate == null || size == null)
            {
                return null;
            }
            else
            {
                return new Archive(archiveId, archiveDescription, creationDate.Value, size.Value, sha256TreeHash);
            }
        }

        public string ArchiveId { get; private set; }
        public string ArchiveDescription { get; private set; }
        public DateTime CreationDate { get; private set; }
        public decimal Size { get; private set; }
        public string SHA256TreeHash { get; private set; }
        public DateTime? DeletionDate
        {
            get { return deletionDate; }
            set
            {
                deletionDate = value;
                IsDirty = true;
            }
        }
        public DownloadInfo DownloadDetails { get; private set; }

        public Archive(JsonData inventoryEntry)
            : this(inventoryEntry["ArchiveId"].ToString(),
            inventoryEntry["ArchiveDescription"] == null ? null : inventoryEntry["ArchiveDescription"].ToString(),
            DateFormat.parseDateTime(inventoryEntry["CreationDate"]),
            decimal.Parse(inventoryEntry["Size"].ToString()),
            inventoryEntry["SHA256TreeHash"] == null ? null : inventoryEntry["SHA256TreeHash"].ToString())
        {
        }
        public Archive(string archiveId, string archiveDescription, DateTime? creationDate, decimal size, string sha256TreeHash)
            : base(archiveDescription)
        {
            ArchiveId = archiveId;
            ArchiveDescription = archiveDescription;
            CreationDate = creationDate != null && creationDate.HasValue ? creationDate.Value : DateTime.Now;
            Size = size;
            SHA256TreeHash = sha256TreeHash;
        }

        public Archive(FileDetail detail)
            : base(detail.RelativePath, detail.FileLength, detail.LastModified)
        {
            ArchiveDescription = DateFormat.formatDateTime(detail.LastModified, true) + "," + detail.FileLength + "," + detail.RelativePath;
            Size = detail.FileLength;
        }

        public void updateFromServerInventory(JsonData inventoryEntry)
        {
            DateTime? val = DateFormat.parseDateTime(inventoryEntry["CreationDate"]);
            JsonData data;

            if (val != null && val.HasValue && CreationDate != val.Value)
            {
                CreationDate = val.Value;
                IsDirty = true;
            }
            decimal oldSize = Size;
            string oldHash = SHA256TreeHash;

            Size = (data = inventoryEntry["Size"]) != null && (data.IsLong || data.IsInt) ? decimal.Parse(data.ToString()) : 0;
            SHA256TreeHash = (data = inventoryEntry["SHA256TreeHash"]) != null && data.IsString ? data.ToString() : null;

            if (oldSize != Size || oldHash != SHA256TreeHash)
            {
                IsDirty = true;
            }
        }

        public void upload(ArchiveTransferManager manager, string vaultName, string basePath)
        {
            ArchiveId = manager.Upload(vaultName, ArchiveDescription, basePath + "\\" + RelativePath).ArchiveId;
            CreationDate = DateTime.Now;
            IsDirty = true;
        }

        public void beginDownload(FolderVaultMapping mapping, Func<bool> keepRunning)
        {
            if (ArchiveId != null)
            {
                string jobId = new ArchiveRetrievalJob(mapping, keepRunning, ArchiveId).run();
                DownloadDetails = new DownloadInfo(jobId, DateTime.Now);
                IsDirty = true;
            }
        }
        public void recordDownloadScheduled(string jobId, DateTime scheduledAt)
        {
            DownloadDetails = new DownloadInfo(jobId, scheduledAt);
            IsDirty = true;
        }
        public void endDownload(FolderVaultMapping mapping, string jobId, long totalBytes, Stream body)
        {
            byte[] buffer = new byte[65536];
            int len;

            DownloadDetails = DownloadDetails == null ? new DownloadInfo(jobId, DateTime.Now) : DownloadDetails;
            DownloadDetails.StartedAt = DateTime.Now;
            DownloadDetails.TotalBytes = totalBytes;

            try
            {
                using (FileStream file = File.OpenWrite(mapping.LocalFolder + "\\" + RelativePath))
                {
                    while ((len = body.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        DownloadDetails.TransferredBytes += len;
                        file.Write(buffer, 0, len);
                    }

                    file.Flush();
                }
            }
            finally
            {
                DownloadDetails.CompletedAt = DateTime.Now;
                IsDirty = true;
            }
        }

        public void save(XmlWriter writer)
        {
            writer.WriteStartElement("Archive");
            writer.WriteElementString("ArchiveId", ArchiveId);
            writer.WriteElementString("ArchiveDescription", ArchiveDescription);
            writer.WriteElementString("CreationDate", DateFormat.formatDateTime(CreationDate, true));
            writer.WriteElementString("Size", Size.ToString());
            writer.WriteElementString("SHA256TreeHash", SHA256TreeHash);
            if (DeletionDate != null)
            {
                writer.WriteElementString("DeletionDate", DateFormat.formatDateTime(DeletionDate.Value, true));
            }
            if (DownloadDetails != null)
            {
                DownloadDetails.save(writer);
            }
            writer.WriteEndElement();

            IsDirty = false;
        }
    }

    public class DownloadInfo
    {
        public DateTime ScheduledAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int PercentDone
        {
            get { return TotalBytes > 0 ? (int)((TransferredBytes * 100) / TotalBytes) : 0; }
        }
        public long TotalBytes { get; set; }
        public long TransferredBytes { get; set; }
        public string JobId { get; set; }

        public DownloadInfo(string jobId, DateTime scheduledAt)
        {
            JobId = jobId;
            ScheduledAt = scheduledAt;
        }

        public void save(XmlWriter writer)
        {
            writer.WriteStartElement("Download");
            writer.WriteElementString("ScheduledAt", ScheduledAt.ToString());

            if (StartedAt != null && StartedAt.HasValue)
            {
                writer.WriteElementString("StartedAt", StartedAt.Value.ToString());
            }
            if (CompletedAt != null && CompletedAt.HasValue)
            {
                writer.WriteElementString("CompletedAt", CompletedAt.Value.ToString());
            }

            writer.WriteElementString("PercentDone", PercentDone.ToString());
            writer.WriteElementString("TotalBytes", TotalBytes.ToString());
            writer.WriteElementString("TransferredBytes", TransferredBytes.ToString());
            writer.WriteElementString("JobId", JobId);
            writer.WriteEndElement();
        }
    }
}
