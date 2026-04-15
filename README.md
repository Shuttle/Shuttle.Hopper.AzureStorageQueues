# Azure Storage Queues

## Installation

```bash
dotnet add package Shuttle.Hopper.AzureStorageQueues
```

In order to make use of the `AzureStorageQueue` you will need access to an Azure Storage account or use the [Azurite](https://docs.microsoft.com/en-us/azure/storage/common/storage-use-azurite) emulator for local Azure Storage development.

You may want to take a look at how to [get started with Azure Queue storage using .NET](https://docs.microsoft.com/en-us/azure/storage/queues/storage-dotnet-how-to-use-queues?tabs=dotnet).

## Configuration

The URI structure is `azuresq://configuration-name/queue-name`.

If the `StorageAccount` is not specified then the `ConnectionString` will be used. At least one of the two is required.  When `StorageAccount` is specified the `DefaultAzureCredential` will be used to authenticate.

```c#
services.AddHopper()
    .UseAzureStorageQueues(builder =>
    {
        builder.Configure("azure", options =>
        {
            options.StorageAccount = "devstoreaccount1";
            options.ConnectionString = "UseDevelopmentStorage=true";
            options.MaxMessages = 20;
            options.VisibilityTimeout = null;
        });
    });
```

The default JSON settings structure is as follows:

```json
{
  "Shuttle": {
    "AzureStorageQueues": {
      "azure": {
        "StorageAccount": "devstoreaccount1",
        "ConnectionString": "UseDevelopmentStorage=true",
        "MaxMessages": 32,
        "VisibilityTimeout": "00:00:30"
      }
    }
  }
}
```

## `AzureStorageQueueOptions`

| Segment / Argument | Default | Description |
| --- | --- | --- | 
| `StorageAccount` | | The name of the storage account. |
| `ConnectionString` | | The Azure Storage Queue endpoint to connect to. |
| `MaxMessages` | `32` | Specifies the number of messages to fetch from the queue (between 1 and 32). |
| `VisibilityTimeout` | `null` | The message visibility timeout that will be used for messages that fail processing. |
| `QueueClient` | `null` | A `QueueClientOptions` instance for specific client configuration. |
