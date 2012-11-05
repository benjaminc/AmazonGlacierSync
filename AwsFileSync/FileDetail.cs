using AwsJob;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AwsFileSync
{
    class FileDetail : IComparable<FileDetail>
    {
        public string RelativePath { get; private set; }
        public long FileLength { get; private set; }
        public DateTime LastModified { get; private set; }
        public bool IsDirty { get; protected set; }

        public FileDetail(string relativePath, long fileLength, DateTime lastModified)
        {
            RelativePath = relativePath;
            FileLength = fileLength;
            LastModified = lastModified;
        }
        protected FileDetail(string combinedProperties)
        {
            string[] props = combinedProperties.Split(',');
            DateTime? val = DateFormat.parseDateTime(props[0]);
            if (val != null && val.HasValue)
            {
                LastModified = val.Value;
            }
            FileLength = Int64.Parse(props[1]);
            RelativePath = props[2];
        }

        public int CompareTo(FileDetail other)
        {
            if (other == null)
            {
                return 1;
            }
            else if (other.RelativePath == null && RelativePath == null)
            {
                return 0;
            }
            else if (other.RelativePath == null)
            {
                return 1;
            }
            else if (RelativePath == null)
            {
                return -1;
            }
            else
            {
                return RelativePath.CompareTo(other.RelativePath);
            }
        }
    }
}
