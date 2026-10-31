using Lupus.Core;

namespace Lupus.Web.Services;

/// <summary>
/// Una stanza multiplayer. Contiene la partita del motore, i posti dei
/// giocatori (ognuno con il proprio token) e i voti in corso.
/// Vive in memoria nel server: non conosce Blazor, JS o HTTP.
/// </summary>
public sealed class Stanza
{
    public string Codice { get; init; } = "";
    public string HostToken { get; init; } = "";
    public Partita Partita { get; init; } = new();
    public List<Posto> Posti { get; init; } = new();

    /// <summary>
    /// Finché è false i giocatori sono nella lobby e non vedono il proprio
    /// ruolo. Il narratore lo imposta a true quando la compagnia è pronta.
    /// </summary>
    public bool Avviata { get; set; }

    public DateTime CreataIl { get; init; } = DateTime.Now;
    public DateTime UltimoAggiornamento { get; set; } = DateTime.Now;

    /// <summary>
    /// Protegge i dizionari letti dai telefoni mentre un altro circuito
    /// registra un voto o un risultato.
    /// </summary>
    public object Sync { get; } = new();

    // --- Voti del rogo e del ballottaggio ---------------------------------
    private readonly Dictionary<string, string> _voti = new();
    private readonly Dictionary<string, string> _votiBallottaggio = new();

    public IReadOnlyDictionary<string, string> Voti
    {
        get
        {
            lock (Sync) return new Dictionary<string, string>(_voti);
        }
    }

    public IReadOnlyDictionary<string, string> VotiBallottaggio
    {
        get
        {
            lock (Sync) return new Dictionary<string, string>(_votiBallottaggio);
        }
    }

    public bool HaVotato(string giocatoreId)
    {
        lock (Sync) return _voti.ContainsKey(giocatoreId);
    }

    public bool HaVotatoBallottaggio(string giocatoreId)
    {
        lock (Sync) return _votiBallottaggio.ContainsKey(giocatoreId);
    }

    public bool RegistraVoto(string giocatoreId, string bersaglioId)
    {
        lock (Sync)
        {
            if (_voti.ContainsKey(giocatoreId)) return false;
            _voti[giocatoreId] = bersaglioId;
            return true;
        }
    }

    public bool RegistraVotoBallottaggio(string giocatoreId, string bersaglioId)
    {
        lock (Sync)
        {
            if (_votiBallottaggio.ContainsKey(giocatoreId)) return false;
            _votiBallottaggio[giocatoreId] = bersaglioId;
            return true;
        }
    }

    public Dictionary<string, string> SnapshotVoti()
    {
        lock (Sync) return new Dictionary<string, string>(_voti);
    }

    public Dictionary<string, string> SnapshotVotiBallottaggio()
    {
        lock (Sync) return new Dictionary<string, string>(_votiBallottaggio);
    }

    public bool VotoChiuso { get; set; }
    public string? EsitoVoto { get; set; }
    public bool VotoPareggio { get; set; }
    public List<string> CandidatiPareggio { get; } = new();

    public bool BallottaggioChiuso { get; set; }
    public string? EsitoBallottaggio { get; set; }
    public bool BallottaggioPareggio { get; set; }
    public List<string> CandidatiPareggioBallottaggio { get; } = new();

    // --- Cosa il narratore ha scelto di rivelare ai telefoni --------------
    public bool RivelaRuoliMorti { get; set; }
    public bool RivelaRuoloRogo { get; set; }

    public Posto? PostoDi(string giocatoreId) =>
        Posti.FirstOrDefault(p => p.GiocatoreId == giocatoreId);

    public Giocatore? GiocatoreDi(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        var posto = Posti.FirstOrDefault(p => p.Token == token);
        return posto == null ? null : Partita.Giocatore(posto.GiocatoreId);
    }

    public int PostiOccupati => Posti.Count(p => p.Occupato);

    public bool TuttiPresenti => Posti.Count > 0 && Posti.All(p => p.Occupato);

    public void AzzeraVoti()
    {
        lock (Sync)
        {
            _voti.Clear();
            _votiBallottaggio.Clear();

            VotoChiuso = false;
            EsitoVoto = null;
            VotoPareggio = false;
            CandidatiPareggio.Clear();

            BallottaggioChiuso = false;
            EsitoBallottaggio = null;
            BallottaggioPareggio = false;
            CandidatiPareggioBallottaggio.Clear();
        }
    }

    public void AzzeraVotiBallottaggio()
    {
        lock (Sync)
        {
            _votiBallottaggio.Clear();
            BallottaggioChiuso = false;
            EsitoBallottaggio = null;
            BallottaggioPareggio = false;
            CandidatiPareggioBallottaggio.Clear();
        }
    }
}

/// <summary>
/// Un posto a tavola. Il token è il segreto che lega il telefono al
/// giocatore; il narratore può azzerarlo per far rientrare qualcuno.
/// </summary>
public sealed class Posto
{
    public string GiocatoreId { get; init; } = "";
    public string? Token { get; set; }
    public DateTime? UltimoAccesso { get; set; }

    public bool Occupato => !string.IsNullOrWhiteSpace(Token);
}
