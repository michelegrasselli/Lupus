using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.JSInterop;

namespace Lupus.Web.Services;

public enum TipoSessione
{
    Host,
    Giocatore
}

/// <summary>
/// Ricorda nel browser chi è il narratore e quale posto ha reclamato un
/// giocatore. Il token è il segreto che lega il dispositivo alla stanza.
/// </summary>
public sealed class SessioneBrowser
{
    public string Codice { get; set; } = "";
    public TipoSessione Tipo { get; set; }
    public string Token { get; set; } = "";
    public string? GiocatoreId { get; set; }
    public string? Nome { get; set; }
    public DateTime SalvataIl { get; set; } = DateTime.Now;
}

/// <summary>
/// Elenco delle sessioni salvate nel localStorage. Più stanze possono
/// convivere nello stesso browser (utile per provare host e giocatore).
/// </summary>
public sealed class ArchivioSessioni
{
    public const string Chiave = "lupus.sessioni.v1";

    private static readonly JsonSerializerOptions Opzioni = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly IJSRuntime _js;

    public ArchivioSessioni(IJSRuntime js)
    {
        _js = js;
    }

    public async Task<List<SessioneBrowser>> CaricaAsync()
    {
        try
        {
            var json = await _js.InvokeAsync<string?>("lupusStorage.load", Chiave);
            if (string.IsNullOrWhiteSpace(json)) return new List<SessioneBrowser>();

            return JsonSerializer.Deserialize<List<SessioneBrowser>>(json, Opzioni)
                ?? new List<SessioneBrowser>();
        }
        catch
        {
            return new List<SessioneBrowser>();
        }
    }

    public async Task<SessioneBrowser?> TrovaAsync(string codice, TipoSessione tipo)
    {
        var lista = await CaricaAsync();
        return lista.FirstOrDefault(s =>
            s.Tipo == tipo &&
            string.Equals(s.Codice, codice, StringComparison.OrdinalIgnoreCase));
    }

    public async Task SalvaAsync(SessioneBrowser sessione)
    {
        var lista = await CaricaAsync();
        lista.RemoveAll(s =>
            s.Tipo == sessione.Tipo &&
            string.Equals(s.Codice, sessione.Codice, StringComparison.OrdinalIgnoreCase));

        lista.Add(sessione);
        await ScriviAsync(lista);
    }

    public async Task RimuoviAsync(string codice, TipoSessione tipo)
    {
        var lista = await CaricaAsync();
        lista.RemoveAll(s =>
            s.Tipo == tipo &&
            string.Equals(s.Codice, codice, StringComparison.OrdinalIgnoreCase));

        await ScriviAsync(lista);
    }

    public async Task PulisciAsync()
    {
        await _js.InvokeVoidAsync("lupusStorage.remove", Chiave);
    }

    private async Task ScriviAsync(List<SessioneBrowser> lista)
    {
        if (lista.Count == 0)
        {
            await _js.InvokeVoidAsync("lupusStorage.remove", Chiave);
            return;
        }

        var json = JsonSerializer.Serialize(lista, Opzioni);
        await _js.InvokeAsync<bool>("lupusStorage.save", Chiave, json);
    }
}
