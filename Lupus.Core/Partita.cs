namespace Lupus.Core;

public sealed class Partita
{
    public int Versione { get; set; } = 1;
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime CreataIl { get; set; } = DateTime.Now;
    public DateTime? SalvataIl { get; set; }

    public List<Giocatore> Giocatori { get; set; } = new();
    public int Turno { get; set; } = 1;
    public FasePartita Fase { get; set; } = FasePartita.NotteIntro;

    public List<PassoNotte> PassiNotte { get; set; } = new();
    public bool PassiImputatoGenerati { get; set; }
    public List<AzioneNotte> AzioniNotte { get; set; } = new();
    public RisultatoNotte? RisultatoNotte { get; set; }

    public string? VotatoId { get; set; }
    public string? UltimoRogoId { get; set; }
    public List<string> BallottaggioIds { get; set; } = new();
    public EsitoPartita? Esito { get; set; }

    public List<VoceLog> Log { get; set; } = new();
    public int TimerSecondi { get; set; } = 300;

    public IEnumerable<Giocatore> Vivi => Giocatori.Where(g => g.Vivo);
    public IEnumerable<Giocatore> LupiVivi => Vivi.Where(g => g.Ruolo == Ruolo.Lupo);
    public IEnumerable<Giocatore> AltriVivi => Vivi.Where(g => g.Ruolo != Ruolo.Lupo);

    public int LupiViviCount => LupiVivi.Count();
    public int AltriViviCount => AltriVivi.Count();
    public int TotaleVivi => Vivi.Count();

    public Giocatore? Capobranco => Giocatori.FirstOrDefault(g => g.Vivo && g.Capobranco);

    public Giocatore? Giocatore(string? id) =>
        string.IsNullOrWhiteSpace(id) ? null : Giocatori.FirstOrDefault(g => g.Id == id);

    public Giocatore? PerNome(string nome) =>
        Giocatori.FirstOrDefault(g => string.Equals(g.Nome, nome, StringComparison.OrdinalIgnoreCase));

    public string NomeDi(string? id) => Giocatore(id)?.Nome ?? "?";

    public PassoNotte? PassoCorrente => PassiNotte.FirstOrDefault(p => !p.Completato);

    public bool NotteCompleta => PassiNotte.All(p => p.Completato);

    public void AggiungiLog(string testo, TipoLog tipo = TipoLog.Info)
    {
        Log.Add(new VoceLog
        {
            Turno = Turno,
            Fase = Fase,
            Tipo = tipo,
            Testo = testo
        });

        if (Log.Count > 1000)
        {
            Log.RemoveRange(0, Log.Count - 1000);
        }
    }

    public void AggiungiEvento(RisultatoNotte risultato, string testo, bool riservato = false)
    {
        risultato.Eventi.Add(new EventoNotte(testo, riservato));
    }

    /// <summary>
    /// Ripara i riferimenti nulli che possono arrivare da un JSON esterno.
    /// </summary>
    public void Normalizza()
    {
        Giocatori ??= new();
        PassiNotte ??= new();
        AzioniNotte ??= new();
        BallottaggioIds ??= new();
        Log ??= new();

        foreach (var g in Giocatori)
        {
            g.Nome ??= "";
        }

        foreach (var passo in PassiNotte)
        {
            passo.OpzioniIds ??= new();
        }

        RisultatoNotte?.Normalizza();
    }

    /// <summary>
    /// Ripulisce i flag temporanei all'inizio di una nuova notte.
    /// </summary>
    public void PreparaNuovaNotte()
    {
        foreach (var g in Giocatori)
        {
            g.MortoInNotte = false;
            g.MortoAlRogo = false;
        }

        PassiNotte.Clear();
        PassiImputatoGenerati = false;
        AzioniNotte.Clear();
        RisultatoNotte = null;
        VotatoId = null;
        UltimoRogoId = null;
        BallottaggioIds.Clear();
    }
}
