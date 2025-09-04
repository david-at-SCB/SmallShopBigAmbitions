global using LanguageExt;
global using LanguageExt.Common;
global using LanguageExt.Pipes;
global using static LanguageExt.Prelude;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using SmallShopBigAmbitions.Application._PipelineBehaviours;
using SmallShopBigAmbitions.Application._Policy;
using SmallShopBigAmbitions.Application.Billing.ChargeCustomer;
using SmallShopBigAmbitions.Application.Cart.GetCartForUser;
using SmallShopBigAmbitions.Application.HelloWorld;
using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.Business.Services;
using SmallShopBigAmbitions.Database;
using SmallShopBigAmbitions.FunctionalDispatcher;
using SmallShopBigAmbitions.FunctionalDispatcher.DI;
using SmallShopBigAmbitions.HTTP;
using SmallShopBigAmbitions.Logic_examples;
using SmallShopBigAmbitions.Models;
using SmallShopBigAmbitions.Monads.TraceableTransformer;
using SmallShopBigAmbitions.TracingSources;
using System.Diagnostics;
using System.Security.Claims;
using System.Text;
using SmallShopBigAmbitions.Application.Billing.Payments.CreateIntentToPay; // IEventPublisher
using SmallShopBigAmbitions.Application.Billing.Payments.CreatePaymentIntent.Repo;
using SmallShopBigAmbitions.Application.Orders;
using SmallShopBigAmbitions.Application.Billing.Payments;
using SmallShopBigAmbitions.Database.Idempotency;
using SmallShopBigAmbitions.Application._Abstractions;

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
        .AddHttpClientInstrumentation()
        .AddSource(
            Telemetry.BillingSource.Name,
            Telemetry.CartSource.Name,
            Telemetry.MediatorSource.Name,
            Telemetry.OrderSource.Name)
        .AddConsoleExporter() // show traces locally
        .AddOtlpExporter(o => o.Endpoint = new Uri("http://localhost:4317"))) // gRPC
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddProcessInstrumentation()
        .AddRuntimeInstrumentation()
        .AddConsoleExporter()
        .AddMeter("System.Net.Http")
        .AddMeter("System.Net.NameResolution")
        .AddOtlpExporter(o => o.Endpoint = new Uri("http://localhost:4317")));
////// ------ OPEN TELEMETRY

////// ++++++ SERVICES
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IJwtValidator, JwtValidator>();

// Event publishing (no-op default)
builder.Services.AddSingleton<IEventPublisher, NoopEventPublisher>();

builder.Services.AddTransient<TraceableIOLoggerExample>();
builder.Services.AddScoped<BillingService>();
builder.Services.AddScoped<CartService>();
builder.Services.AddScoped<LoggingService>();
// Register FunctionalHttpClient with base address; ProductService depends on FunctionalHttpClient
builder.Services.AddHttpClient<FunctionalHttpClient>(client =>
{
    client.BaseAddress = new Uri("https://fakestoreapi.com/");
});
builder.Services.AddScoped<ProductService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<OrderService>();

// SQLite-backed repositories
// Connection string built below during initialization; register factories so runtime gets the correct string
string connectionString;
string dbPath;

builder.Services.AddScoped<IDataAccess, DataAccess>();
// Register repo/idempotency with a factory so we can capture the connectionString computed after builder.Configuration
builder.Services.AddScoped<IPaymentIntentRepository>(sp =>
{
    // Build connection string identically to how initialization does
    var env = sp.GetRequiredService<IHostEnvironment>();
    var dbDir = Path.Combine(env.ContentRootPath, "App_Data");
    Directory.CreateDirectory(dbDir);
    var path = Path.Combine(dbDir, "shop.db");
    var cs = $"Data Source={path}";
    return new PaymentIntentRepository(cs);
});

builder.Services.AddScoped<IIdempotencyStore>(sp =>
{
    var env = sp.GetRequiredService<IHostEnvironment>();
    var dbDir = Path.Combine(env.ContentRootPath, "App_Data");
    Directory.CreateDirectory(dbDir);
    var path = Path.Combine(dbDir, "shop.db");
    var cs = $"Data Source={path}";
    return new SmallShopBigAmbitions.Database.Idempotency.SqliteIdempotencyStore(cs);
});

// Seeder
builder.Services.AddScoped<FakeStoreSeeder>();
////// ------ SERVICES

////// ++++++ AUTHENTICATION/AUTHORIZATION
var jwtKey = builder.Configuration["Jwt:Key"];
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];

