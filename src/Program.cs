using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
using CommandLine;
using Microsoft.Azure.Management.Compute.Fluent;
using Microsoft.Azure.Management.Compute.Fluent.Models;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using Microsoft.Rest;

namespace AzureDiskSnapshotTool
{
    class Program
    {
        static async Task Main(string[] args)
        {
            await Parser.Default.ParseArguments<CreateOptions>(args)
                .WithParsedAsync(async options =>
                {
                    var azureCredentials = await GetCredentials(options.TenantId);

                    var azureSource = Microsoft.Azure.Management.Fluent.Azure
                        .Configure()
                        .Authenticate(azureCredentials)
                        .WithSubscription(options.SubscriptionId);

                    var disk = await azureSource.Disks.GetByIdAsync(options.ManagedDiskId);
                    string timeformat= "yy-MM-dd.hh.mm.ss";
                    var snapshotName = String.Format (options.Snapshot + "_" + DateTime.Now.ToString(timeformat));
                    Console.WriteLine($"Creating snapshot '{snapshotName}'...");

                    var azure = Microsoft.Azure.Management.Fluent.Azure
                        .Configure()
                        .Authenticate(azureCredentials)
                        .WithSubscription(options.TargetSubscriptionId);

                    var stopwatch = Stopwatch.StartNew();
                    await azure.Snapshots.Define(snapshotName)
                        .WithRegion(disk.Region)
                        .WithExistingResourceGroup(options.TargetResourceGroup)
                        .WithDataFromDisk(disk)
                        .WithSku(SnapshotSkuType.FromStorageAccountType(SnapshotStorageAccountTypes.Parse(options.SnapshotSkuType)))
                        .CreateAsync();
                    stopwatch.Stop();
                    Console.WriteLine($"Done creating snapshot in {stopwatch.Elapsed:g}.");
                    if (options.RetainLimit > 0)
                    {
                        var existingSnapshots = await azure.Snapshots.ListByResourceGroupAsync(options.TargetResourceGroup);
                        var retainedSnapshots = existingSnapshots.Where(a => a.Name.Contains(snapshotName.Split('_')[0].Trim()) )
                            .OrderByDescending(x => x.Inner.TimeCreated)
                            .Take(options.RetainLimit);

                        var discardedSnapshotIds = existingSnapshots.Where(a => a.Name.Contains(snapshotName.Split('_')[0].Trim()) )
                            .Except(retainedSnapshots)
                            .Select(x => x.Id)
                            .ToArray();

                        if (discardedSnapshotIds.Any())
                        {
                            Console.WriteLine(
                                $"Retaining {options.RetainLimit} snapshot(s) and discarding {discardedSnapshotIds.Length} snapshot(s)...");

                            stopwatch.Restart();
                            await azure.Snapshots.DeleteByIdsAsync(discardedSnapshotIds);
                            stopwatch.Stop();
                            Console.WriteLine($"Done discarding snapshot(s) in {stopwatch.Elapsed:g}.");
                        }
                    }
                });
        }

        private static async Task<AzureCredentials> GetCredentials(string tenantId)
        {
            var scopes = new[] {"https://management.azure.com/.default"};

            var defaultAzureCredential = new DefaultAzureCredential();

            var accessToken = await defaultAzureCredential.GetTokenAsync(new TokenRequestContext(scopes));

            var tokenCredentials = new TokenCredentials(accessToken.Token);

            return new AzureCredentials(
                tokenCredentials,
                tokenCredentials,
                tenantId,
                AzureEnvironment.AzureGlobalCloud);
        }
    }

    [Verb("create", HelpText = "Creates a snapshot of the specified managed disk.")]
    internal sealed class CreateOptions
    {

        private string _TargetResourceGroup = "";
        private string _resourceGroup = "";
        private string _TenantId = "";
        private string _SubscriptionId = "";
        private string _TargetSubscriptionId = "";
        private string _DiskName = "";
        private string _Snapshot = "";
        private string _SnapshotSkuType = "";


        [Option(shortName: 't', longName: "tenantId", Required = true, HelpText = "Tenant ID against which to authenticate the current Azure credentials.")]
        public string TenantId { get {return _TenantId.Trim();} set{_TenantId=value;} }

        [Option(shortName: 's', longName: "subscriptionId", Required = true, HelpText = "Subscription ID in which the managed disk exists.")]
        public string SubscriptionId { get {return _SubscriptionId.Trim();} set{_SubscriptionId=value;} }

        [Option(shortName: 'i', longName: "targetsubscriptionId", Required = true, HelpText = "Subscription ID in which the the snapshot will be created.")]
        public string TargetSubscriptionId { get {return _TargetSubscriptionId.Trim();} set{_TargetSubscriptionId=value;} }

        [Option(shortName: 'g', longName: "resourceGroup", Required = true, HelpText = "Resource group in which the managed disk exists.")]
        public string ResourceGroup {get {return _resourceGroup.Trim();} set{_resourceGroup=value;} }

        [Option(shortName: 'o', longName: "targetresourceGroup", Required = true, HelpText = "Resource group in which the snapshot will be created.")]
        public string TargetResourceGroup {get {return _TargetResourceGroup.Trim();} set{_TargetResourceGroup=value;} }

        [Option(shortName: 'n', longName: "diskName", Required = true, HelpText = "Name of the managed disk from which to take a snapshot.")]
        public string DiskName { get {return _DiskName.Trim();} set{_DiskName=value;} }

        [Option(shortName: 'f', longName: "snapshotName", Required = true, HelpText = "Defines the name of the snapshot resource.")]
        public string Snapshot { get {return _Snapshot.Trim();} set{_Snapshot=value;} }

        [Option(shortName: 'l', longName: "retainLimit", Default = 0, HelpText = "Limits the retained snapshots to specified count.  Default is unlimited (0).")]
        public int RetainLimit { get; set; }

        [Option(shortName: 'k', longName: "skuType",  Default = "Standard_LRS", HelpText = "Snapshot sku type.  Available values are 'Standard_LRS' or 'Premium_LRS'. Default is 'Standard_LRS'.")]
        public string SnapshotSkuType { get {return _SnapshotSkuType.Trim();} set{_SnapshotSkuType=value;} }

        public string ManagedDiskId => $"/subscriptions/{this.SubscriptionId.Trim()}/resourceGroups/{this.ResourceGroup.Trim()}/providers/Microsoft.Compute/disks/{this.DiskName.Trim()}";

    }
}
