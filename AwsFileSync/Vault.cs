using Amazon.Glacier;
using Amazon.Glacier.Model;
using Amazon.Glacier.Transfer;
using AwsJob;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using ThirdParty.Json.LitJson;

namespace AwsFileSync
{
    class Vault
    {
        public static Vault parse(XmlReader xml)
        {
            string vaultARN = null, vaultName = null;
            List<Archive> archives = new List<Archive>();
            DateTime? inventoryDate = null, saveDate = null;
            long uploadCount = 0, uploadBytes = 0, downloadCount = 0, downloadBytes = 0, deleteCount = 0;
            bool resetStats;
            Archive arch;

            while (xml.Read() && !xml.EOF)
            {
                if (xml.IsStartElement())
                {
                    if (xml.LocalName == "VaultARN")
                    {
                        vaultARN = xml.IsEmptyElement ? null : xml.ReadElementContentAsString();
                    }
                    else if (xml.LocalName == "VaultName")
                    {
                        vaultName = xml.IsEmptyElement ? null : xml.ReadElementContentAsString();
                    }
                    else if (xml.LocalName == "UploadCount")
                    {
                        long.TryParse(xml.IsEmptyElement ? null : xml.ReadElementContentAsString(), out uploadCount);
                    }
                    else if (xml.LocalName == "UploadBytes")
                    {
                        long.TryParse(xml.IsEmptyElement ? null : xml.ReadElementContentAsString(), out uploadBytes);
                    }
                    else if (xml.LocalName == "DownloadCount")
                    {
                        long.TryParse(xml.IsEmptyElement ? null : xml.ReadElementContentAsString(), out downloadCount);
                    }
                    else if (xml.LocalName == "DownloadBytes")
                    {
                        long.TryParse(xml.IsEmptyElement ? null : xml.ReadElementContentAsString(), out downloadBytes);
                    }
                    else if (xml.LocalName == "DeleteCount")
                    {
                        long.TryParse(xml.IsEmptyElement ? null : xml.ReadElementContentAsString(), out deleteCount);
                    }
                    else if (xml.LocalName == "InventoryDate")
                    {
                        inventoryDate = DateFormat.parseDateTime(xml.IsEmptyElement ? null : xml.ReadElementContentAsString());
                    }
                    else if (xml.LocalName == "SaveDate")
                    {
                        saveDate = DateFormat.parseDateTime(xml.IsEmptyElement ? null : xml.ReadElementContentAsString());
                    }
                    else if (xml.LocalName == "ArchiveList" && !xml.IsEmptyElement)
                    {
                        while (xml.Read() && !xml.EOF)
                        {
                            if (xml.IsStartElement() && xml.LocalName == "Archive" && !xml.IsEmptyElement)
                            {
                                arch = Archive.parse(xml);
                                if (arch != null)
                                {
                                    archives.Add(arch);
                                }
                            }
                            else if (xml.NodeType == XmlNodeType.EndElement && xml.LocalName == "ArchiveList")
                            {
                                break;
                            }
                        }
                    }
                }
                else if (xml.NodeType == XmlNodeType.EndElement && xml.LocalName == "Vault")
                {
                    break;
                }
            }

            if (vaultName != null)
            {
                DateTime now = DateTime.Now;
                resetStats = saveDate != null && saveDate.HasValue &&
                    (saveDate.Value.Year < now.Year || (saveDate.Value.Year == now.Year && saveDate.Value.Month < now.Month));

                return new Vault(vaultName,
                    vaultARN,
                    inventoryDate,
                    resetStats ? 0 : uploadCount,
                    resetStats ? 0 : uploadBytes,
                    resetStats ? 0 : downloadCount,
                    resetStats ? 0 : downloadBytes,
                    resetStats ? 0 : deleteCount,
                    archives);
            }
            else
            {
                return null;
            }
        }

        private volatile bool loadedFromFile;
        private volatile bool loadedFromServer;
        private volatile bool isDirty;
        private InventoryRetrievalJob job;
        private Dictionary<string, List<Archive>> inventoryByPath = new Dictionary<string, List<Archive>>();
        private Dictionary<string, Archive> inventoryById = new Dictionary<string, Archive>();
        private List<Archive> toDelete = new List<Archive>();
        private Dictionary<Archive, Exception> deleteErrors = new Dictionary<Archive, Exception>();
        private List<Archive> toUpload = new List<Archive>();
        private Dictionary<Archive, Exception> uploadErrors = new Dictionary<Archive, Exception>();
        private List<Archive> toDownload = new List<Archive>();

