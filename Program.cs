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
using SmallShopBigAmbitions.Application._Abstractions;
using SmallShopBigAmbitions.Application._PipelineBehaviours;
using SmallShopBigAmbitions.Application._Policy;
using SmallShopBigAmbitions.Application.Billing.ChargeCustomer;
using SmallShopBigAmbitions.Application.Billing.Payments;
using SmallShopBigAmbitions.Application.Billing.Payments.CreateIntentToPay; // IEventPublisher
using SmallShopBigAmbitions.Application.Billing.Payments.CreatePaymentIntent;
using SmallShopBigAmbitions.Application.Billing.Payments.CreatePaymentIntent.Repo;
using SmallShopBigAmbitions.Application.Cart.GetCartForUser;
using SmallShopBigAmbitions.Application.HelloWorld;
using SmallShopBigAmbitions.Application.Orders;
using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.Business.Services;
using SmallShopBigAmbitions.Database;
using SmallShopBigAmbitions.Database.Idempotency;
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
using SmallShopBigAmbitions.Database.Commands;

var builder = WebApplication.CreateBuilder(args);

var serviceName = "SmallShopBigAmbitions.Webshop";
builder.Services.AddSingleton(new ActivitySource("SmallShopBigAmbitions"));

// ----- Connection String (config-driven + normalization)
string rawCs = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Missing ConnectionStrings:Default in configuration.");

string NormalizeSqlite(string cs, string contentRoot)
{
    const string prefix = "Data Source=";
    if (!cs.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        return cs; // leave untouched (advanced scenarios)
    var remainder = cs[prefix.Length..].Trim();
    var pathPart = remainder.Split(';')[0].Trim();
    if (!Path.IsPathRooted(pathPart))
    {
        var fullPath = Path.Combine(contentRoot, pathPart);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        return $"{prefix}{fullPath}";
    }
    Directory.CreateDirectory(Path.GetDirectoryName(pathPart)!);
    return cs;
}

var dbConnectionString = NormalizeSqlite(rawCs, builder.Environment.ContentRootPath);

// Make available if needed elsewhere
builder.Services.AddSingleton(new DatabaseConfig { ConnectionString = dbConnectionString });

// ----- Serilog
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
            ["service.name"] = serviceName
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
        .AddConsoleExporter()
        .AddOtlpExporter(o => o.Endpoint = new Uri("http://localhost:4317")))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddProcessInstrumentation()
        .AddRuntimeInstrumentation()
        .AddConsoleExporter()
        .AddMeter("System.Net Http")
        .AddMeter("System.Net.NameResolution")
        .AddOtlpExporter(o => o.Endpoint = new Uri("http://localhost:4317")));

// ----- Services
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IJwtValidator, JwtValidator>();
builder.Services.AddHttpClient<FunctionalHttpClient>(c =>
{
    c.BaseAddress = new Uri("https://fakestoreapi.com/");
});
builder.Services.AddSingleton<IEventPublisher, NoopEventPublisher>();

builder.Services.AddTransient<TraceableIOLoggerExample>();
builder.Services.AddScoped<BillingService>();
builder.Services.AddScoped<CartService>();
builder.Services.AddScoped<LoggingService>();
builder.Services.AddScoped<ProductService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<OrderService>();


// IDataAccess via factory using normalized config-derived connection string
builder.Services.AddScoped<IDataAccess>(_ =>
    new DataAccess(dbConnectionString));

// PaymentIntent repo
builder.Services.AddScoped<IPaymentIntentRepository>(sp =>
{
    var da = sp.GetRequiredService<IDataAccess>();
    return new PaymentIntentRepository(da);
});

builder.Services.AddScoped<IIdempotencyStore>(sp =>
{
    // Reuse same db file for idempotency or separate if desired
    // If you want a separate file later, add another connection string key
    var cs = dbConnectionString;
    return new SmallShopBigAmbitions.Database.Idempotency.SqliteIdempotencyStore(cs);
});

builder.Services.AddScoped<FakeStoreSeeder>();

// AuthN/AuthZ
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

