using Lupus.Web.Components;
using Lupus.Web.Services;
using Microsoft.AspNetCore.Hosting.StaticWebAssets;

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddSingleton<ReteHelper>();
builder.Services.AddScoped<ArchivioPartita>();
builder.Services.AddScoped<ArchivioSessioni>();

var app = builder.Build();

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
