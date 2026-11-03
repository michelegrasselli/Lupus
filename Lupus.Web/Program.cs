using System.Net;
using Lupus.Web.Components;
using Lupus.Web.Services;
using Microsoft.AspNetCore.Hosting.StaticWebAssets;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

// Cloudflared gira sulla stessa macchina e inoltra a 127.0.0.1:5092.
// Accettiamo X-Forwarded-* solo da un proxy locale: un header falsificato
// arrivato dalla LAN non può cambiare schema, host o IP del client.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
        | ForwardedHeaders.XForwardedProto
        | ForwardedHeaders.XForwardedHost;
    options.ForwardLimit = 1;

    options.KnownProxies.Clear();
    options.KnownProxies.Add(IPAddress.Loopback);
    options.KnownProxies.Add(IPAddress.IPv6Loopback);
    options.KnownIPNetworks.Clear();
});

// L'eseguibile dentro bin/ parte in Production: senza questo loader
// MapStaticAssets non serve né app.css né _framework/blazor.web.js, quindi la
// UI resta senza stile e senza interattività. Su un publish il manifest non
// esiste e la chiamata non ha effetto.
if (!builder.Environment.IsDevelopment())
{
    StaticWebAssetsLoader.UseStaticWebAssets(builder.Environment, builder.Configuration);
}

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSingleton<StanzeService>();
builder.Services.AddSingleton<GiocatoriAi>();
builder.Services.AddHostedService<AiWorker>();
builder.Services.AddSingleton<ReteHelper>();
builder.Services.AddScoped<LinkAccesso>();
builder.Services.AddScoped<ArchivioPartita>();
builder.Services.AddScoped<ArchivioSessioni>();

var app = builder.Build();

// Deve stare prima di tutto il resto: da qui in poi la richiesta vede lo
// schema https e l'host pubblico del tunnel, e NavigationManager.BaseUri
// costruisce i link giusti anche fuori dalla LAN.
app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
