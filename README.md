# AzureStorageWebApp

ASP.NET Core 8 MVC application demonstrating Azure Storage services.

## Technologies

* .NET 8
* ASP.NET Core MVC
* Visual Studio 2022
* Microsoft Azure

## Azure Services

| Service             | Purpose              |
| ------------------- | -------------------- |
| Azure Table Storage | Customers & Products |
| Azure Blob Storage  | Images & Multimedia  |
| Azure Queue Storage | Orders & Inventory   |
| Azure Files         | Application Logs     |
| Azure App Service   | Web Hosting          |

## Storage Resources

```text
CustomerProfiles
Products
productmedia
orderprocessing
applicationlogs
```

## Running Locally

1. Open `AzureStorageWebApp.sln` in Visual Studio.
2. Configure the Azure Storage connection string using User Secrets.
3. Build the project.
4. Press https.
5. Test Customers, Products, Blobs, Queues and Files.

## Azure Configuration

In Azure App Service, add:

```text
AzureStorage__ConnectionString
```

Also configure:

```text
AzureStorage__BlobContainer = productmedia
AzureStorage__QueueName = orderprocessing
AzureStorage__FileShare = applicationlogs
AzureStorage__CustomerTable = CustomerProfiles
AzureStorage__ProductTable = Products
```

## Final Deployment

Once an approved App Service is provided:

```text
Visual Studio
→ Publish
→ Azure
→ Azure App Service
→ Existing
→ Select App Service
→ Publish
```

The application can then connect to the existing Azure Storage resources.

### Security

Never commit Azure Storage connection strings, passwords, or keys to GitHub.
