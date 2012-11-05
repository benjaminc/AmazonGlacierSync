using Amazon.Glacier;
using Amazon.Glacier.Transfer;
using AwsJob;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AwsFileSync
{
    class VaultContext
    {
        private volatile bool disposed = false;

        public FolderVaultMapping Mapping { get; private set; }
        public Vault Vault { get; private set; }
        public AmazonGlacierClient Client { get; private set; }
        public ArchiveTransferManager Manager { get; private set; }

        public VaultContext(FolderVaultMapping mapping) : this(mapping, null)
        {
        }

        public VaultContext(FolderVaultMapping mapping, Vault inventory)
        {
            Mapping = mapping;
            Vault = inventory == null ? new Vault(mapping.VaultName) : inventory;
        }

        public void StartSession()
        {
            StopSession();

            disposed = false;
            Client = new AmazonGlacierClient(Mapping.AccessKey, Mapping.SecretKey, Mapping.Endpoint);
            Manager = new ArchiveTransferManager(Client);
        }

        public void StopSession()
        {
            if (disposed)
            {
                return;
            }

            try
            {
                if (Manager != null)
                {
                    Manager.Dispose();
                    Manager = null;
                }
            }
            finally
            {
                if (Client != null)
                {
                    Client.Dispose();
                    Client = null;
                }
            }

            disposed = true;
        }
    }
}
