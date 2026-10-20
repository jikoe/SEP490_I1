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


[ApiController, Route("api/seller-orders"), Authorize]
[RequestSizeLimit(72 * 1024 * 1024)]
public sealed class FarmerOrdersController(OrderFulfillmentService service) : ControllerBase
{
    [HttpGet, Authorize(Roles = "Farmer")] public async Task<IActionResult> List([FromQuery] FarmerRetailQuery q, CancellationToken ct) => ApiResults.From(await service.GetListAsync(q, false, ct), HttpContext);
    [HttpGet("mine"), Authorize(Roles = "RetailCustomer")] public async Task<IActionResult> Mine([FromQuery] FarmerRetailQuery q, CancellationToken ct) => ApiResults.From(await service.GetListAsync(q, true, ct), HttpContext);
    [HttpGet("{id:long}"), Authorize(Roles = "Farmer,RetailCustomer")] public async Task<IActionResult> Get(long id, CancellationToken ct) => ApiResults.From(await service.GetAsync(id, User.IsInRole("RetailCustomer"), ct), HttpContext);
    [HttpPost("{id:long}/confirm"), Authorize(Roles = "Farmer")] public async Task<IActionResult> Confirm(long id, CancellationToken ct) => ApiResults.From(await service.ConfirmAsync(id, ct), HttpContext);
    [HttpPost("{id:long}/ready"), Authorize(Roles = "Farmer")] public async Task<IActionResult> Ready(long id, CancellationToken ct) => ApiResults.From(await service.MarkReadyAsync(id, ct), HttpContext);
    [HttpPost("{id:long}/dispatch"), Authorize(Roles = "Farmer")] public async Task<IActionResult> Dispatch(long id, CancellationToken ct) => ApiResults.From(await service.DispatchAsync(id, ct), HttpContext);
    [HttpPost("{id:long}/reject"), Authorize(Roles = "Farmer")] public async Task<IActionResult> Reject(long id, OrderAction input, CancellationToken ct) => ApiResults.From(await service.RejectAsync(id, input, ct), HttpContext);
    [HttpPost("{id:long}/receive"), Authorize(Roles = "RetailCustomer")] public async Task<IActionResult> Receive(long id, CancellationToken ct) => ApiResults.From(await service.ReceiveAsync(id, false, ct), HttpContext);
    [HttpPost("{id:long}/accept"), Authorize(Roles = "RetailCustomer")] public async Task<IActionResult> Accept(long id, CancellationToken ct) => ApiResults.From(await service.ReceiveAsync(id, true, ct), HttpContext);
    [HttpPost("{id:long}/delivery-issue"), Authorize(Roles = "Farmer")] public async Task<IActionResult> Issue(long id, EvidenceInput input, CancellationToken ct) => ApiResults.From(await service.EvidenceAsync(id, input, true, ct), HttpContext);
    [HttpPost("{id:long}/evidence"), Authorize(Roles = "Farmer")] public async Task<IActionResult> Evidence(long id, EvidenceInput input, CancellationToken ct) => ApiResults.From(await service.EvidenceAsync(id, input, false, ct), HttpContext);
}
