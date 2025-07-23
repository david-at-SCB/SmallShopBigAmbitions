using OpenTelemetry.Extensions.Hosting; // Ensure this namespace is included
using OpenTelemetry.Trace; // Ensure this namespace is included
using OpenTelemetry.Resources; // Ensure this namespace is included
using SmallShopBigAmbitions.Logic_examples; // Ensure this namespace is included

var builder = WebApplication.CreateBuilder(args);

// Add OpenTelemetry tracing
builder.Services.AeddOpenTelemetryTracing(tracerProviderBuilder =>
{
    tracerProviderBuilder
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddSource("MyApp.Tracer") // Replace with your tracer source name
        .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("SmallShopBigAmbitions"))
        .AddConsoleExporter(); // Export traces to the console
});

// Add services to the container.
builder.Services.AddRazorPages();

builder.Services.AddTransient<TraceableIOLoggerExample>();

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