        public string VaultName { get; private set; }
        public string VaultARN { get; private set; }
        public DateTime? InventoryDate { get; private set; }
        public long UploadCount { get; private set; }
        public long UploadBytes { get; private set; }
        public long DownloadCount { get; private set; }
        public long DownloadBytes { get; private set; }
        public long DeleteCount { get; private set; }
        public TimeSpan SaveTime { get; private set; }
        public TimeSpan UploadTime { get; private set; }
        public TimeSpan DownloadTime { get; private set; }
        public TimeSpan DeleteTime { get; private set; }
        public IList<Archive> ToDownload
        {
            get
            {
                List<Archive> val = new List<Archive>(toDownload);
                val.Sort();
                return val.AsReadOnly();
            }
        }
        public IList<Archive> ToDelete
        {
            get
            {
                List<Archive> val = new List<Archive>(toDelete);
                val.Sort();
                return val.AsReadOnly();
            }
        }
        public IDictionary<Archive, Exception> DeleteErrors
        {
            get
            {
                SortedDictionary<Archive, Exception> val = new SortedDictionary<Archive, Exception>(deleteErrors);
                return val;
            }
        }
        public IList<Archive> ToUpload
        {
            get
            {
                List<Archive> val = new List<Archive>(toUpload);
                val.Sort();
                return val.AsReadOnly();
            }
        }
        public IDictionary<Archive, Exception> UploadErrors
        {
            get
            {
                SortedDictionary<Archive, Exception> val = new SortedDictionary<Archive, Exception>(uploadErrors);
                return val;
            }
        }
        public IList<Archive> Uploaded
        {
            get
            {
                List<Archive> uploaded = new List<Archive>(inventoryById.Values);

                uploaded.Sort();

                return uploaded.AsReadOnly();
            }
        }

        public Vault(string vaultName)
        {
            this.VaultName = vaultName;
        }
        private Vault(string vaultName,
            string vaultARN,
            DateTime? inventoryDate,
            long uploadCount,
            long uploadBytes,
            long downloadCount,
            long downloadBytes,
            long deleteCount,
            List<Archive> archives)
        {
            VaultName = vaultName;
            VaultARN = vaultARN;
            InventoryDate = inventoryDate;
            UploadCount = uploadCount;
            UploadBytes = uploadBytes;
            DownloadCount = downloadCount;
            DownloadBytes = downloadBytes;
            DeleteCount = deleteCount;
            archives.ForEach(addNewArchive);
            loadedFromFile = true;
        }

        public void beginLoad(VaultContext context, Func<bool> keepRunning)
        {
            if (job != null)
            {
                throw new InvalidOperationException("You cannot call beginLoad twice without calling endLoad in between");
            }

            job = new InventoryRetrievalJob(context.Mapping, keepRunning);
            job.run();
        }

