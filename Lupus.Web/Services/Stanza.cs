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
    /// Quello che un giocatore AI ha imparato con il proprio ruolo (per ora:
    /// chi ha indagato e chi ritiene cattivo). È privato per costruzione: lo
    /// legge solo <see cref="GiocatoriAi"/>, mai le viste dei giocatori.
    /// </summary>
    public Dictionary<string, MemoriaAi> MemorieAi { get; } = new();

    /// <summary>
    /// Finché è false i giocatori sono nella lobby e non vedono il proprio
    /// ruolo. Il narratore lo imposta a true quando la compagnia è pronta.
    /// </summary>
    public bool Avviata { get; set; }

    public DateTime CreataIl { get; init; } = DateTime.Now;
    public DateTime UltimoAggiornamento { get; set; } = DateTime.Now;

    // --- Chat della piazza (solo fase Giorno) -----------------------------
    private readonly List<MessaggioChat> _chat = new();
    private int _chatSeq;

    /// <summary>
    /// Ultimi messaggi della piazza, in ordine cronologico. La chat vive solo
    /// nella fase di discussione: è pubblica per tutti i giocatori vivi.
    /// </summary>
    public IReadOnlyList<MessaggioChat> SnapshotChat(int ultimi = 80)
    {
        lock (Sync)
        {
            var salta = Math.Max(0, _chat.Count - ultimi);
            return _chat.Skip(salta).ToList();
        }
    }

    public MessaggioChat AggiungiMessaggioChat(
        string autoreId,
        string autoreNome,
        bool autoreAi,
        string testo,
        int turno,
        string? destinatarioId,
        bool domanda,
        string? rispostaA)
    {
        lock (Sync)
        {
            var messaggio = new MessaggioChat
            {
                Id = $"c{++_chatSeq}",
                AutoreId = autoreId,
                AutoreNome = autoreNome,
                AutoreAi = autoreAi,
                Testo = testo,
                Turno = turno,
                Quando = DateTime.Now,
                DestinatarioId = destinatarioId,
                Domanda = domanda,
                RispostaA = rispostaA
            };

            _chat.Add(messaggio);

            // La piazza non deve crescere all'infinito: il narratore e i
            // telefoni mostrano comunque solo la coda della conversazione.
            if (_chat.Count > 200)
                _chat.RemoveRange(0, _chat.Count - 200);

            return messaggio;
        }
    }

    /// <summary>
    /// Domande ancora senza risposta che questo AI può gestire: quelle rivolte
    /// a lui e quelle pubbliche del turno in corso.
    /// </summary>
    public IReadOnlyList<MessaggioChat> DomandeApertePerAi(string giocatoreId, int turno)
    {
        lock (Sync)
        {
            var gestite = _chat
                .Where(m => !string.IsNullOrWhiteSpace(m.RispostaA))
                .Select(m => m.RispostaA!)
                .ToHashSet();

            return _chat
                .Where(m => m.Domanda
                            && !m.AutoreAi
                            && !gestite.Contains(m.Id)
                            && m.Turno == turno
                            && (m.DestinatarioId == giocatoreId
                                || m.DestinatarioId == null))
                .ToList();
        }
    }

    public bool DomandaGiaGestita(string messaggioId)
    {
        lock (Sync)
        {
            return _chat.Any(m => m.RispostaA == messaggioId);
        }
    }

    public MessaggioChat? MessaggioChatDi(string messaggioId)
    {
        lock (Sync)
        {
            return _chat.FirstOrDefault(m => m.Id == messaggioId);
        }
    }

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

    public bool BallottaggioChiuso { get; set; }
    public string? EsitoBallottaggio { get; set; }

    /// <summary>
    /// Il ballottaggio nasce da un pareggio al rogo: è la seconda votazione
    /// tra i più votati (e gli eventuali segnalati dal Gufo).
    /// </summary>
    public bool BallottaggioDaPareggio { get; set; }

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

            BallottaggioChiuso = false;
            EsitoBallottaggio = null;
            BallottaggioDaPareggio = false;
        }
    }

    public void AzzeraVotiBallottaggio()
    {
        lock (Sync)
        {
            _votiBallottaggio.Clear();
            BallottaggioChiuso = false;
            EsitoBallottaggio = null;
            BallottaggioDaPareggio = false;
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

    /// <summary>
    /// Il posto è affidato a un giocatore AI: il token esiste ma non viene
    /// mostrato a nessuno, perché è il server a giocare al posto suo.
    /// </summary>
    public bool IsAi { get; set; }

    public bool Occupato => !string.IsNullOrWhiteSpace(Token);
}

/// <summary>
/// Informazioni che un giocatore AI ha ottenuto tramite il proprio ruolo.
/// Restano nella stanza e non vengono mai serializzate verso i telefoni.
/// </summary>
public sealed class MemoriaAi
{
    /// <summary>Giocatori che una Veggente/Becchino AI ritiene CATTIVI.</summary>
    public HashSet<string> Sospetti { get; } = new();

    /// <summary>Giocatori già indagati: evita di sprecare altre notti.</summary>
    public HashSet<string> Investigati { get; } = new();

    /// <summary>Turno a cui si riferiscono i contatori della chat.</summary>
    public int TurnoChat { get; set; }

    /// <summary>Commenti spontanei già fatti nella piazza di questo turno.</summary>
    public int CommentiChat { get; set; }

    /// <summary>Risposte alle domande già date in questo turno.</summary>
    public int RisposteChat { get; set; }

    /// <summary>Reazioni pubbliche alle informazioni degli umani in questo turno.</summary>
    public int ReazioniChat { get; set; }

    /// <summary>Messaggi umani già letti: ognuno si valuta una volta sola.</summary>
    public HashSet<string> MessaggiValutati { get; } = new();

    /// <summary>Cosa l'AI ha deciso di credere dei messaggi degli umani.</summary>
    public Dictionary<string, ValutazioneMessaggio> Valutazioni { get; } = new();

    /// <summary>Giocatori umani di cui l'AI si fida (o non si fida) dopo la piazza.</summary>
    public HashSet<string> AutoriFidati { get; } = new();
    public HashSet<string> AutoriDiffidati { get; } = new();
}

/// <summary>
/// Il giudizio di un AI su un'informazione raccontata da un umano: serve per
/// reagire in chat e per aggiornare i sospetti. Resta privato nella memoria AI.
/// </summary>
public sealed class ValutazioneMessaggio
{
    /// <summary>L'AI crede a quello che ha letto.</summary>
    public bool Fidato { get; init; }

    /// <summary>L'informazione accusava (true) o scagionava (false) qualcuno.</summary>
    public bool Negativo { get; init; }

    /// <summary>Giocatore di cui parlava il messaggio.</summary>
    public string Nome { get; init; } = "";

    public string AutoreNome { get; init; } = "";

    /// <summary>Turno in cui è arrivato il messaggio.</summary>
    public int Turno { get; init; }

    /// <summary>L'informazione parlava proprio dell'AI che l'ha letta.</summary>
    public bool ControDiSe { get; init; }

    /// <summary>La reazione pubblica dell'AI è già stata inviata (o saltata).</summary>
    public bool ReazioneGestita { get; set; }
}

/// <summary>
/// Un messaggio della piazza del villaggio. La chat esiste solo durante la
/// fase di discussione: è pubblica e non contiene informazioni di ruolo.
/// </summary>
public sealed class MessaggioChat
{
    public string Id { get; init; } = "";
    public string AutoreId { get; init; } = "";
    public string AutoreNome { get; init; } = "";
    public bool AutoreAi { get; init; }
    public string Testo { get; init; } = "";
    public int Turno { get; init; }
    public DateTime Quando { get; init; } = DateTime.Now;

    /// <summary>AI a cui è rivolta la domanda; null per un messaggio pubblico.</summary>
    public string? DestinatarioId { get; init; }

    /// <summary>Il messaggio è una domanda che attende una risposta.</summary>
    public bool Domanda { get; init; }

    /// <summary>Id della domanda a cui questo messaggio risponde (o rifiuta di rispondere).</summary>
    public string? RispostaA { get; init; }
}
