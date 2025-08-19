global using LanguageExt;
global using LanguageExt.Common;
global using LanguageExt.Effects;
global using LanguageExt.Pipes;
global using LanguageExt.Pretty;
global using LanguageExt.Traits;
global using LanguageExt.Traits.Domain;
global using static LanguageExt.Prelude;
using OpenTelemetry.Extensions.Hosting;
using OpenTelemetry.Resources;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using SmallShopBigAmbitions.Application.Billing;
using SmallShopBigAmbitions.Logic_examples;

var builder = WebApplication.CreateBuilder(args);

// Add OpenTelemetry tracing
var billingServiceName = "SmallShopBigAmbitions.Billing";
var orderServiceName = "SmallShopBigAmbitions.Order";
var cartServiceName = "SmallShopBigAmbitions.Cart";
builder.Logging.AddOpenTelemetry(options =>
{
    options
        .SetResourceBuilder(
            ResourceBuilder.CreateDefault()
                .AddService(billingServiceName))
        //.AddService(orderServiceName))
        //.AddService(cartServiceName))
        .AddConsoleExporter();
});
builder.Services.AddOpenTelemetry()
      .ConfigureResource(resource => resource.AddService(billingServiceName))
      .WithTracing(tracing => tracing
          .AddAspNetCoreInstrumentation()
          .AddConsoleExporter())
      .WithMetrics(metrics => metrics
          .AddAspNetCoreInstrumentation()
          .AddConsoleExporter());

// Add services to the container.
builder.Services.AddRazorPages();

builder.Services.AddTransient<TraceableIOLoggerExample>();
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssemblyContaining<ChargeCustomerHandler>());


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();