        public void updateInventoryFromServer(JsonData serverInventory)
        {
            if (serverInventory == null || !serverInventory.IsObject)
            {
                return;
            }

            JsonData data;
            DateTime? val = DateFormat.parseDateTime(serverInventory["InventoryDate"]);
            VaultARN = (data = serverInventory["VaultARN"]) != null && data.IsString ? data.ToString() : null;

            if (val != null && val.HasValue && InventoryDate != val.Value)
            {
                InventoryDate = val.Value;
                isDirty = true;
            }

            HashSet<string> serverIds = new HashSet<string>();
            JsonData list = serverInventory["ArchiveList"];
            string id;

            if (list != null && list.IsArray)
            {
                foreach (JsonData archive in list)
                {
                    id = (data = archive["ArchiveId"]) != null && data.IsString ? data.ToString() : null;

                    if (id != null)
                    {
                        serverIds.Add(id);

                        if (inventoryById.ContainsKey(id))
                        {
                            inventoryById[id].updateFromServerInventory(archive);
                        }
                        else
                        {
                            addNewArchive(new Archive(archive));
                        }
                    }
                }
            }

            List<string> unmatched = new List<string>();
            foreach (string archiveId in inventoryById.Keys)
            {
                if (!serverIds.Contains(archiveId) && inventoryById[archiveId].CreationDate < DateTime.Now.Subtract(TimeSpan.FromDays(3)))
                {
                    unmatched.Add(archiveId);
                }
            }

            Dictionary<string, KeyValuePair<string, List<Archive>>> map = new Dictionary<string, KeyValuePair<string, List<Archive>>>();
            KeyValuePair<string, List<Archive>> pair;

            foreach (string archiveId in unmatched)
            {
                pair = new KeyValuePair<string, List<Archive>>();

                foreach (KeyValuePair<string, List<Archive>> archives in inventoryByPath)
                {
                    foreach (Archive arch in archives.Value)
                    {
                        if (arch.ArchiveId == archiveId)
                        {
                            pair = archives;
                            break;
                        }
                    }

                    if (pair.Value != null)
                    {
                        break;
                    }
                }

                map[archiveId] = pair;
            }

            foreach (KeyValuePair<string, KeyValuePair<string, List<Archive>>> entry in map)
            {
                if (entry.Value.Value != null)
                {
                    entry.Value.Value.Remove(inventoryById[entry.Key]);

                    if (entry.Value.Value.Count == 0)
                    {
                        inventoryByPath.Remove(entry.Value.Key);
                    }
                }

                inventoryById.Remove(entry.Key);
                isDirty = true;
            }

            loadedFromServer = true;
        }

        private void addNewArchive(Archive arch)
        {
            if (!inventoryByPath.ContainsKey(arch.RelativePath))
            {
                inventoryByPath[arch.RelativePath] = new List<Archive>();
            }

            inventoryByPath[arch.RelativePath].Add(arch);
            inventoryById[arch.ArchiveId] = arch;
            isDirty = true;
        }

        public void endLoad(Func<bool> keepRunning)
        {
            if (job == null)
            {
                throw new InvalidOperationException("You must call beginLoad before calling endLoad");
            }

            if (!loadedFromFile && !loadedFromServer)
            {
                while (keepRunning() && !loadedFromServer)
                {
                    try
                    {
                        Thread.Sleep(1000);
                    }
                    catch
                    {
                    }
                }
            }

            job = null;
        }

        public bool IsDirty
        {
            get
            {
                if (!isDirty)
                {
                    foreach (Archive arch in inventoryById.Values)
                    {
                        if (arch.IsDirty)
                        {
                            return true;
                        }
                    }
                }

                return isDirty;
            }
        }

        public void save(XmlWriter writer, FolderVaultMapping mapping)
        {
            DateTime start = DateTime.Now;

            writer.WriteStartElement("Vault");

            if (mapping != null)
            {
                writer.WriteElementString("LocalFolder", mapping.LocalFolder);
                writer.WriteElementString("AccessKey", mapping.AccessKey);
                writer.WriteElementString("SecretKey", mapping.SecretKey);
                writer.WriteElementString("NotificationQueueURL", mapping.NotificationQueueURL);
                writer.WriteElementString("NotificationTopicARN", mapping.NotificationTopicARN);
                writer.WriteElementString("Region", mapping.Region.ToString());
            }

            writer.WriteElementString("VaultARN", VaultARN);
            writer.WriteElementString("VaultName", VaultName);
            writer.WriteElementString("InventoryDate", DateFormat.formatDateTime(InventoryDate, true));
            writer.WriteElementString("SaveDate", DateFormat.formatDateTime(DateTime.Now, true));
            writer.WriteElementString("UploadCount", UploadCount.ToString());
            writer.WriteElementString("UploadBytes", UploadBytes.ToString());
            writer.WriteElementString("UploadTime", UploadTime.ToString());
            writer.WriteElementString("DownloadCount", DownloadCount.ToString());
            writer.WriteElementString("DownloadBytes", DownloadBytes.ToString());
            writer.WriteElementString("DownloadTime", DownloadTime.ToString());
            writer.WriteElementString("DeleteCount", DeleteCount.ToString());
            writer.WriteElementString("DeleteTime", DeleteTime.ToString());
            writer.WriteElementString("LoadedFromFile", loadedFromFile.ToString());
            writer.WriteElementString("LoadedFromServer", loadedFromServer.ToString());
            save(writer, "ArchiveList", Uploaded);
            save(writer, "ToDelete", ToDelete);
            save(writer, "ToUpload", ToUpload);
            save(writer, "DeleteErrors", DeleteErrors);
            save(writer, "UploadErrors", UploadErrors);
            writer.WriteEndElement();

            SaveTime += DateTime.Now.Subtract(start);
            isDirty = false;
        }

