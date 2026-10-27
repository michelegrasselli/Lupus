using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lupus.Core;

public sealed class SalvataggioPartita
{
    public int Versione { get; set; } = 1;
    public DateTime SalvataIl { get; set; } = DateTime.Now;
    public Partita? Partita { get; set; }
}

public static class Serializzazione
{
    public static readonly JsonSerializerOptions Opzioni = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string Serializza(Partita partita)
    {
        partita.SalvataIl = DateTime.Now;
        var salvataggio = new SalvataggioPartita
        {
            Versione = 1,
            SalvataIl = partita.SalvataIl.Value,
            Partita = partita
        };

        return JsonSerializer.Serialize(salvataggio, Opzioni);
    }

    public static Partita? Deserializza(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            var salvataggio = JsonSerializer.Deserialize<SalvataggioPartita>(json, Opzioni);
            var partita = salvataggio?.Partita;
            partita?.Normalizza();
            return partita;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }
}
