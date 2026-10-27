namespace Lupus.Core;

public enum FasePartita
{
    NotteIntro,
    Notte,
    Alba,
    Giorno,
    Voto,
    Ballottaggio,
    Esecuzione,
    Finita
}

public enum EsitoPartita
{
    Buoni,
    Lupi,
    Diavolo,
    Pareggio
}

public enum TipoLog
{
    Configurazione,
    Notte,
    Potere,
    Morte,
    Trasformazione,
    Voto,
    Vittoria,
    Info
}

public enum TipoPasso
{
    Capobranco,
    Azione
}

/// <summary>
/// Poteri che possono essere registrati durante la notte. I poteri "Imputato*"
/// sono copie usate dall'Imputato grazie al ruolo di chi lo ha puntato.
/// </summary>
public enum Potere
{
    Lupo,
    Guardiano,
    Annullatore,
    Becchino,
    Veggente,
    Puttana,
    Spia,
    Cacciatore,
    Gufo,
    ImputatoLupo,
    ImputatoGuardiano,
    ImputatoPuttana,
    ImputatoVeggente,
    ImputatoSpia,
    ImputatoCacciatore,
    ImputatoGufo,
    Copione,
    Copione2
}

public sealed class AzioneNotte
{
    public Potere Potere { get; set; }
    public string AttoreId { get; set; } = "";
    public string BersaglioId { get; set; } = "";
}

public sealed class PassoNotte
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public TipoPasso Tipo { get; set; } = TipoPasso.Azione;
    public Potere Potere { get; set; }
    public string? AttoreId { get; set; }
    public int Priorita { get; set; } = 1000;
    public string Titolo { get; set; } = "";
    public string Istruzione { get; set; } = "";
    public List<string> OpzioniIds { get; set; } = new();
    public bool RichiedeConferma { get; set; }
    public bool Completato { get; set; }
    public bool Saltato { get; set; }
    public string? Risultato { get; set; }
}

public sealed class VoceLog
{
    public int Turno { get; set; }
    public FasePartita Fase { get; set; }
    public TipoLog Tipo { get; set; }
    public string Testo { get; set; } = "";
    public DateTime Ora { get; set; } = DateTime.Now;
}

public sealed class EventoNotte
{
    public string Testo { get; set; } = "";
    public bool Riservato { get; set; }

    public EventoNotte() { }

    public EventoNotte(string testo, bool riservato = false)
    {
        Testo = testo;
        Riservato = riservato;
    }
}

public sealed class RisultatoNotte
{
    public List<string> MortiIds { get; set; } = new();
    public List<string> TrasformatiIds { get; set; } = new();
    public List<EventoNotte> Eventi { get; set; } = new();

    public bool NessunMorto => MortiIds.Count == 0;

    public void Normalizza()
    {
        MortiIds ??= new();
        TrasformatiIds ??= new();
        Eventi ??= new();
    }
}

public sealed class EsitoPasso
{
    public bool Ok { get; init; }
    public string? Errore { get; init; }

    public static EsitoPasso Successo() => new() { Ok = true };
    public static EsitoPasso Fallito(string errore) => new() { Ok = false, Errore = errore };
}
