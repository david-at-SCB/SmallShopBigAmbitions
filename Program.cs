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
using SmallShopBigAmbitions.Application.Cart;
using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.Business.Services;
using SmallShopBigAmbitions.Logic_examples;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

var serviceName = "SmallShopBigAmbitions.Webshop";

///// ++++++ SERILOG
// Bootstrap Serilog early to capture startup logs
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithSpan()
    .WriteTo.OpenTelemetry(opts =>
    {
        opts.Endpoint = "http://localhost:4317";
        opts.Protocol = Serilog.Sinks.OpenTelemetry.OtlpProtocol.Grpc;
        opts.ResourceAttributes = new Dictionary<string, object>
        {
            ["service.name"] = $"{serviceName}"
        };
    })
    .CreateLogger();
builder.Host.UseSerilog();
////// ------ SERILOG

////// ++++++ OPEN TELEMETRY 
builder.Services.AddOpenTelemetry()
      .ConfigureResource(resource => resource.AddService(serviceName))
      .WithTracing(tracing => tracing
          .AddAspNetCoreInstrumentation()
          .AddOtlpExporter(o => o.Endpoint = new Uri("http://localhost:4317"))) // gRPC
      .WithMetrics(metrics => metrics
          .AddAspNetCoreInstrumentation()
          .AddConsoleExporter());
////// ------ OPEN TELEMETRY 

////// ++++++ SERVICES
builder.Services.AddTransient<TraceableIOLoggerExample>();
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(AuthorizationBehavior<,>));
builder.Services.AddScoped<CartService>();
////// ------ SERVICES

////// ++++++ MediatR
builder.Services.AddMediatR(cfg =>
{
    //cfg.RegisterServicesFromAssembly(typeof(GetCartForUserHandler).Assembly);
    cfg.RegisterServicesFromAssemblyContaining<ChargeCustomerHandler>();
    cfg.RegisterServicesFromAssemblyContaining<GetCartForUserHandler>();
});
////// ------ MediatR

builder.Services.AddRazorPages();
var app = builder.Build();

// Serilog request logging
app.UseSerilogRequestLogging();

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
