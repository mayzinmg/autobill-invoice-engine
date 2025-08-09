using Invoice.API.Services;
using QuestPDF.Infrastructure;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Invoice.API.Domain.Tax;

QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

// ✅ Explicitly configure Kestrel for HTTP/2 (especially required in containers)
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5000, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http2;
    });
});

builder.Services.AddGrpc(o =>
{
    o.EnableDetailedErrors = true;   // TEMP: show inner exceptions to client
});

builder.Services.AddGrpcReflection(); 
builder.Services.AddSingleton<BlobUploader>();
builder.Services.AddSingleton<ITaxEngine, TaxEngine>();
var app = builder.Build();

app.MapGrpcService<InvoiceGrpcService>();
app.MapGrpcReflectionService(); // ✅ Mapped unconditionally
app.MapGet("/", () => "gRPC service is running.");

app.Run();
