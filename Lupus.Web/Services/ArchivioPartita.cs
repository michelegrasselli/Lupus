using Lupus.Core;
using Microsoft.JSInterop;

namespace Lupus.Web.Services;

/// <summary>
/// Salvataggio nel localStorage del browser + export/import JSON.
/// I dati non lasciano mai il dispositivo.
/// </summary>
public sealed class ArchivioPartita
{
    public const string Chiave = "lupus.partita.v1";
    private readonly IJSRuntime _js;

    public ArchivioPartita(IJSRuntime js)
    {
        _js = js;
    }

    public async Task<bool> SalvaAsync(Partita partita)
    {
        var json = Serializzazione.Serializza(partita);
        return await _js.InvokeAsync<bool>("lupusStorage.save", Chiave, json);
    }

    public async Task<Partita?> CaricaAsync()
    {
        var json = await _js.InvokeAsync<string?>("lupusStorage.load", Chiave);
        return Serializzazione.Deserializza(json);
    }

    public async Task CancellaAsync()
    {
        await _js.InvokeVoidAsync("lupusStorage.remove", Chiave);
    }

    public async Task ScaricaAsync(Partita partita)
    {
        var json = Serializzazione.Serializza(partita);
        var nome = $"lupus-{partita.CreataIl:yyyyMMdd-HHmm}.json";
        await _js.InvokeVoidAsync("lupusStorage.download", nome, json);
    }

    public async Task SuonaAsync()
    {
        await _js.InvokeVoidAsync("lupusStorage.beep");
    }

    public async Task<bool> ConfermaAsync(string messaggio)
    {
        return await _js.InvokeAsync<bool>("confirm", messaggio);
    }
}
