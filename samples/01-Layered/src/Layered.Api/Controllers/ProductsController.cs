using Layered.Business.Dtos;
using Layered.Business.Services;
using Microsoft.AspNetCore.Mvc;

namespace Layered.Api.Controllers;

[ApiController]
[Route("products")]
public class ProductsController(ProductService service) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<ProductDto>> List(CancellationToken ct) => service.ListAsync(ct);
}
