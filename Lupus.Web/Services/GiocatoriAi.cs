using Lupus.Core;

namespace Lupus.Web.Services;

/// <summary>
/// Il cervello dei giocatori AI. Non conosce mai la partita grezza: per ogni
/// bot legge solo la vista filtrata di <see cref="StatoGiocatoreFactory"/>,
/// esattamente come il telefono di un giocatore umano, e agisce con le stesse
/// API di <see cref="StanzeService"/> usate dai telefoni.
///
/// Le attese casuali servono a non far sembrare i bot dei pulsanti automatici:
/// il narratore vede i passi avanzare al ritmo di un tavolo vero.
/// </summary>
public sealed class GiocatoriAi
{
    private readonly StanzeService _stanze;

    /// <summary>
    /// Quando ogni AI deve agire. La chiave contiene stanza, giocatore, turno
    /// e passo, così un nuovo turno non eredita l'attesa del precedente.
    /// </summary>
    private readonly Dictionary<string, DateTime> _attese = new();

    /// <summary>
    /// Bersaglio scelto per un passo che chiede conferma: serve ad annotare
    /// l'esito dell'indagine nella memoria del bot.
    /// </summary>
    private readonly Dictionary<string, string> _scelte = new();

    public GiocatoriAi(StanzeService stanze) => _stanze = stanze;

