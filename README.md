# az-snapshot (A Fork of dotnet-az-snapshot-tool)

Command line tool that creates Azure managed disk snapshots.

Main motivation was to be able to automate the creation of snapshots **without** using Runbooks and/or having to use Azure Backup with a Virtual Machine.

## Installation

`dotnet tool install --global az-snapshot`

## Usage

Available arguments:

- **--tenantId (-t)**: (Required) Azure Tenant ID of the user credentials used to create the snapshot.
- **--subscriptionId (-s)**: (Required) Azure subscription ID of the source disk for the snapshot.
- **--resourceGroup (-g)**: (Required) Resource group of the source disk for the snapshot.
- **--targetsubscriptionId (-i)**: (Required) Azure subscription ID of the target resource group for the snapshot.
- **--targetresourceGroup (-o)**: (Required) Resource group in which the snapshot will be created.
- **--diskName (-n)**: (Required) Name of the source managed disk name. (ex: pvc-xxxx-xxxx-xxxxxxx) 
- **--snapshotName (-f)**: (Required) Defines the name of the snapshot resource.
- **--retainLimit (-l)**: Limits the retained snapshots to specified count.  Default is unlimited (0).
- **--skuType (-k)**: Snapshot sku type.  Available values are 'Standard_LRS' or 'Premium_LRS'. Default is 'Standard_LRS'..

### Example

```shell script
az-snapshot --tenantId xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx --subscriptionId xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx --targetsubscriptionId xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx --resourceGroup disksourcerg --targetresourceGroup targetsnapshotrg --diskName pvc-xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx --snapshotName testsnapshot
```

or

```shell script
az-snapshot -t xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx -s xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx -i xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx -g disksourcerg -o targetsnapshotrg -n pvc-xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx -f testsnapshot
```

To retain 7 latest snapshot values (including latest):

```shell script
az-snapshot -t xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx -s xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx -i xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx -g disksourcerg -o targetsnapshotrg -n pvc-xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx -f testsnapshot --retainLimit 7
```

### Details on authentication

This tool uses `AzureDefaultCredentials` which tries multiple credentials types in order, including environment variables, managed identity and az cli.
See [here](https://docs.microsoft.com/en-us/dotnet/api/azure.identity.defaultazurecredential?view=azure-dotnet) for more details.

### Using with a CronJob in Kubernetes

To easily automate the creation on snapshot in Kubernetes, use the [CronJob](https://kubernetes.io/docs/tasks/job/automated-tasks-with-cron-jobs/) resource.
Here's an example using environment variables to provide the Azure Active Directory Application credentials.  It retains the last 7 days of snapshots:

_note: you can also use [aad-pod-identity](https://github.com/Azure/aad-pod-identity)_

```yaml
apiVersion: batch/v1beta1
kind: CronJob
metadata:
  name: snapshot-job
spec:
  schedule: "@daily"
  jobTemplate:
    spec:
      template:
        spec:
          containers:
          - name: snapshot
            image: mcr.microsoft.com/dotnet/core/sdk:3.1
            args:
            - /bin/sh
            - -c
            - >-
                dotnet tool install --tool-path . dotnet-az-snapshot-tool;
                ./az-snapshot-tool run
                -t xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
                -s xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx 
                -i xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx 
                -o targetsnapshotrg 
                -g disksourcerg
                -n pvc-xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
                -f data-test-0
                -l 7
            env:
            - name: AZURE_TENANT_ID
              value: xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
            - name: AZURE_CLIENT_ID
              value: xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
            - name: AZURE_CLIENT_SECRET
              valueFrom:
                secretKeyRef:
                  name: mySecret
                  key: mySecretKey
          restartPolicy: OnFailure
```
