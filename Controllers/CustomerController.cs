using AzureStorageWebApp.Models;
using AzureStorageWebApp.Services;
using Microsoft.AspNetCore.Mvc;

namespace AzureStorageWebApp.Controllers;

public class CustomersController : Controller
{
    private readonly AzureStorageService _storage;


    public CustomersController(
        AzureStorageService storage)
    {
        _storage = storage;
    }


    public async Task<IActionResult> Index(
        CancellationToken cancellationToken)
    {
        await _storage.EnsureResourcesAsync(
            cancellationToken);


        return View(
            await _storage.GetCustomersAsync(
                cancellationToken));
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        CustomerEntity model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await _storage.EnsureResourcesAsync(
                cancellationToken);


            return View(
                "Index",
                await _storage.GetCustomersAsync(
                    cancellationToken));
        }


        model.PartitionKey =
            "Customers";


        model.RowKey =
            string.IsNullOrWhiteSpace(
                model.RowKey)

            ? Guid.NewGuid()
                .ToString("N")

            : model.RowKey;


        model.CreatedAt =
            DateTime.UtcNow;


        await _storage.UpsertCustomerAsync(
            model,
            cancellationToken);


        TempData["Success"] =
            "Customer saved to Azure Table Storage.";


        return RedirectToAction(
            nameof(Index));
    }
}