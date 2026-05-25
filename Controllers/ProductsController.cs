using Microsoft.AspNetCore.Mvc;
using UserManagementAPI.Models;

namespace UserManagementAPI.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsController : ControllerBase
{
    private static readonly List<Product> Products = new();
    private static int nextId = 1;

    [HttpGet]
    public IActionResult GetProducts()
    {
        return Ok(Products);
    }

    [HttpGet("{id}")]
    public IActionResult GetProductById(int id)
    {
        var product = Products.FirstOrDefault(p => p.Id == id);

        if (product == null)
        {
            return NotFound("Product not found");
        }

        return Ok(product);
    }

    [HttpPost]
    public IActionResult CreateProduct([FromBody] Product product)
    {
        product.Id = nextId++;
        Products.Add(product);

        return Created($"/api/products/{product.Id}", product);
    }

    [HttpPut("{id}")]
    public IActionResult UpdateProduct(int id, [FromBody] Product updatedProduct)
    {
        var product = Products.FirstOrDefault(p => p.Id == id);

        if (product == null)
        {
            return NotFound("Product not found");
        }

        product.Name = updatedProduct.Name;
        product.Description = updatedProduct.Description;
        product.Price = updatedProduct.Price;

        return Ok(product);
    }

    [HttpDelete("{id}")]
    public IActionResult DeleteProduct(int id)
    {
        var product = Products.FirstOrDefault(p => p.Id == id);

        if (product == null)
        {
            return NotFound("Product not found");
        }

        Products.Remove(product);

        return NoContent();
    }
}