        private void save(XmlWriter writer, string wrappingElementName, IEnumerable<Archive> archives)
        {
            writer.WriteStartElement(wrappingElementName);

            foreach (Archive arch in archives)
            {
                arch.save(writer);
            }

            writer.WriteEndElement();
        }

        private void save(XmlWriter writer, string wrappingElementName, IDictionary<Archive, Exception> archives)
        {
            writer.WriteStartElement(wrappingElementName);

            foreach (KeyValuePair<Archive, Exception> arch in archives)
            {
                writer.WriteStartElement("ArchiveException");
                arch.Key.save(writer);
                writer.WriteElementString("Exception", arch.Value.ToString());
                writer.WriteEndElement();
            }

            writer.WriteEndElement();
        }

        public void uploadNewAndChanged(ArchiveTransferManager manager, string basePath, Dictionary<string, FileDetail> localFiles, Func<bool> keepRunning)
        {
            Archive arch;
            bool hasMatch;
            toUpload.Clear();

            foreach (FileDetail file in localFiles.Values)
            {
                if (!keepRunning())
                {
                    return;
                }

                if (!inventoryByPath.ContainsKey(file.RelativePath))
                {
                    arch = new Archive(file);
                    toUpload.Add(arch);
                }
                else
                {
                    hasMatch = false;

                    foreach (Archive a in inventoryByPath[file.RelativePath])
                    {
                        if (a.FileLength == file.FileLength && a.LastModified == file.LastModified)
                        {
                            hasMatch = true;
                        }
                    }

                    if (!hasMatch)
                    {
                        arch = new Archive(file);
                        toUpload.Add(arch);
                    }
                }
            }

            toUpload.Sort();
            toUpload.Reverse();

            Exception error;
            for (int i = toUpload.Count - 1; i >= 0 ; i--)
            {
                if (!keepRunning())
                {
                    return;
                }

                arch = toUpload[i];
                if ((error = uploadArchive(manager, arch, basePath)) == null)
                {
                    UploadCount++;
                }
                else
                {
                    uploadErrors[arch] = error;
                }
            }
        }

        public bool reuploadArchive(ArchiveTransferManager manager, string archiveId, string basePath)
        {
            Archive arch = inventoryById.ContainsKey(archiveId) ? inventoryById[archiveId] : null;

            if (arch != null)
            {
                arch = new Archive(arch);
                toUpload.Add(arch);
                return uploadArchive(manager, arch, basePath) == null;
            }

            return false;
        }

        private Exception uploadArchive(ArchiveTransferManager manager, Archive arch, string basePath)
        {
            int rep = 0;
            Exception error = null;
            isDirty = true;

            do
            {
                if (rep > 0)
                {
                    Thread.Sleep(2000);
                }

                try
                {
                    DateTime start = DateTime.Now;
                    arch.upload(manager, VaultName, basePath);
                    UploadTime += DateTime.Now.Subtract(start);
                    UploadBytes += arch.FileLength;

                    if (!inventoryByPath.ContainsKey(arch.RelativePath))
                    {
                        inventoryByPath[arch.RelativePath] = new List<Archive>();
                    }

                    inventoryByPath[arch.RelativePath].Add(arch);
                    inventoryById[arch.ArchiveId] = arch;
                    toUpload.Remove(arch);
                }
                catch(Exception ex)
                {
                    error = ex;
                }
            }
            while (++rep < 4 && error != null);

            return error;
        }

        public void downloadArchive(string archiveId, FolderVaultMapping mapping, Func<bool> keepRunning)
        {
            Archive arch = inventoryById.ContainsKey(archiveId) ? inventoryById[archiveId] : null;
            if (arch != null)
            {
                arch.beginDownload(mapping, keepRunning);
                toDownload.Add(arch);
                isDirty = true;
            }
        }

        public void recordDownloadScheduled(string archiveId, string jobId, DateTime scheduledAt)
        {
            Archive arch = inventoryById.ContainsKey(archiveId) ? inventoryById[archiveId] : null;
            if (arch != null)
            {
                arch.recordDownloadScheduled(jobId, scheduledAt);
                toDownload.Add(arch);
                isDirty = true;
            }
        }

