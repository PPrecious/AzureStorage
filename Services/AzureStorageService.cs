using System.Text;
using System.Text.Json;

using Azure;
using Azure.Data.Tables;

using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

using Azure.Storage.Files.Shares;

using Azure.Storage.Queues;

using AzureStorageWebApp.Models;

namespace AzureStorageWebApp.Services;

public class AzureStorageService
{
    private readonly IConfiguration _config;

    private readonly TableServiceClient _tableService;

    private readonly BlobContainerClient _blobContainer;

    private readonly QueueClient _queue;

    private readonly ShareClient _share;


    // ============================================================
    // CONSTRUCTOR
    // ============================================================

    public AzureStorageService(
        IConfiguration config)
    {
        _config = config;

        var connectionString =
            config["AzureStorage:ConnectionString"];

        if (string.IsNullOrWhiteSpace(
            connectionString))
        {
            throw new InvalidOperationException(
                "Azure Storage connection string is missing. " +
                "Configure AzureStorage:ConnectionString " +
                "using User Secrets locally or " +
                "AzureStorage__ConnectionString in Azure App Service.");
        }


        // --------------------------------------------------------
        // AZURE TABLE STORAGE
        // --------------------------------------------------------

        _tableService =
            new TableServiceClient(
                connectionString);


        // --------------------------------------------------------
        // AZURE BLOB STORAGE
        // --------------------------------------------------------

        _blobContainer =
            new BlobContainerClient(
                connectionString,
                config["AzureStorage:BlobContainer"]
                    ?? "productmedia");


        // --------------------------------------------------------
        // AZURE QUEUE STORAGE
        // --------------------------------------------------------

        var queueOptions =
            new QueueClientOptions
            {
                MessageEncoding =
                    QueueMessageEncoding.None
            };

        _queue =
            new QueueClient(
                connectionString,
                config["AzureStorage:QueueName"]
                    ?? "orderprocessing",
                queueOptions);


        // --------------------------------------------------------
        // AZURE FILE STORAGE
        // --------------------------------------------------------

        var shareService =
            new ShareServiceClient(
                connectionString);

        _share =
            shareService.GetShareClient(
                config["AzureStorage:FileShare"]
                    ?? "applicationlogs");
    }


    // ============================================================
    // TABLE CLIENTS
    // ============================================================

    public TableClient Customers =>
        _tableService.GetTableClient(
            _config["AzureStorage:CustomerTable"]
                ?? "CustomerProfiles");


    public TableClient Products =>
        _tableService.GetTableClient(
            _config["AzureStorage:ProductTable"]
                ?? "Products");


    // ============================================================
    // CREATE / VERIFY STORAGE RESOURCES
    // ============================================================

    public async Task EnsureResourcesAsync(
        CancellationToken cancellationToken = default)
    {
        await Customers.CreateIfNotExistsAsync(
            cancellationToken);

        await Products.CreateIfNotExistsAsync(
            cancellationToken);

        await _blobContainer.CreateIfNotExistsAsync(
            cancellationToken:
                cancellationToken);

        await _queue.CreateIfNotExistsAsync(
            cancellationToken:
                cancellationToken);

        await _share.CreateIfNotExistsAsync(
            cancellationToken:
                cancellationToken);
    }


    // ============================================================
    // CUSTOMERS — AZURE TABLE STORAGE
    // ============================================================

    public async Task<List<CustomerEntity>>
        GetCustomersAsync(
            CancellationToken cancellationToken = default)
    {
        var list =
            new List<CustomerEntity>();

        await foreach (
            var customer
            in Customers.QueryAsync<CustomerEntity>(
                cancellationToken:
                    cancellationToken))
        {
            list.Add(customer);
        }

        return list
            .OrderBy(
                x => x.RowKey)
            .ToList();
    }


    public Task UpsertCustomerAsync(
        CustomerEntity customer,
        CancellationToken cancellationToken = default)
    {
        return Customers.UpsertEntityAsync(
            customer,
            cancellationToken:
                cancellationToken);
    }


    // ============================================================
    // PRODUCTS — AZURE TABLE STORAGE
    // ============================================================