if (!string.IsNullOrWhiteSpace(jwtKey))
{
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                ValidateIssuer = !string.IsNullOrWhiteSpace(jwtIssuer),
                ValidIssuer = jwtIssuer,
                ValidateAudience = !string.IsNullOrWhiteSpace(jwtAudience),
                ValidAudience = jwtAudience,
                RequireExpirationTime = true,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(2),
                NameClaimType = ClaimTypes.NameIdentifier,
                RoleClaimType = ClaimTypes.Role
            };
        });
}

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("AdminOnlyClaim", policy =>
        policy.RequireClaim(ClaimTypes.Role, "Admin"));

////// ++++++ FUNCTIONAL DISPATCHER
/// inject trusted context to all handlers
builder.Services.AddScoped<IFunctionalDispatcher, FunctionalDispatcher>();
builder.Services.AddScoped<TrustedContext>(provider =>
{
    var httpContext = provider.GetRequiredService<IHttpContextAccessor>().HttpContext;
    // Ensure AnonymousId cookie exists for anonymous users
    if (httpContext != null && !httpContext.User.Identity?.IsAuthenticated == true)
    {
        const string CookieName = "anon-id";
        if (!httpContext.Request.Cookies.ContainsKey(CookieName))
        {
            var anonId = Guid.NewGuid().ToString();
            httpContext.Response.Cookies.Append(
                CookieName,
                anonId,
                new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Lax,
                    IsEssential = true,
                    Expires = DateTimeOffset.UtcNow.AddYears(1)
                });
        }
    }
    return TrustedContextFactory.FromHttpContext(httpContext);
});

builder.Services.AddFunctionalHandlerWithPolicy<ChargeCustomerCommand, ChargeResult, ChargeCustomerHandler, ChargeCustomerPolicy>();
builder.Services.AddFunctionalHandlerWithPolicy<GetCartForUserQuery, Cart, GetCartForUserHandler, GetCartForUserPolicy>();
builder.Services.AddFunctionalHandlerWithPolicy<HelloWorldRequest, string, HelloWorldHandler, HelloWorldPolicy>();
builder.Services.AddFunctionalHandlerWithPolicy<CreateOrderCommand, OrderSnapshot, CreateOrderHandler, CreateOrderPolicy>();
builder.Services.AddFunctionalHandlerWithPolicy<AuthorizePaymentCommand, IntentToPayDto, AuthorizePaymentHandler, AuthorizePaymentPolicy>();
builder.Services.AddFunctionalHandlerWithPolicy<CapturePaymentCommand, Unit, CapturePaymentHandler, CapturePaymentPolicy>();
builder.Services.AddFunctionalHandlerWithPolicy<RefundPaymentCommand, Unit, RefundPaymentHandler, RefundPaymentPolicy>();
builder.Services.AddFunctionalHandlerWithPolicy<ApplyCreditCommand, Unit, ApplyCreditHandler, ApplyCreditPolicy>();
builder.Services.AddScoped(typeof(IAuthorizationPolicy<>), typeof(AdminOnlyPolicy<>));
builder.Services.AddScoped(typeof(IFunctionalPipelineBehavior<,>), typeof(AuthorizationBehavior<,>));
builder.Services.AddScoped(typeof(IFunctionalPipelineBehavior<,>), typeof(LoggingBehavior<,>));
builder.Services.AddScoped(typeof(IFunctionalPipelineBehavior<,>), typeof(IdempotencyBehavior<,>));
////// ------ FUNCTIONAL DISPATCHER

builder.Services.AddRazorPages();

// Initialize SQLite database (declarative)
try
{
    var dbDir = Path.Combine(builder.Environment.ContentRootPath, "App_Data");
    Directory.CreateDirectory(dbDir);
    dbPath = Path.Combine(dbDir, "shop.db");
    connectionString = $"Data Source={dbPath}";

    var init = DatabaseInitialize.Initialize(connectionString).Run();
    var _ = init.Match(
        Succ: _ => { Log.Information("Database initialized at {DbPath}", dbPath); return unit; },
        Fail: err => { Log.Warning("Database initialization failed: {Error}", err.Message); return unit; }
    );
}
catch (Exception ex)
{
    Log.Error(ex, "Database initialization threw an exception");
    throw;
}

var app = builder.Build();

// Seed products from FakeStore API (functional/traceable)
using (var scope = app.Services.CreateScope())
{
    SQLitePCL.Batteries.Init();
    var seeder = scope.ServiceProvider.GetRequiredService<FakeStoreSeeder>();
    var msLogger = scope.ServiceProvider.GetRequiredService<ILogger<FakeStoreSeeder>>();
    try
    {
        var trace = seeder.Seed(connectionString)
                          .WithLogging(msLogger)
                          .WithSpanName("SeedProductsFromFakeStore");
        var fin = trace.RunTraceableFin(CancellationToken.None).Run();
        fin.Match(
            Succ: _ => Log.Information("Product seed complete"),
            Fail: err => Log.Warning("Product seed failed: {Error}", err.Message)
        );
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Product seed threw an exception");
    }
}

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
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();