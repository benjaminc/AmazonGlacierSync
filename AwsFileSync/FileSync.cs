using Amazon;
using Amazon.Glacier;
using Amazon.Glacier.Model;
using Amazon.Glacier.Transfer;
using AwsFileSync.Properties;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AwsFileSync
{
    class FileSync
    {
        public void syncFiles(List<VaultContext> contexts, Func<bool> keepRunning)
        {
            foreach (VaultContext context in contexts)
            {
                try
                {
                    context.StartSession();

                    Dictionary<string, FileDetail> localFiles = listLocalContent(context.Mapping.LocalFolder);
                    context.Vault.endLoad(keepRunning);

                    context.Vault.uploadNewAndChanged(context.Manager, context.Mapping.LocalFolder, localFiles, keepRunning);
                    context.Vault.deleteRemovedAndOldVersions(context.Manager, localFiles, keepRunning);
                }
                catch
                {
                }
                finally
                {
                    context.StopSession();
                }
            }
        }

        private Dictionary<string, FileDetail> listLocalContent(string basePath)
        {
            Dictionary<string, FileDetail> localFiles = new Dictionary<string, FileDetail>();

            if (Directory.Exists(basePath))
            {
                basePath = Path.GetFullPath(basePath);
                if (!basePath.EndsWith("\\"))
                {
                    basePath += Path.DirectorySeparatorChar;
                }

                addDirectory(localFiles, basePath.Length, new DirectoryInfo(basePath));
            }

            return localFiles;
        }

        private void addDirectory(Dictionary<string, FileDetail> localFiles, int relativePathStart, DirectoryInfo directory)
        {
            FileDetail detail;

            foreach (FileInfo file in directory.GetFiles())
            {
                detail = new FileDetail(file.FullName.Substring(relativePathStart), file.Length, file.LastWriteTimeUtc);
                localFiles[detail.RelativePath] = detail;
            }

            foreach (DirectoryInfo child in directory.GetDirectories())
            {
                addDirectory(localFiles, relativePathStart, child);
            }
        }
    }
}
