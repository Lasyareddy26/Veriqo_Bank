using Veriqo.Api.Data;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace Veriqo.Api.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsController : ControllerBase
{
    private readonly MongoDbContext _db;
    public ProductsController(MongoDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var products = await _db.Products.Find(p => p.Active)
            .SortBy(p => p.SortOrder).ToListAsync();
        return Ok(products);
    }

    [HttpGet("{key}")]
    public async Task<IActionResult> Get(string key)
    {
        var product = await _db.Products.Find(p => p.Key == key).FirstOrDefaultAsync();
        return product == null ? NotFound() : Ok(product);
    }
}