    public async Task<List<ProductEntity>>
        GetProductsAsync(
            CancellationToken cancellationToken = default)
    {
        var list =
            new List<ProductEntity>();

        await foreach (
            var product
            in Products.QueryAsync<ProductEntity>(
                cancellationToken:
                    cancellationToken))
        {
            list.Add(product);
        }

        return list
            .OrderBy(
                x => x.ProductName)
            .ToList();
    }


    public Task UpsertProductAsync(
        ProductEntity product,
        CancellationToken cancellationToken = default)
    {
        return Products.UpsertEntityAsync(
            product,
            cancellationToken:
                cancellationToken);
    }


    // ============================================================
    // BLOBS — AZURE BLOB STORAGE
    // ============================================================

    public async Task<List<BlobItemViewModel>>
        GetBlobsAsync(
            CancellationToken cancellationToken = default)
    {
        var list =
            new List<BlobItemViewModel>();

        await foreach (
            var blob
            in _blobContainer.GetBlobsAsync(
                cancellationToken:
                    cancellationToken))
        {
            list.Add(
                new BlobItemViewModel
                {
                    Name =
                        blob.Name,

                    ContentType =
                        GetContentType(
                            blob.Name),

                    Size =
                        blob.Properties
                            .ContentLength
                            ?? 0
                });
        }

        return list
            .OrderBy(
                x => x.Name)
            .ToList();
    }


    public async Task UploadBlobAsync(
        string fileName,
        Stream stream,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var safeName =
            SanitizeFileName(
                fileName);

        var blob =
            _blobContainer.GetBlobClient(
                safeName);

        await blob.UploadAsync(
            stream,
            overwrite: true,
            cancellationToken:
                cancellationToken);

        await blob.SetHttpHeadersAsync(
            new BlobHttpHeaders
            {
                ContentType =
                    contentType
            },
            cancellationToken:
                cancellationToken);
    }


    public async Task<(
        Stream Stream,
        string ContentType)>
        DownloadBlobAsync(
            string name,
            CancellationToken cancellationToken = default)
    {
        var safeName =
            SanitizeFileName(
                name);

        var blob =
            _blobContainer.GetBlobClient(
                safeName);

        var response =
            await blob.DownloadStreamingAsync(
                cancellationToken:
                    cancellationToken);

        var contentType =
            response.Value.Details.ContentType;

        if (string.IsNullOrWhiteSpace(
            contentType))
        {
            contentType =
                GetContentType(
                    safeName);
        }

        return
        (
            response.Value.Content,
            contentType
        );
    }


    // ============================================================
    // QUEUES — AZURE QUEUE STORAGE
    // ============================================================

    public async Task<(
        List<string> Messages,
        long ApproximateCount)>
        GetQueueDataAsync(
            CancellationToken cancellationToken = default)
    {
        var properties =
            await _queue.GetPropertiesAsync(
                cancellationToken);

        var peeked =
            await _queue.PeekMessagesAsync(
                32,
                cancellationToken);

        return
        (
            peeked.Value
                .Select(
                    x => x.MessageText)
                .ToList(),

            properties.Value
                .ApproximateMessagesCount
        );
    }


    public Task SendQueueMessageAsync(
        string type,
        string message,
        CancellationToken cancellationToken = default)
    {
        var payload =
            JsonSerializer.Serialize(
                new
                {
                    Type =
                        type,

                    Message =
                        message,

                    CreatedAt =
                        DateTime.UtcNow
                });

        return _queue.SendMessageAsync(
            payload,
            cancellationToken);
    }


    // ============================================================
    // AZURE FILES
    // ============================================================

    public async Task<List<LogFileViewModel>>
        GetLogFilesAsync(
            CancellationToken cancellationToken = default)
    {
        var root =
            _share.GetRootDirectoryClient();

        var list =
            new List<LogFileViewModel>();

        await foreach (
            var item
            in root.GetFilesAndDirectoriesAsync(
                cancellationToken:
                    cancellationToken))
        {
            if (item.IsDirectory)
            {
                continue;
            }

            var file =
                root.GetFileClient(
                    item.Name);

            var properties =
                await file.GetPropertiesAsync(
                    cancellationToken:
                        cancellationToken);

            list.Add(
                new LogFileViewModel
                {
                    FileName =
                        item.Name,

                    Size =
                        properties.Value
                            .ContentLength,

                    LastModified =
                        properties.Value
                            .LastModified
                });
        }

        return list
            .OrderByDescending(
                x => x.LastModified)
            .ToList();
    }


