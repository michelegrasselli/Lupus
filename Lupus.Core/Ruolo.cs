namespace Lupus.Core;

/// <summary>
/// Ruoli disponibili. I nomi ricalcano quelli della versione console.
/// </summary>
public enum Ruolo
{
    Lupo,
    BabyLupo,
    Veggente,
    Guardiano,
    Becchino,
    Annullatore,
    Cacciatore,
    Puttana,
    Spia,
    Imputato,
    Copione,
    Copione2,
    Diavolo,
    Contadino,
    Vvelenatu,
    Kamikaze,
    Gufo,
    Sfigato
}

/// <summary>
/// Metadati di un ruolo: nome leggibile, descrizione, priorità notturna e fazione.
/// </summary>
public sealed record RuoloInfo(
    Ruolo Ruolo,
    string Nome,
    string Descrizione,
    int Priorita,
    bool Buono,
    bool AzioneNotturna,
    string Icona,
    string Accento);

public static class Ruoli
{
    public static readonly IReadOnlyList<RuoloInfo> Tutti = new List<RuoloInfo>
    {
        new(Ruolo.Lupo, "Lupo",
            "Ogni notte, insieme al branco, sceglie chi sbranare. Se restano più lupi che buoni, vince.",
            3, false, true, "🐺", "#e05a5a"),
        new(Ruolo.BabyLupo, "Baby Lupo",
            "Se il branco lo attacca, invece di morire diventa un Lupo.",
            1000, true, false, "🐾", "#d98c5f"),
        new(Ruolo.Veggente, "Veggente",
            "Scopre se un giocatore è buono o cattivo.",
            8, true, true, "🔮", "#8b7cf6"),
        new(Ruolo.Guardiano, "Guardiano",
            "Protegge un giocatore dagli attacchi notturni.",
            4, true, true, "🛡️", "#4aa3df"),
        new(Ruolo.Becchino, "Becchino",
            "Scopre l'allineamento di un giocatore già morto.",
            6, true, true, "⚰️", "#8b93a7"),
        new(Ruolo.Annullatore, "Annullatore",
            "Blocca il potere di un giocatore per una notte. I lupi sono immuni se sono in branco.",
            1, false, true, "🚫", "#c084fc"),
        new(Ruolo.Cacciatore, "Cacciatore",
            "Piazza una trappola: se il branco attacca quel giocatore, muore il capobranco. Non può piazzarla due notti di fila.",
            2, true, true, "🪤", "#c9a227"),
        new(Ruolo.Puttana, "Puttana",
            "Passa la notte con un giocatore. Se sceglie un Lupo, muore all'alba.",
            7, true, true, "💃", "#e879a6"),
        new(Ruolo.Spia, "Spia",
            "Scopre chi ha puntato un giocatore durante la notte.",
            9, true, true, "🕵️", "#5fb3a1"),
        new(Ruolo.Imputato, "Imputato",
            "Copia i poteri di chi lo ha puntato quella notte e li usa subito.",
            10, true, true, "⚖️", "#e0b15a"),
        new(Ruolo.Copione, "Copione",
            "Copia il ruolo di un giocatore: dalla notte successiva diventa quel ruolo.",
            11, true, true, "🎭", "#b78ce8"),
        new(Ruolo.Copione2, "Copione 2",
            "Secondo Copione, con le stesse regole. Utile per partite numerose.",
            12, true, true, "🎭", "#9d7ad1"),
        new(Ruolo.Diavolo, "Diavolo",
            "Se il branco lo attacca sopravvive. Se viene anche votato quel giorno, vince da solo.",
            1000, false, false, "😈", "#ef4444"),
        new(Ruolo.Contadino, "Contadino",
            "Nessun potere. Il suo voto è la sua arma.",
            1000, true, false, "🌾", "#84cc16"),
        new(Ruolo.Vvelenatu, "Avvelenato",
            "Nessun potere. Ruolo passivo del villaggio.",
            1000, true, false, "☠️", "#65a30d"),
        new(Ruolo.Kamikaze, "Kamikaze",
            "Se il branco lo attacca, esplode e porta con sé il capobranco.",
            1000, true, false, "💥", "#f97316"),
        new(Ruolo.Gufo, "Gufo",
            "Manda un giocatore al ballottaggio del giorno successivo.",
            5, true, true, "🦉", "#a78bfa"),
        new(Ruolo.Sfigato, "Sfigato",
            "Nessun potere. Appare cattivo alle indagini, ma non è un Lupo e non conta per la vittoria del branco.",
            1000, false, false, "🍀", "#94a3b8")
    };

    private static readonly Dictionary<Ruolo, RuoloInfo> PerRuolo =
        Tutti.ToDictionary(r => r.Ruolo);

    public static RuoloInfo Info(Ruolo ruolo) => PerRuolo[ruolo];

    public static bool IsLupo(Ruolo ruolo) => ruolo == Ruolo.Lupo;

    public static string Nome(Ruolo ruolo) => Info(ruolo).Nome;

    public static string Fazione(Ruolo ruolo) => Info(ruolo).Buono ? "Buono" : "Cattivo";
}
