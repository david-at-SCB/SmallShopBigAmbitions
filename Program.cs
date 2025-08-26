global using LanguageExt;
global using LanguageExt.Common;
global using LanguageExt.Pipes;
global using static LanguageExt.Prelude;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using SmallShopBigAmbitions.Application._Behaviours;
using SmallShopBigAmbitions.Application._Policy;
using SmallShopBigAmbitions.Application.Billing.ChargeCustomer;
using SmallShopBigAmbitions.Application.Cart.GetCartForUser;
using SmallShopBigAmbitions.Application.HelloWorld;
using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.Business.Services;
using SmallShopBigAmbitions.FunctionalDispatcher;
using SmallShopBigAmbitions.FunctionalDispatcher.DI;
using SmallShopBigAmbitions.Logic_examples;
using SmallShopBigAmbitions.Models;
using SmallShopBigAmbitions.TracingSources;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

var serviceName = "SmallShopBigAmbitions.Webshop";
builder.Services.AddSingleton(new ActivitySource("SmallShopBigAmbitions"));

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
builder.Services.AddScoped<BillingService>();
builder.Services.AddScoped<CartService>();
builder.Services.AddScoped<LoggingService>();
// Use typed HttpClient for ProductService targeting Fake Store API base address
builder.Services.AddHttpClient<ProductService>(client =>
{
    client.BaseAddress = new Uri("https://fakestoreapi.com/");
});
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<OrderService>();
////// ------ SERVICES

////// ++++++ FUNCTIONAL DISPATCHER
/// inject trusted context to all handlers
builder.Services.AddScoped<IFunctionalDispatcher, FunctionalDispatcher>();
builder.Services.AddScoped<TrustedContext>(provider =>
{
    var httpContext = provider.GetRequiredService<IHttpContextAccessor>().HttpContext;
    var token = httpContext?.Request.Headers.Authorization.FirstOrDefault()?.Replace("Bearer ", "");
    var validator = provider.GetRequiredService<IJwtValidator>();
    return validator.Validate(token!).Match(
        Succ: ctx => ctx,
        Fail: _ => new TrustedContext() // fallback
    );
});

builder.Services.AddFunctionalHandlerWithPolicy<ChargeCustomerCommand, ChargeResult, ChargeCustomerHandler, ChargeCustomerPolicy>();
builder.Services.AddFunctionalHandlerWithPolicy<GetCartForUserQuery, CustomerCart, GetCartForUserHandler, GetCartForUserPolicy>();
builder.Services.AddFunctionalHandlerWithPolicy<HelloWorldRequest, string, HelloWorldHandler, HelloWorldPolicy>();
builder.Services.AddScoped(typeof(IAuthorizationPolicy<>), typeof(AdminOnlyPolicy<>));
builder.Services.AddScoped(typeof(IFunctionalPipelineBehavior<,>), typeof(AuthorizationBehavior<,>));
builder.Services.AddScoped(typeof(IFunctionalPipelineBehavior<,>), typeof(LoggingBehavior<,>));
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