    // ============================================================
    // UPLOAD LOG FILE
    // ============================================================

    public async Task UploadLogAsync(
        string fileName,
        string content,
        CancellationToken cancellationToken = default)
    {
        var root =
            _share.GetRootDirectoryClient();

        var safeName =
            SanitizeFileName(
                fileName);

        var file =
            root.GetFileClient(
                safeName);

        var bytes =
            Encoding.UTF8.GetBytes(
                content);

        await file.DeleteIfExistsAsync(
            cancellationToken:
                cancellationToken);

        await file.CreateAsync(
            bytes.LongLength,
            cancellationToken:
                cancellationToken);

        await using var stream =
            new MemoryStream(
                bytes);

        await file.UploadAsync(
            stream,
            cancellationToken:
                cancellationToken);
    }


    // ============================================================
    // DOWNLOAD LOG FILE
    // ============================================================

    public async Task<Stream>
        DownloadLogAsync(
            string fileName,
            CancellationToken cancellationToken = default)
    {
        var root =
            _share.GetRootDirectoryClient();

        var safeName =
            SanitizeFileName(
                fileName);

        var file =
            root.GetFileClient(
                safeName);

        var response =
            await file.DownloadAsync(
                cancellationToken:
                    cancellationToken);

        return response.Value.Content;
    }


    // ============================================================
    // DEMO DATA
    // ============================================================

