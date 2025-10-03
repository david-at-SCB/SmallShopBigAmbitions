global using LanguageExt;
global using LanguageExt.Common;
global using LanguageExt.Pipes;
global using static LanguageExt.Prelude;
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
using SmallShopBigAmbitions.Application.Carts.AddItemToCart;
using SmallShopBigAmbitions.Application.Carts.GetCartForUser; // added for dummy users
using SmallShopBigAmbitions.Application.HelloWorld;
using SmallShopBigAmbitions.Application.Orders;
using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.Business.Services;
using SmallShopBigAmbitions.Database;
using SmallShopBigAmbitions.Database.Commands;
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
// Added for on-prem AD (Negotiate) + claims transformation
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Authentication;
using SmallShopBigAmbitions.Application.Billing.Payments.CreatePaymentIntent.PaymentProviders;
using OpenTelemetry.Exporter; // IClaimsTransformation & SignIn

var builder = WebApplication.CreateBuilder(args);

// Register dummy user store
builder.Services.AddSingleton<IDummyUserStore, InMemoryDummyUserStore>();

// Base authentication (cookie for dummy / impersonation) remains default for dev/testing.
// We ALSO register Negotiate so pages can opt-in: [Authorize(AuthenticationSchemes = NegotiateDefaults.AuthenticationScheme)]
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = "DummyAuth";            // keep dummy as default so existing flows unchanged
    options.DefaultChallengeScheme = "DummyAuth";
})
.AddCookie("DummyAuth", opts =>
{
    opts.LoginPath = "/Auth/Impersonate";
    opts.AccessDeniedPath = "/Auth/Impersonate";
})
.AddNegotiate(); // on-prem AD (Kerberos/NTLM) integration

// Register claims transformer that will map AD group memberships to claims/roles
builder.Services.AddScoped<IClaimsTransformation, AdGroupClaimsTransformer>();

// Align the resource service.name with the site-wide ActivitySource name so spans & resource match
var serviceName = Telemetry.SiteWideActivitySourceName;

// ----- Connection String (config-driven + normalization)
string rawCs = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Missing ConnectionStrings:Default in configuration.");

var dbConnectionString = ConnectionStringBuilder.NormalizeSqlite(rawCs, builder.Environment.ContentRootPath);

// Make available if needed elsewhere
builder.Services.AddSingleton(new DatabaseConfig { ConnectionString = dbConnectionString });

////// ++++++ Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
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
builder.Services.AddSingleton(Telemetry.SiteWideServiceSource); // optional injection
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(serviceName, serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString())
        .AddAttributes(new KeyValuePair<string, object>[]
        {
            new("deployment.environment", builder.Environment.EnvironmentName),
            new("service.instance.id", Environment.MachineName)
        }))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddSource(
            Telemetry.BillingServiceSource.Name,
            Telemetry.CartServiceSource.Name,
            Telemetry.MediatorServiceSource.Name,
            Telemetry.OrderServiceSource.Name,
            Telemetry.SiteWideActivitySourceName)
        .SetSampler(new AlwaysOnSampler())
        //.AddConsoleExporter() // TEMP: verify spans produced before relying solely on OTLP
        .AddOtlpExporter(o =>
        {
            o.Endpoint = new Uri("http://localhost:4317"); // gRPC (Tempo)
            o.Protocol = OtlpExportProtocol.Grpc; // default
        })
        .AddOtlpExporter(o =>
        {
            o.Endpoint = new Uri("http://localhost:4318");
            o.Protocol = OtlpExportProtocol.HttpProtobuf;
        }))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddProcessInstrumentation()
        .AddRuntimeInstrumentation()
        .AddMeter("System.Net Http")
        .AddMeter("System.Net.NameResolution")
        .AddOtlpExporter(o => o.Endpoint = new Uri("http://localhost:4317")));
////// ------ OPEN TELEMETRY

// ----- Services
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IJwtValidator, JwtValidator>();
builder.Services.AddHttpClient<FunctionalHttpClient>(c =>
{
    c.BaseAddress = new Uri("https://fakestoreapi.com/");
});
builder.Services.AddSingleton<IEventPublisher, NoopEventPublisher>();

builder.Services.AddTransient<TraceableIOLoggerExample>();

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
    builder.Services.AddAuthentication() // extend existing builder
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

