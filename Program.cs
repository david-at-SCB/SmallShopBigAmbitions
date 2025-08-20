global using LanguageExt;
global using LanguageExt.Common;
global using LanguageExt.Pipes;
global using static LanguageExt.Prelude;
using MediatR;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using SmallShopBigAmbitions.Application.Billing;
using SmallShopBigAmbitions.Application.Cart;
using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.Business.Services;
using SmallShopBigAmbitions.FunctionalDispatcher;
using SmallShopBigAmbitions.Logic_examples;
using SmallShopBigAmbitions.TracingSources;

var builder = WebApplication.CreateBuilder(args);

var serviceName = "SmallShopBigAmbitions.Webshop";

///// ++++++ SERILOG
// Bootstrap Serilog early to capture startup logs
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
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
builder.Services.AddLogging();
////// ------ SERILOG

////// ++++++ OPEN TELEMETRY
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(serviceName))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddSource(
            Telemetry.BillingSource.Name,
            Telemetry.CartSource.Name,
            Telemetry.MediatorSource.Name,
            Telemetry.OrderSource.Name)
        .AddConsoleExporter() // show traces locally
        .AddOtlpExporter(o => o.Endpoint = new Uri("http://localhost:4317"))) // gRPC
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddConsoleExporter());
////// ------ OPEN TELEMETRY

////// ++++++ SERVICES
builder.Services.AddTransient<TraceableIOLoggerExample>();
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(TracingBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(AuthorizationBehavior<,>));
builder.Services.AddScoped<BillingService>();
builder.Services.AddScoped<CartService>();
builder.Services.AddScoped<LoggingService>();
builder.Services.AddScoped<ProductService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<OrderService>();
////// ------ SERVICES

////// ++++++ FUNCTIONAL DISPATCHER
builder.Services.Scan(scan => scan //CS1501 Scan doesnt take 1 argument ?
    .FromAssemblyOf<SomeHandler>()
    .AddClasses(classes => classes.AssignableTo(typeof(IFunctionalHandler<,>)))
    .AsImplementedInterfaces()
    .WithScopedLifetime());

builder.Services.Scan(scan => scan //CS1501 Scan doesnt take 1 argument ?
    .FromAssemblyOf<SomeBehavior>()
    .AddClasses(classes => classes.AssignableTo(typeof(IFunctionalPipelineBehavior<,>)))
    .AsImplementedInterfaces()
    .WithScopedLifetime());

builder.Services.AddScoped<FunctionalDispatcher>();

////// ------ FUNCTIONAL DISPATCHER

builder.Services.AddRazorPages();
var app = builder.Build();

// Serilog request logging
app.UseSerilogRequestLogging();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();