using Amazon;
using Amazon.Glacier.Transfer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AwsJob
{
    public class FolderVaultMapping
    {
        public FolderVaultMapping()
        {
        }

        public FolderVaultMapping(FolderVaultMapping mapping)
        {
            this.LocalFolder = mapping.LocalFolder;
            this.VaultName = mapping.VaultName;
            this.Region = mapping.Region;
            this.NotificationTopicARN = mapping.NotificationTopicARN;
            this.NotificationQueueURL = mapping.NotificationQueueURL;
            this.AccessKey = mapping.AccessKey;
            this.SecretKey = mapping.SecretKey;
        }

        public string LocalFolder { get; set; }
        public string VaultName { get; set; }
        public RegionEnum Region { get; set; }
        public string EmailTopicARN { get; set; }
        public string NotificationTopicARN { get; set; }
        public string NotificationQueueURL { get; set; }
        public string AccessKey { get; set; }
        public string SecretKey { get; set; }

        public RegionEndpoint Endpoint
        {
            get
            {
                switch (Region)
                {
                    case RegionEnum.Asia_Pacific_Singapore:
                        return RegionEndpoint.APSoutheast1;
                    case RegionEnum.Asia_Pacific_Tokyo:
                        return RegionEndpoint.APNortheast1;
                    case RegionEnum.Europe_West_Ireland:
                        return RegionEndpoint.EUWest1;
                    case RegionEnum.South_America_Sao_Paulo:
                        return RegionEndpoint.SAEast1;
                    case RegionEnum.United_States_East_Virginia:
                        return RegionEndpoint.USEast1;
                    case RegionEnum.United_States_West_North_California:
                        return RegionEndpoint.USWest1;
                    default:
                        return RegionEndpoint.USWest2;
                }
            }
        }
    }

    public enum RegionEnum
    {
        //
        // Summary:
        //     The Asia Pacific (Tokyo) endpoint.
        //     APNortheast1
        Asia_Pacific_Tokyo,

        //
        // Summary:
        //     The Asia Pacific (Singapore) endpoint.
        //     APSoutheast1
        Asia_Pacific_Singapore,

        //
        // Summary:
        //     The EU West (Ireland) endpoint.
        //     EUWest1
        Europe_West_Ireland,

        //
        // Summary:
        //     The South America (Sao Paulo)endpoint.
        //     SAEast1
        South_America_Sao_Paulo,

        //
        // Summary:
        //     The US East (Virginia) endpoint.
        //     USEast1
        United_States_East_Virginia,

        //
        // Summary:
        //     The US West (N. California) endpoint.
        //     USWest1
        United_States_West_North_California,

        //
        // Summary:
        //     The US West (Oregon) endpoint.
        //     USWest2
        United_States_West_Oregon
    }
}