////++++++++++++++Functional Handlers + Policies + Pipeline Behaviors
builder.Services.AddScoped<IAuthorizationPolicy<IntentToPayCommand>, CreateIntentToPayPolicy>();
builder.Services.AddScoped<CreateIntentToPayPolicy>();
builder.Services.AddFunctionalHandlerWithPolicy<AddCartLineCommand, CartSnapshot, AddCartLineHandler, CartMutationPolicy>();
builder.Services.AddFunctionalHandlerWithPolicy<AddItemToCartCommand, AddItemToCartResult, AddItemToCartHandler, AddItemToCartPolicy>();
builder.Services.AddFunctionalHandlerWithPolicy<ApplyCreditCommand, Unit, ApplyCreditHandler, ApplyCreditPolicy>();
builder.Services.AddFunctionalHandlerWithPolicy<AuthorizePaymentCommand, IntentToPayDto, AuthorizePaymentHandler, AuthorizePaymentPolicy>();
builder.Services.AddFunctionalHandlerWithPolicy<CapturePaymentCommand, Unit, CapturePaymentHandler, CapturePaymentPolicy>();
builder.Services.AddFunctionalHandlerWithPolicy<ChargeCustomerCommand, ChargeResult, ChargeCustomerHandler, ChargeCustomerPolicy>();
builder.Services.AddFunctionalHandlerWithPolicy<ClearCartCommand, CartSnapshot, ClearCartHandler, CartMutationPolicy>();
builder.Services.AddFunctionalHandlerWithPolicy<CreateOrderCommand, OrderSnapshot, CreateOrderHandler, CreateOrderPolicy>();
builder.Services.AddFunctionalHandlerWithPolicy<GetCartForUserQuery, Cart, GetCartForUserHandler, GetCartForUserPolicy>();
builder.Services.AddFunctionalHandlerWithPolicy<HelloWorldRequest, string, HelloWorldHandler, HelloWorldPolicy>();
builder.Services.AddFunctionalHandlerWithPolicy<IntentToPayCommand, IntentToPayDto, CreateIntentToPayHandler, CreateIntentToPayPolicy>();
builder.Services.AddFunctionalHandlerWithPolicy<RefundPaymentCommand, Unit, RefundPaymentHandler, RefundPaymentPolicy>();
builder.Services.AddFunctionalHandlerWithPolicy<RemoveCartLineCommand, CartSnapshot, RemoveCartLineHandler, CartMutationPolicy>();
builder.Services.AddFunctionalHandlerWithPolicy<SetCartLineQuantityCommand, CartSnapshot, SetCartLineQuantityHandler, CartMutationPolicy>();
builder.Services.AddScoped(typeof(IAuthorizationPolicy<>), typeof(AdminOnlyPolicy<>));
builder.Services.AddScoped(typeof(IFunctionalPipelineBehavior<,>), typeof(AuthorizationBehavior<,>));
builder.Services.AddScoped(typeof(IFunctionalPipelineBehavior<,>), typeof(ObservabilityBehavior<,>));
builder.Services.AddScoped(typeof(IFunctionalPipelineBehavior<,>), typeof(CancellationGuardBehaviour<,>));
builder.Services.AddScoped(typeof(IFunctionalPipelineBehavior<,>), typeof(IdempotencyBehavior<,>));
// ----------------

// Misc services
builder.Services.AddScoped<IPaymentProvider, StripePaymentProvider>(); // Register StripePaymentProvider
builder.Services.AddScoped<IPaymentProviderSelector, PaymentProviderSelector>();
builder.Services.AddScoped<IPaymentRefundService, PaymentRefundService>();
builder.Services.AddScoped<ICreditService, CreditService>();
builder.Services.AddScoped<IPaymentCaptureService, PaymentCaptureService>();
builder.Services.AddScoped<ICartPersistence, CartPersistenceImplementation>();
builder.Services.AddScoped<ICartQueries, InMemoryCartQueries>();
builder.Services.AddScoped<IOrderRepository, InMemoryOrderRepository>();
builder.Services.AddScoped<IPricingService, BasicPricingService>();
builder.Services.AddScoped<IInventoryService, NoopInventoryService>();
builder.Services.AddScoped<ProductService>();
builder.Services.AddScoped<BillingService>();
builder.Services.AddScoped<CartService>();
builder.Services.AddScoped<UserService>();

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

// Simple diagnostic to confirm ActivitySources are subscribed
app.Lifetime.ApplicationStarted.Register(() =>
{
    Log.Information("[OTEL-DIAG] SiteWideActivitySource.HasListeners={HasListeners}", Telemetry.SiteWideServiceSource.HasListeners);
});

// Seeding
using (var scope = app.Services.CreateScope())
{
    SQLitePCL.Batteries.Init();
    var seeder = scope.ServiceProvider.GetRequiredService<FakeStoreSeeder>();
    var msLogger = scope.ServiceProvider.GetRequiredService<ILogger<FakeStoreSeeder>>();
    try
    {
        var trace = seeder.Seed(dbConnectionString)
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