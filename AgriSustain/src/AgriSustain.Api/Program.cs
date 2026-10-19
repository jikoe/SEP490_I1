using AgriSustain.Api;
using AgriSustain.Api.Middlewares;
using AgriSustain.Api.Security.Authentication;
using AgriSustain.Application.Interfaces.Identity;
using AgriSustain.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment.IsDevelopment());
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddAuthentication("Demo")
    .AddScheme<AuthenticationSchemeOptions, DemoAuthenticationHandler>("Demo", _ => { });
builder.Services.AddAuthorization(options =>
{
    // DEMO-PERMISSIONS: only SystemAdmin is mapped here. CustomerSupport and other staff
    // permissions are not approved yet; do not grant them every Administration action.
    options.AddPolicy("Administration", policy => policy.RequireRole("SystemAdmin"));
    options.AddPolicy("RetailCustomerCheckout", policy => policy.RequireRole("RetailCustomer"));
});
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler();
// Keep framework-generated authentication/authorization failures in the same sanitized
// ProblemDetails shape as use-case failures, with a trace ID for the team to diagnose.
app.UseStatusCodePages(async statusContext =>
{
    var context = statusContext.HttpContext;
    await context.Response.WriteAsJsonAsync(new Microsoft.AspNetCore.Mvc.ProblemDetails
    {
        Status = context.Response.StatusCode,
        Title = context.Response.StatusCode == 401 ? "Phiên không hợp lệ hoặc đã bị thu hồi."
            : "Không thể truy cập tài nguyên hoặc thực hiện thao tác.",
        Extensions = { ["code"] = $"HTTP_{context.Response.StatusCode}",
            ["traceId"] = context.TraceIdentifier }
    });
});
string frontendPath = Path.GetFullPath(Path.Combine(
    app.Environment.ContentRootPath, "../../frontend/dist"));
if (Directory.Exists(frontendPath))
{
    // Optional local build output. Run frontend/npm build once to use only the API host.
    var frontendFiles = new PhysicalFileProvider(frontendPath);
    app.Lifetime.ApplicationStopped.Register(frontendFiles.Dispose);
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = frontendFiles });
    app.UseStaticFiles(new StaticFileOptions { FileProvider = frontendFiles });
}
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
// Liveness/configuration only: not a SQL connectivity or external-provider readiness assertion.
app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy", service = "AgriSustain.Api",
    persistence = app.Configuration["Persistence:Mode"] ?? "InMemory"
}));

app.Run();

public partial class Program;