        public void completeDownload(FolderVaultMapping mapping, string archiveId, string jobId, long totalBytes, Stream body)
        {
            Archive arch = inventoryById.ContainsKey(archiveId) ? inventoryById[archiveId] : null;

            if (arch != null)
            {
                DateTime start = DateTime.Now;

                try
                {
                    arch.endDownload(mapping, jobId, totalBytes, body);
                }
                finally
                {
                    DownloadBytes += arch.DownloadDetails != null ? arch.DownloadDetails.TransferredBytes : arch.FileLength;
                    DownloadTime += DateTime.Now.Subtract(start);
                    toDownload.Remove(arch);
                    isDirty = true;
                }
            }
        }

        public bool deleteArchive(ArchiveTransferManager manager, string archiveId)
        {
            KeyValuePair<string, List<Archive>> entry = new KeyValuePair<string,List<Archive>>();
            Archive archive = null;

            foreach (KeyValuePair<string, List<Archive>> archives in inventoryByPath)
            {
                foreach (Archive arch in archives.Value)
                {
                    if (arch.ArchiveId == archiveId)
                    {
                        archive = arch;
                        entry = archives;
                        break;
                    }
                }

                if (archive != null)
                {
                    break;
                }
            }

            return archive == null ? true : deleteArchive(manager, entry, archive) == null;
        }

        public void deleteRemovedAndOldVersions(ArchiveTransferManager manager, Dictionary<string, FileDetail> localFiles, Func<bool> keepRunning)
        {
            Dictionary<Archive, KeyValuePair<string, List<Archive>>> deleted = new Dictionary<Archive, KeyValuePair<string, List<Archive>>>();
            DateTime compare = DateTime.Now;
            TimeSpan maxAge = TimeSpan.FromDays(120.0);
            List<string> empty = new List<string>();
            bool hasGoodEntry;

            foreach (KeyValuePair<string, List<Archive>> archives in inventoryByPath)
            {
                if (!keepRunning())
                {
                    return;
                }

                hasGoodEntry = false;
                foreach (Archive archive in archives.Value)
                {
                    if (!keepRunning())
                    {
                        return;
                    }

                    if (hasGoodEntry || !localFiles.ContainsKey(archive.RelativePath) ||
                        localFiles[archive.RelativePath].FileLength != archive.FileLength ||
                        localFiles[archive.RelativePath].LastModified != archive.LastModified)
                    {
                        archive.DeletionDate = archive.DeletionDate == null ? DateTime.Now : archive.DeletionDate;

                        if (compare.Subtract((DateTime)archive.DeletionDate) > maxAge)
                        {
                            deleted[archive] = archives;
                        }
                    }
                    else
                    {
                        hasGoodEntry = true;
                    }
                }
            }

            toDelete = new List<Archive>(deleted.Keys);
            toDelete.Sort();
            toDelete.Reverse();

            Archive arch;
            Exception error;

            for(int i = toDelete.Count - 1; i >= 0; i--)
            {
                if (!keepRunning())
                {
                    return;
                }

                arch = toDelete[i];
                if ((error = deleteArchive(manager, deleted[arch], arch)) == null)
                {
                    DeleteCount++;
                }
                else
                {
                    deleteErrors[arch] = error;
                }
            }
        }

        private Exception deleteArchive(ArchiveTransferManager manager, KeyValuePair<string, List<Archive>> entryInThis, Archive archive)
        {
            int rep = 0;
            Exception error = null;
            isDirty = true;

            do
            {
                if (rep > 0)
                {
                    Thread.Sleep(2000);
                }

                try
                {
                    DateTime start = DateTime.Now;
                    manager.DeleteArchive(VaultName, archive.ArchiveId);
                    DeleteTime += DateTime.Now.Subtract(start);
                    entryInThis.Value.Remove(archive);
                    inventoryById.Remove(archive.ArchiveId);

                    if (entryInThis.Value.Count == 0)
                    {
                        inventoryByPath.Remove(entryInThis.Key);
                    }

                    toDelete.Remove(archive);
                }
                catch(Exception ex)
                {
                    error = ex;
                }
            }
            while (++rep < 4 && error != null);

            return error;
        }
    }
}