    /// <summary>
    /// Un giro di gioco: esamina tutte le stanze attive e fa muovere gli AI
    /// che hanno qualcosa da fare. Chiamato periodicamente da <see cref="AiWorker"/>.
    /// </summary>
    public async Task TickAsync(CancellationToken cancellationToken = default)
    {
        foreach (var stanza in _stanze.Stanze())
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            if (!stanza.Avviata)
                continue;

            try
            {
                await GiocaStanzaAsync(stanza, cancellationToken);
            }
            catch
            {
                // Un bot che inciampa non deve fermare le altre stanze né il
                // narratore: al prossimo giro riproverà.
            }
        }
    }

    private async Task GiocaStanzaAsync(Stanza stanza, CancellationToken cancellationToken)
    {
        foreach (var (token, giocatoreId) in _stanze.PostiAi(stanza))
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            var giocatore = _stanze.GiocatoreDi(stanza, token);
            if (giocatore == null)
                continue;

            var stato = _stanze.StatoDi(stanza, giocatore);
            if (!stato.Avviata || stato.Finita || !stato.Vivo)
                continue;

            switch (stato.Fase)
            {
                case FasePartita.Notte:
                    await GiocaNotteAsync(stanza, token, giocatoreId, stato);
                    break;

                case FasePartita.Voto:
                    await GiocaVotoAsync(stanza, token, giocatoreId, stato);
                    break;

                case FasePartita.Ballottaggio:
                    await GiocaBallottaggioAsync(stanza, token, giocatoreId, stato);
                    break;
            }
        }
    }

    // ---------------------------------------------------------------------
    // Notte
    // ---------------------------------------------------------------------

    private async Task GiocaNotteAsync(
        Stanza stanza,
        string token,
        string giocatoreId,
        StatoGiocatore stato)
    {
        var azione = stato.Azioni.FirstOrDefault(a => a.PuoAgire);
        if (azione == null)
            return;

        // Il branco è una sola azione condivisa: se c'è almeno un lupo umano
        // al tavolo, la scelta spetta a lui. Gli AI lupi intervengono solo
        // quando il branco è interamente artificiale.
        if (azione.Potere == Potere.Lupo && BrancoHaUnUmano(stanza, stato))
            return;

        var chiave = $"{stanza.Codice}|{giocatoreId}|{stato.Turno}|{azione.PassoId}";

        // I poteri informativi mostrano il risultato e chiedono una conferma:
        // il bot la dà da solo, dopo un attimo, e intanto ricorda l'esito.
        if (azione.RichiedeConferma && !string.IsNullOrWhiteSpace(azione.Risultato))
        {
            if (!AttesaScaduta(chiave + "|conferma", 800, 1800))
                return;

            AnnotaIndagine(stanza, giocatoreId, chiave + "|scelta", azione.Risultato);
            await _stanze.ConfermaRisultatoGiocatoreAsync(stanza, token, azione.PassoId);
            return;
        }

        if (azione.Opzioni.Count == 0)
            return;

        if (!AttesaScaduta(chiave + "|azione", 1500, 4000))
            return;

        var bersaglio = ScegliBersaglio(stanza, giocatoreId, azione);
        if (bersaglio == null)
            return;

        if (Indaga(azione.Potere))
            _scelte[chiave + "|scelta"] = bersaglio;

        await _stanze.EseguiPassoGiocatoreAsync(stanza, token, azione.PassoId, bersaglio);
    }

    private string? ScegliBersaglio(Stanza stanza, string giocatoreId, AzioneGiocatore azione)
    {
        var opzioni = azione.Opzioni.Select(o => o.Id).ToList();
        if (opzioni.Count == 0)
            return null;

        if (Indaga(azione.Potere))
        {
            // Veggente e Becchino non sprecano una notte su chi hanno già
            // guardato: scelgono un volto nuovo finché ne restano.
            var memoria = Memoria(stanza, giocatoreId);
            var nuovi = opzioni.Where(id => !memoria.Investigati.Contains(id)).ToList();
            if (nuovi.Count > 0)
                opzioni = nuovi;
        }

        return opzioni[Random.Shared.Next(opzioni.Count)];
    }

    private static bool Indaga(Potere potere) =>
        potere is Potere.Veggente or Potere.ImputatoVeggente or Potere.Becchino;

    /// <summary>
    /// Vero se tra i compagni di branco c'è almeno un giocatore umano.
    /// L'AI lo sa perché il suo ruolo le mostra i compagni.
    /// </summary>
    private bool BrancoHaUnUmano(Stanza stanza, StatoGiocatore stato)
    {
        if (stato.CompagniBranco.Count == 0)
            return false;

        var posti = _stanze.PostiPubblici(stanza);
        return stato.CompagniBranco.Any(nome =>
            posti.FirstOrDefault(p => p.Nome == nome) is { Occupato: true, Ai: false });
    }

    // ---------------------------------------------------------------------
    // Voto e ballottaggio
    // ---------------------------------------------------------------------

    private async Task GiocaVotoAsync(
        Stanza stanza,
        string token,
        string giocatoreId,
        StatoGiocatore stato)
    {
        if (stato.Voto is not { PuoVotare: true } voto)
            return;

        var chiave = $"{stanza.Codice}|{giocatoreId}|{stato.Turno}|voto";
        if (!AttesaScaduta(chiave, 2000, 5500))
            return;

        var bersaglio = ScegliVoto(stanza, giocatoreId, stato, voto.Candidati);
        if (bersaglio == null)
            return;

        await _stanze.RegistraVotoAsync(stanza, token, bersaglio);
    }

    private async Task GiocaBallottaggioAsync(
        Stanza stanza,
        string token,
        string giocatoreId,
        StatoGiocatore stato)
    {
        if (stato.Ballottaggio is not { PuoVotare: true } ballottaggio)
            return;

        var chiave = $"{stanza.Codice}|{giocatoreId}|{stato.Turno}|ballottaggio";
        if (!AttesaScaduta(chiave, 2000, 5500))
            return;

        var bersaglio = ScegliVoto(stanza, giocatoreId, stato, ballottaggio.Candidati);
        if (bersaglio == null)
            return;

        await _stanze.RegistraVotoBallottaggioAsync(stanza, token, bersaglio);
    }

    /// <summary>
    /// Sceglie il voto usando solo ciò che il ruolo gli ha detto: prima i
    /// sospetti delle indagini, poi — se è un lupo — qualcuno fuori dal branco,
    /// altrimenti un vivo a caso. Il voto resta segreto come per gli umani.
    /// </summary>
    private string? ScegliVoto(
        Stanza stanza,
        string giocatoreId,
        StatoGiocatore stato,
        IReadOnlyList<GiocatorePubblico> candidati)
    {
        var validi = candidati.Where(c => c.Vivo && c.Id != giocatoreId).ToList();
        if (validi.Count == 0)
            return null;

        var memoria = Memoria(stanza, giocatoreId);
        var sospetti = validi.Where(c => memoria.Sospetti.Contains(c.Id)).ToList();
        if (sospetti.Count > 0)
            return sospetti[Random.Shared.Next(sospetti.Count)].Id;

        if (stato.Ruolo == Ruolo.Lupo)
        {
            var fuoriBranco = validi
                .Where(c => !stato.CompagniBranco.Contains(c.Nome))
                .ToList();

            if (fuoriBranco.Count > 0)
                validi = fuoriBranco;
        }

        return validi[Random.Shared.Next(validi.Count)].Id;
    }

    // ---------------------------------------------------------------------
    // Memoria e ritmo
    // ---------------------------------------------------------------------

    private MemoriaAi Memoria(Stanza stanza, string giocatoreId)
    {
        if (!stanza.MemorieAi.TryGetValue(giocatoreId, out var memoria))
        {
            memoria = new MemoriaAi();
            stanza.MemorieAi[giocatoreId] = memoria;
        }

        return memoria;
    }

    private void AnnotaIndagine(
        Stanza stanza,
        string giocatoreId,
        string chiaveScelta,
        string risultato)
    {
        if (!_scelte.Remove(chiaveScelta, out var bersaglio))
            return;

        var memoria = Memoria(stanza, giocatoreId);
        memoria.Investigati.Add(bersaglio);

        if (risultato.Contains("CATTIVO", StringComparison.OrdinalIgnoreCase))
            memoria.Sospetti.Add(bersaglio);
        else if (risultato.Contains("BUONO", StringComparison.OrdinalIgnoreCase))
            memoria.Sospetti.Remove(bersaglio);
    }

    private bool AttesaScaduta(string chiave, int minimoMs, int massimoMs)
    {
        var adesso = DateTime.UtcNow;

        if (!_attese.TryGetValue(chiave, out var quando))
        {
            quando = adesso.AddMilliseconds(Random.Shared.Next(minimoMs, massimoMs + 1));
            _attese[chiave] = quando;
            PulisciAttese(adesso);
        }

        if (adesso < quando)
            return false;

        _attese.Remove(chiave);
        return true;
    }

    private void PulisciAttese(DateTime adesso)
    {
        if (_attese.Count < 256)
            return;

        var scadute = _attese
            .Where(kv => kv.Value < adesso.AddMinutes(-5))
            .Select(kv => kv.Key)
            .ToList();

        foreach (var chiave in scadute)
            _attese.Remove(chiave);
    }
}

/// <summary>
/// Fa girare un giro di gioco al secondo. Gira anche senza circuiti Blazor
/// aperti: il bot non dipende dal browser del narratore.
/// </summary>
public sealed class AiWorker : BackgroundService
{
    private readonly GiocatoriAi _ai;

    public AiWorker(GiocatoriAi ai) => _ai = ai;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
                await _ai.TickAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Arresto del server: uscita pulita.
        }
    }
}