// Functional dispatcher pipeline
builder.Services.AddScoped<IFunctionalDispatcher, FunctionalDispatcher>();
builder.Services.AddScoped<TrustedContext>(provider =>
{
    var httpContext = provider.GetRequiredService<IHttpContextAccessor>().HttpContext;
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


// ++++++++++++++ Functional Handlers + Policies + Pipeline Behaviors
builder.Services.AddFunctionalHandlerWithPolicy<ChargeCustomerCommand, ChargeResult, ChargeCustomerHandler, ChargeCustomerPolicy>();
builder.Services.AddFunctionalHandlerWithPolicy<GetCartForUserQuery, Cart, GetCartForUserHandler, GetCartForUserPolicy>();
builder.Services.AddFunctionalHandlerWithPolicy<HelloWorldRequest, string, HelloWorldHandler, HelloWorldPolicy>();
builder.Services.AddFunctionalHandlerWithPolicy<CreateOrderCommand, OrderSnapshot, CreateOrderHandler, CreateOrderPolicy>();
builder.Services.AddFunctionalHandlerWithPolicy<AuthorizePaymentCommand, IntentToPayDto, AuthorizePaymentHandler, AuthorizePaymentPolicy>();
builder.Services.AddFunctionalHandlerWithPolicy<CapturePaymentCommand, Unit, CapturePaymentHandler, CapturePaymentPolicy>();
builder.Services.AddFunctionalHandlerWithPolicy<RefundPaymentCommand, Unit, RefundPaymentHandler, RefundPaymentPolicy>();
builder.Services.AddFunctionalHandlerWithPolicy<ApplyCreditCommand, Unit, ApplyCreditHandler, ApplyCreditPolicy>();
builder.Services.AddFunctionalHandlerWithPolicy<AddCartLineCommand, CartSnapshot, AddCartLineHandler, CartMutationPolicy>();
builder.Services.AddFunctionalHandlerWithPolicy<SetCartLineQuantityCommand, CartSnapshot, SetCartLineQuantityHandler, CartMutationPolicy>();
builder.Services.AddFunctionalHandlerWithPolicy<RemoveCartLineCommand, CartSnapshot, RemoveCartLineHandler, CartMutationPolicy>();
builder.Services.AddFunctionalHandlerWithPolicy<ClearCartCommand, CartSnapshot, ClearCartHandler, CartMutationPolicy>();
builder.Services.AddScoped(typeof(IAuthorizationPolicy<>), typeof(AdminOnlyPolicy<>));
builder.Services.AddScoped(typeof(IFunctionalPipelineBehavior<,>), typeof(AuthorizationBehavior<,>));
builder.Services.AddScoped(typeof(IFunctionalPipelineBehavior<,>), typeof(LoggingBehavior<,>));
builder.Services.AddScoped(typeof(IFunctionalPipelineBehavior<,>), typeof(IdempotencyBehavior<,>));
// ----------------


// Misc services
builder.Services.AddScoped<IPaymentProviderSelector, PaymentProviderSelector>();
builder.Services.AddScoped<IPaymentRefundService, PaymentRefundService>();
builder.Services.AddScoped<ICreditService, CreditService>();
builder.Services.AddScoped<IPaymentCaptureService, PaymentCaptureService>();
builder.Services.AddScoped<ICartQueries, InMemoryCartQueries>();
builder.Services.AddScoped<IOrderRepository, InMemoryOrderRepository>();
builder.Services.AddScoped<IPricingService, BasicPricingService>();
builder.Services.AddScoped<IInventoryService, NoopInventoryService>();


builder.Services.AddRazorPages();

// Declarative DB initialization (same single source connection string)
try
{
    var init = DatabaseInitialize.Initialize(dbConnectionString).Run();
    var _ = init.Match(
        Succ: _ => { Log.Information("Database initialized at {Conn}", dbConnectionString); return unit; },
        Fail: err => { Log.Warning("Database initialization failed: {Error}", err.Message); return unit; }
    );
}
catch (Exception ex)
{
    Log.Error(ex, "Database initialization threw an exception");
    throw;
}

var app = builder.Build();

// Seeding
using (var scope = app.Services.CreateScope())
{
    SQLitePCL.Batteries.Init();
    var seeder = scope.ServiceProvider.GetRequiredService<FakeStoreSeeder>();
    var msLogger = scope.ServiceProvider.GetRequiredService<ILogger<FakeStoreSeeder>>();
    try
    {
        var trace = seeder.Seed(dbConnectionString)
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

app.UseSerilogRequestLogging();

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