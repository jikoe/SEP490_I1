using AgriSustain.Application.Interfaces.UnitOfWork;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Shared;
using AgriSustain.Application.DTOs.Farmer;
using AgriSustain.Application.DTOs.RetailCustomer;
using AgriSustain.Application.Interfaces.Identity;
using AgriSustain.Application.Interfaces.Repositories;
using AgriSustain.Application.Interfaces.Services;
using AgriSustain.Application.Services.Shared;
using AgriSustain.Application.Services.Farmer;
using AgriSustain.Application.Services.RetailCustomer;
using AgriSustain.Infrastructure.Data.Contexts;
using AgriSustain.Infrastructure.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Api.Controllers.Farmer;


public sealed record VisibilityInput(bool Published, long Revision, bool Confirmed);
[ApiController, Route("api/farmer/products"), Authorize(Roles = "Farmer")]
[RequestSizeLimit(72 * 1024 * 1024)]
public sealed class FarmerProductsController(ProductService service, IFarmerRetailUnitOfWork unit, ICurrentUser user, IAddressCatalog addresses) : ControllerBase
{
    [HttpGet("provinces")] public IActionResult Provinces() => Ok(addresses.GetProvinces());
    [HttpGet] public async Task<IActionResult> List([FromQuery] FarmerRetailQuery q, CancellationToken ct) => ApiResults.From(await service.GetListAsync(q, ct), HttpContext);
    [HttpGet("categories")] public async Task<IActionResult> Categories(CancellationToken ct) => ApiResults.From(await unit.RunAsync(async r => {
        var actor = await FarmerRetailRules.Actor(r, user, "Farmer");
        return actor.IsSuccess ? Result<CategoryOption[]>.Success(await r.CategoriesAsync()) : Result<CategoryOption[]>.Failure(actor.Error!);
    }, ct), HttpContext);
    [HttpGet("{id:long}")] public async Task<IActionResult> Get(long id, CancellationToken ct) => ApiResults.From(await service.GetAsync(id, ct), HttpContext);
    [HttpPost] public async Task<IActionResult> Submit(ProductInput input, CancellationToken ct) => ApiResults.From(await service.SubmitAsync(input, ct), HttpContext);
    [HttpPut("{id:long}")] public async Task<IActionResult> Update(long id, ProductInput input, CancellationToken ct) => ApiResults.From(await service.UpdateAsync(id, input, ct), HttpContext);
    [HttpPost("{id:long}/visibility")] public async Task<IActionResult> Visibility(long id, VisibilityInput input, CancellationToken ct) => ApiResults.From(await service.SetVisibilityAsync(id, input.Published, input.Revision, input.Confirmed, ct), HttpContext);
}
