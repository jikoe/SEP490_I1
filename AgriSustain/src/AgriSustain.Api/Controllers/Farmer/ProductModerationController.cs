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


[ApiController, Route("api/moderation/products"), Authorize(Roles = "ContentModerator")]
public sealed class ProductModerationController(ProductService service) : ControllerBase
{
    [HttpPost("{id:long}")]
    public async Task<IActionResult> Moderate(long id, ModerationInput input, CancellationToken ct) => ApiResults.From(await service.ModerateAsync(id, input, ct), HttpContext);
}
