namespace Lupus.Core;

public sealed record PresetPartita(
    string Nome,
    string Descrizione,
    IReadOnlyDictionary<Ruolo, int> Ruoli)
{
    public int Totale => Ruoli.Values.Sum();
}

public static class Preset
{
    public static readonly IReadOnlyList<PresetPartita> Tutti = new List<PresetPartita>
    {
        new("Rapida (6)", "Una partita corta per iniziare.",
            new Dictionary<Ruolo, int>
            {
                [Lupus.Core.Ruolo.Lupo] = 1,
                [Lupus.Core.Ruolo.Veggente] = 1,
                [Lupus.Core.Ruolo.Guardiano] = 1,
                [Lupus.Core.Ruolo.Contadino] = 3
            }),
        new("Classica (8)", "Il set base, equilibrato e collaudato.",
            new Dictionary<Ruolo, int>
            {
                [Lupus.Core.Ruolo.Lupo] = 2,
                [Lupus.Core.Ruolo.Veggente] = 1,
                [Lupus.Core.Ruolo.Guardiano] = 1,
                [Lupus.Core.Ruolo.Becchino] = 1,
                [Lupus.Core.Ruolo.Contadino] = 3
            }),
        new("Villaggio (10)", "Più poteri informativi e il Cacciatore.",
            new Dictionary<Ruolo, int>
            {
                [Lupus.Core.Ruolo.Lupo] = 2,
                [Lupus.Core.Ruolo.Veggente] = 1,
                [Lupus.Core.Ruolo.Guardiano] = 1,
                [Lupus.Core.Ruolo.Becchino] = 1,
                [Lupus.Core.Ruolo.Cacciatore] = 1,
                [Lupus.Core.Ruolo.Puttana] = 1,
                [Lupus.Core.Ruolo.Spia] = 1,
                [Lupus.Core.Ruolo.Contadino] = 2
            }),
        new("Caos (14)", "Tanti ruoli attivi, notti imprevedibili.",
            new Dictionary<Ruolo, int>
            {
                [Lupus.Core.Ruolo.Lupo] = 3,
                [Lupus.Core.Ruolo.BabyLupo] = 1,
                [Lupus.Core.Ruolo.Veggente] = 1,
                [Lupus.Core.Ruolo.Guardiano] = 1,
                [Lupus.Core.Ruolo.Becchino] = 1,
                [Lupus.Core.Ruolo.Cacciatore] = 1,
                [Lupus.Core.Ruolo.Puttana] = 1,
                [Lupus.Core.Ruolo.Spia] = 1,
                [Lupus.Core.Ruolo.Gufo] = 1,
                [Lupus.Core.Ruolo.Imputato] = 1,
                [Lupus.Core.Ruolo.Contadino] = 2
            }),
        new("Completa (18)", "Quasi tutti i ruoli in una sola partita.",
            new Dictionary<Ruolo, int>
            {
                [Lupus.Core.Ruolo.Lupo] = 3,
                [Lupus.Core.Ruolo.BabyLupo] = 1,
                [Lupus.Core.Ruolo.Veggente] = 1,
                [Lupus.Core.Ruolo.Guardiano] = 1,
                [Lupus.Core.Ruolo.Becchino] = 1,
                [Lupus.Core.Ruolo.Cacciatore] = 1,
                [Lupus.Core.Ruolo.Annullatore] = 1,
                [Lupus.Core.Ruolo.Gufo] = 1,
                [Lupus.Core.Ruolo.Puttana] = 1,
                [Lupus.Core.Ruolo.Spia] = 1,
                [Lupus.Core.Ruolo.Imputato] = 1,
                [Lupus.Core.Ruolo.Copione] = 1,
                [Lupus.Core.Ruolo.Diavolo] = 1,
                [Lupus.Core.Ruolo.Kamikaze] = 1,
                [Lupus.Core.Ruolo.Contadino] = 1,
                [Lupus.Core.Ruolo.Vvelenatu] = 1
            })
    };
}
