using AzureStorageWebApp.Models;
using AzureStorageWebApp.Services;
using Microsoft.AspNetCore.Mvc;

namespace AzureStorageWebApp.Controllers;

public class ProductsController : Controller
{
    private readonly AzureStorageService _storage;


    public ProductsController(
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
            await _storage.GetProductsAsync(
                cancellationToken));
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        ProductEntity model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await _storage.EnsureResourcesAsync(
                cancellationToken);


            return View(
                "Index",
                await _storage.GetProductsAsync(
                    cancellationToken));
        }


        model.PartitionKey =
            "Products";


        model.RowKey =
            string.IsNullOrWhiteSpace(
                model.RowKey)

            ? Guid.NewGuid()
                .ToString("N")

            : model.RowKey;


        await _storage.UpsertProductAsync(
            model,
            cancellationToken);


        TempData["Success"] =
            "Product saved to Azure Table Storage.";


        return RedirectToAction(
            nameof(Index));
    }
}