using OpenTelemetry.Trace; // Ensure this namespace is included
using OpenTelemetry.Resources; // Ensure this namespace is included

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

//// Fix: Use the correct extension method for OpenTelemetry configuration
//builder.Services.ConfigureOpenTelemetryTracerProvider(tracerProviderBuilder =>
//{
//    tracerProviderBuilder
//        .AddSource("MyApp.Telemetry")
//        .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("MyApp"))
//        .AddConsoleExporter() // For debugging
//        .AddOtlpExporter();   // Sends to OTLP collector (e.g., OpenTelemetry Collector)
//});

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
