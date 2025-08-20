global using LanguageExt;
global using LanguageExt.Common;
global using LanguageExt.Effects;
global using LanguageExt.Pipes;
global using LanguageExt.Pretty;
global using LanguageExt.Traits;
global using LanguageExt.Traits.Domain;
global using static LanguageExt.Prelude;
using MediatR;
using OpenTelemetry.Extensions.Hosting;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using SmallShopBigAmbitions.Application.Billing;
using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.Logic_examples;

var builder = WebApplication.CreateBuilder(args);


////// OPEN TELEMETRY LOGGING
// Add OpenTelemetry tracing
var serviceName = "SmallShopBigAmbitions.Webshop";
builder.Logging.AddOpenTelemetry(options =>
{
    options
        .SetResourceBuilder(
            ResourceBuilder.CreateDefault()
                .AddService(serviceName))
        .AddConsoleExporter();
});
builder.Services.AddOpenTelemetry()
      .ConfigureResource(resource => resource.AddService(serviceName))
      .WithTracing(tracing => tracing
          .AddAspNetCoreInstrumentation()
          .AddConsoleExporter())
      .WithMetrics(metrics => metrics
          .AddAspNetCoreInstrumentation()
          .AddConsoleExporter());
////// OPEN TELEMETRY LOGGING

////// Serilog configuration
builder.Services.AddLogging(builder =>
{
    builder.AddOpenTelemetry(options =>
    {
        options.IncludeFormattedMessage = true;
        options.IncludeScopes = true;
        options.ParseStateValues = true;
    });
});
////// ---------------------------

// Add services to the container.
builder.Services.AddRazorPages();

builder.Services.AddTransient<TraceableIOLoggerExample>();
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(AuthorizationBehavior<,>));

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