    public async Task SeedDemoDataAsync(
        CancellationToken cancellationToken = default)
    {
        await EnsureResourcesAsync(
            cancellationToken);


        // ========================================================
        // FIVE CUSTOMERS
        // ========================================================

        var customers =
            new[]
            {
                (
                    CustomerId: "CUST001",
                    FirstName: "Emma",
                    LastName: "Williams",
                    Email: "emma.williams@gmail.com",
                    Phone: "0712345689",
                    City: "Cape Town"
                ),

                (
                    CustomerId: "CUST002",
                    FirstName: "Liam",
                    LastName: "Johnson",
                    Email: "liam.johnson@gmail.com",
                    Phone: "0723456891",
                    City: "Pretoria"
                ),

                (
                    CustomerId: "CUST003",
                    FirstName: "Sophia",
                    LastName: "Brown",
                    Email: "sophia.brown@gmail.com",
                    Phone: "0731245689",
                    City: "Durban"
                ),

                (
                    CustomerId: "CUST004",
                    FirstName: "Daniel",
                    LastName: "Miller",
                    Email: "daniel.miller@gmail.com",
                    Phone: "0745689123",
                    City: "Kimberley"
                ),

                (
                    CustomerId: "CUST005",
                    FirstName: "Olivia",
                    LastName: "Davis",
                    Email: "olivia.davis@gmail.com",
                    Phone: "0756489123",
                    City: "Bloemfontein"
                )
            };


        foreach (var customer in customers)
        {
            await UpsertCustomerAsync(
                new CustomerEntity
                {
                    PartitionKey =
                        "Customers",

                    RowKey =
                        customer.CustomerId,

                    FirstName =
                        customer.FirstName,

                    LastName =
                        customer.LastName,

                    Email =
                        customer.Email,

                    Phone =
                        customer.Phone,

                    City =
                        customer.City,

                    CreatedAt =
                        DateTime.UtcNow
                },
                cancellationToken);
        }


        // ========================================================
        // FIVE PRODUCTS
        // ========================================================

        var products =
            new[]
            {
                (
                    ProductId: "PROD001",
                    ProductName: "Laptop",
                    Price: 12999.99,
                    StockQuantity: 10
                ),

                (
                    ProductId: "PROD002",
                    ProductName: "Mouse",
                    Price: 299.99,
                    StockQuantity: 25
                ),

                (
                    ProductId: "PROD003",
                    ProductName: "Keyboard",
                    Price: 149.99,
                    StockQuantity: 20
                ),

                (
                    ProductId: "PROD004",
                    ProductName: "Monitor",
                    Price: 3499.00,
                    StockQuantity: 15
                ),

                (
                    ProductId: "PROD005",
                    ProductName: "Headphones",
                    Price: 1000.00,
                    StockQuantity: 30
                )
            };


        foreach (var product in products)
        {
            await UpsertProductAsync(
                new ProductEntity
                {
                    PartitionKey =
                        "Products",

                    RowKey =
                        product.ProductId,


                    ProductName =
                        product.ProductName,

                    Price =
                        product.Price,

                    StockQuantity =
                        product.StockQuantity,

                },
                cancellationToken);
        }


        // ========================================================
        // FIVE BLOBS
        // ========================================================

        for (var i = 1; i <= 5; i++)
        {
            var svg =
                $"""
                <svg xmlns="http://www.w3.org/2000/svg"
                     width="800"
                     height="450"
                     viewBox="0 0 800 450">

                    <rect
                        width="800"
                        height="450"
                        fill="#f4f4f5"/>

                    <text
                        x="400"
                        y="205"
                        text-anchor="middle"
                        font-family="Arial"
                        font-size="42"
                        fill="#18181b">

                        Product Media {i}

                    </text>

                    <text
                        x="400"
                        y="260"
                        text-anchor="middle"
                        font-family="Arial"
                        font-size="22"
                        fill="#52525b">

                        Azure Blob Storage

                    </text>

                </svg>
                """;


            await using var stream =
                new MemoryStream(
                    Encoding.UTF8.GetBytes(
                        svg));


            await UploadBlobAsync(
                $"product-{i}.svg",
                stream,
                "image/svg+xml",
                cancellationToken);
        }


        // ========================================================
        // FIVE QUEUE MESSAGES
        // ========================================================

        var queueData =
            await GetQueueDataAsync(
                cancellationToken);

        var currentCount =
            Math.Min(
                queueData.ApproximateCount,
                5);

        for (
            var i =
                (int)currentCount + 1;

            i <= 5;

            i++)
        {
            await SendQueueMessageAsync(
                i % 2 == 0
                    ? "Inventory"
                    : "Order",

                $"Demo transaction {i}: " +
                $"processing order and inventory update.",

                cancellationToken);
        }


        // ========================================================
        // FIVE LOG FILES
        // ========================================================

        for (var i = 1; i <= 5; i++)
        {
            await UploadLogAsync(
                $"application-log-{i}.log",

                $"Azure Storage Demo Log {i}" +
                Environment.NewLine +

                $"Timestamp: {DateTime.UtcNow:O}" +
                Environment.NewLine +

                $"Status: Completed" +
                Environment.NewLine +

                $"Operation: Demo storage transaction" +
                Environment.NewLine,

                cancellationToken);
        }
    }


    // ============================================================
    // HELPERS
    // ============================================================

    public static string SanitizeFileName(
        string fileName)
    {
        var safeName =
            Path.GetFileName(
                fileName);

        foreach (
            var invalid
            in Path.GetInvalidFileNameChars())
        {
            safeName =
                safeName.Replace(
                    invalid,
                    '-');
        }

        return string.IsNullOrWhiteSpace(
            safeName)

            ? $"file-{Guid.NewGuid():N}"

            : safeName
                .Replace(
                    " ",
                    "-")
                .ToLowerInvariant();
    }


    public static string GetContentType(
        string fileName)
    {
        return Path
            .GetExtension(
                fileName)
            .ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" =>
                "image/jpeg",

            ".png" =>
                "image/png",

            ".gif" =>
                "image/gif",

            ".webp" =>
                "image/webp",

            ".svg" =>
                "image/svg+xml",

            ".mp4" =>
                "video/mp4",

            ".webm" =>
                "video/webm",

            ".mov" =>
                "video/quicktime",

            ".mp3" =>
                "audio/mpeg",

            ".wav" =>
                "audio/wav",

            ".m4a" =>
                "audio/mp4",

            ".txt" or ".log" =>
                "text/plain",

            _ =>
                "application/octet-stream"
        };
    }
}