using Lupus.Core;

namespace Lupus.Web.Services;

/// <summary>
/// Registro delle stanze multiplayer. È un singleton: tutte le schede e tutti
/// i telefoni collegati allo stesso server vedono la stessa stanza.
///
/// Le mutazioni sono protette da un lock; le notifiche ai circuiti Blazor
/// avvengono sempre fuori dal lock, così una scheda lenta non blocca le altre.
/// </summary>
public sealed class StanzeService
{
    private const string Alfabeto = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    private readonly object _lock = new();
    private readonly Dictionary<string, Stanza> _stanze = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<Func<Task>>> _sottoscrittori = new(StringComparer.OrdinalIgnoreCase);

    // ---------------------------------------------------------------------
    // Creazione e ricerca
    // ---------------------------------------------------------------------

    public (Stanza Stanza, string HostToken) Crea(
        IReadOnlyList<string> nomi,
        IReadOnlyDictionary<Ruolo, int> composizione,
        bool assegnaCasualmente = true)
    {
        var partita = CreazionePartita.Crea(nomi, composizione, assegnaCasualmente);
        return CreaDaPartita(partita, avviata: false);
    }

    /// <summary>
    /// Crea una stanza da una partita già esistente (import JSON o ripresa).
    /// </summary>
    public (Stanza Stanza, string HostToken) CreaDaPartita(Partita partita, bool avviata)
    {
        partita.Normalizza();

        lock (_lock)
        {
            Pulizia();

            string codice;
            do
            {
                codice = GeneraCodice();
            }
            while (_stanze.ContainsKey(codice));

            var stanza = new Stanza
            {
                Codice = codice,
                HostToken = Guid.NewGuid().ToString("N"),
                Partita = partita,
                Avviata = avviata
            };

            stanza.Posti.AddRange(partita.Giocatori.Select(g => new Posto
            {
                GiocatoreId = g.Id
            }));

            _stanze[codice] = stanza;
            _sottoscrittori[codice] = new List<Func<Task>>();

            return (stanza, stanza.HostToken);
        }
    }

    public Stanza? Ottieni(string? codice)
    {
        if (string.IsNullOrWhiteSpace(codice)) return null;

        lock (_lock)
        {
            Pulizia();
            if (!_stanze.TryGetValue(codice.Trim(), out var stanza)) return null;
            stanza.UltimoAggiornamento = DateTime.Now;
            return stanza;
        }
    }

    public bool ValidaHost(Stanza stanza, string? token) =>
        !string.IsNullOrWhiteSpace(token) &&
        string.Equals(stanza.HostToken, token, StringComparison.Ordinal);

    /// <summary>
    /// Legge lo stato di un giocatore con il lock della stanza, così un voto
    /// registrato da un altro telefono non può rompere l'enumerazione.
    /// </summary>
    public StatoGiocatore StatoDi(Stanza stanza, Giocatore giocatore)
    {
        lock (_lock)
        {
            return StatoGiocatoreFactory.Crea(stanza, giocatore);
        }
    }

    public Giocatore? GiocatoreDi(Stanza stanza, string? token)
    {
        lock (_lock)
        {
            return stanza.GiocatoreDi(token);
        }
    }

    public IReadOnlyList<PostoStanza> PostiPubblici(Stanza stanza)
    {
        lock (_lock)
        {
            return stanza.Posti
                .Select(posto =>
                {
                    var giocatore = stanza.Partita.Giocatore(posto.GiocatoreId);
                    return new PostoStanza(
                        posto.GiocatoreId,
                        giocatore?.Nome ?? "?",
                        giocatore?.Iniziali ?? "?",
                        posto.Occupato);
                })
                .ToList();
        }
    }

    // ---------------------------------------------------------------------
    // Ingresso dei giocatori
    // ---------------------------------------------------------------------

    public async Task<(bool Ok, string? Errore, Posto? Posto)> UnisciAsync(string codice, string giocatoreId)
    {
        (bool Ok, string? Errore, Posto? Posto) risultato;

        lock (_lock)
        {
            var stanza = Trova(codice);
            if (stanza == null)
            {
                risultato = (false, "Stanza non trovata.", null);
            }
            else if (stanza.Partita.Fase == FasePartita.Finita)
            {
                risultato = (false, "La partita è già conclusa.", null);
            }
            else
            {
                var posto = stanza.PostoDi(giocatoreId);
                if (posto == null)
                {
                    risultato = (false, "Posto non trovato.", null);
                }
                else if (posto.Occupato)
                {
                    risultato = (false, "Questo posto è già occupato. Chiedi al narratore di sbloccarlo.", null);
                }
                else
                {
                    posto.Token = Guid.NewGuid().ToString("N");
                    posto.UltimoAccesso = DateTime.Now;
                    stanza.UltimoAggiornamento = DateTime.Now;
                    risultato = (true, null, posto);
                }
            }
        }

        if (risultato.Ok)
            await NotificaAsync(codice);

        return risultato;
    }

    public async Task<EsitoPasso> LiberaPostoAsync(Stanza stanza, string hostToken, string giocatoreId)
    {
        if (!ValidaHost(stanza, hostToken))
            return EsitoPasso.Fallito("Token narratore non valido.");

        lock (_lock)
        {
            var posto = stanza.PostoDi(giocatoreId);
            if (posto == null)
                return EsitoPasso.Fallito("Posto non trovato.");

            posto.Token = null;
            posto.UltimoAccesso = null;
            stanza.UltimoAggiornamento = DateTime.Now;
        }

        await NotificaAsync(stanza.Codice);
        return EsitoPasso.Successo();
    }

    /// <summary>
    /// Il giocatore abbandona il posto dal proprio telefono: il posto torna
    /// libero e un altro dispositivo può reclamarlo.
    /// </summary>
    public async Task<EsitoPasso> LasciaPostoAsync(Stanza stanza, string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return EsitoPasso.Fallito("Sessione non valida.");

        lock (_lock)
        {
            var posto = stanza.Posti.FirstOrDefault(p =>
                string.Equals(p.Token, token, StringComparison.Ordinal));

            if (posto == null)
                return EsitoPasso.Fallito("Posto non trovato.");

            posto.Token = null;
            posto.UltimoAccesso = null;
            stanza.UltimoAggiornamento = DateTime.Now;
        }

        await NotificaAsync(stanza.Codice);
        return EsitoPasso.Successo();
    }

    // ---------------------------------------------------------------------
    // Avvio e fasi
    // ---------------------------------------------------------------------

    public async Task<EsitoPasso> AvviaAsync(Stanza stanza, string hostToken)
    {
        if (!ValidaHost(stanza, hostToken))
            return EsitoPasso.Fallito("Token narratore non valido.");

        lock (_lock)
        {
            if (stanza.Avviata)
                return EsitoPasso.Fallito("La partita è già avviata.");

            stanza.Avviata = true;
            stanza.Partita.AggiungiLog(
                "La stanza è avviata: ogni giocatore vede il proprio ruolo sul telefono.",
                TipoLog.Configurazione);
            stanza.UltimoAggiornamento = DateTime.Now;
        }

        await NotificaAsync(stanza.Codice);
        return EsitoPasso.Successo();
    }

    public async Task<EsitoPasso> IniziaNotteAsync(Stanza stanza, string hostToken)
    {
        if (!ValidaHost(stanza, hostToken))
            return EsitoPasso.Fallito("Token narratore non valido.");

        lock (_lock)
        {
            var p = stanza.Partita;
            if (p.Fase != FasePartita.NotteIntro)
                return EsitoPasso.Fallito("Non è il momento di iniziare la notte.");

            MotoreLupus.IniziaNotte(p);
            stanza.AzzeraVoti();
            stanza.RivelaRuoliMorti = false;
            stanza.RivelaRuoloRogo = false;
            stanza.RegistraRisultati();
            stanza.UltimoAggiornamento = DateTime.Now;
        }

        await NotificaAsync(stanza.Codice);
        return EsitoPasso.Successo();
    }

    public async Task<EsitoPasso> RisolviNotteAsync(Stanza stanza, string hostToken)
    {
        if (!ValidaHost(stanza, hostToken))
            return EsitoPasso.Fallito("Token narratore non valido.");

        EsitoPasso esito;

        lock (_lock)
        {
            esito = MotoreLupus.RisolviNotte(stanza.Partita);
            if (esito.Ok)
            {
                stanza.RegistraRisultati();
                stanza.RivelaRuoliMorti = false;
                stanza.RivelaRuoloRogo = false;
                stanza.UltimoAggiornamento = DateTime.Now;
            }
        }

        if (esito.Ok)
            await NotificaAsync(stanza.Codice);

        return esito;
    }

    public async Task<EsitoPasso> VaiAlGiornoAsync(Stanza stanza, string hostToken)
    {
        if (!ValidaHost(stanza, hostToken))
            return EsitoPasso.Fallito("Token narratore non valido.");

        lock (_lock)
        {
            if (stanza.Partita.Fase != FasePartita.Alba)
                return EsitoPasso.Fallito("Non è il momento di passare al giorno.");

            MotoreLupus.VaiAlGiorno(stanza.Partita);
            stanza.UltimoAggiornamento = DateTime.Now;
        }

        await NotificaAsync(stanza.Codice);
        return EsitoPasso.Successo();
    }

    public async Task<EsitoPasso> ApriVotoAsync(Stanza stanza, string hostToken)
    {
        if (!ValidaHost(stanza, hostToken))
            return EsitoPasso.Fallito("Token narratore non valido.");

        lock (_lock)
        {
            if (stanza.Partita.Fase != FasePartita.Giorno)
                return EsitoPasso.Fallito("Non è il momento di aprire il voto.");

            MotoreLupus.ApriVoto(stanza.Partita);
            stanza.AzzeraVoti();
            stanza.UltimoAggiornamento = DateTime.Now;
        }

        await NotificaAsync(stanza.Codice);
        return EsitoPasso.Successo();
    }

    public async Task<EsitoPasso> ProssimaNotteAsync(Stanza stanza, string hostToken)
    {
        if (!ValidaHost(stanza, hostToken))
            return EsitoPasso.Fallito("Token narratore non valido.");

        lock (_lock)
        {
            if (stanza.Partita.Fase != FasePartita.Esecuzione)
                return EsitoPasso.Fallito("Non è il momento di calare la notte.");

            MotoreLupus.ProssimaNotte(stanza.Partita);
            stanza.AzzeraVoti();
            stanza.RivelaRuoliMorti = false;
            stanza.RivelaRuoloRogo = false;
            stanza.UltimoAggiornamento = DateTime.Now;
        }

        await NotificaAsync(stanza.Codice);
        return EsitoPasso.Successo();
    }

    // ---------------------------------------------------------------------
    // Azioni notturne
    // ---------------------------------------------------------------------

    public async Task<EsitoPasso> EseguiPassoGiocatoreAsync(
        Stanza stanza,
        string token,
        string passoId,
        string bersaglioId)
    {
        EsitoPasso esito;

        lock (_lock)
        {
            var giocatore = stanza.GiocatoreDi(token);
            if (giocatore == null)
                return EsitoPasso.Fallito("Sessione non valida: rientra dalla stanza.");

            var p = stanza.Partita;
            if (p.Fase != FasePartita.Notte)
                return EsitoPasso.Fallito("Non è il momento delle azioni notturne.");
            if (!giocatore.Vivo)
                return EsitoPasso.Fallito("Sei fuori gioco: non puoi agire.");

            var passo = p.PassiNotte.FirstOrDefault(x => x.Id == passoId);
            if (passo == null)
                return EsitoPasso.Fallito("Azione non trovata.");
            if (!StatoGiocatoreFactory.Autorizzato(giocatore, passo))
                return EsitoPasso.Fallito("Non puoi compiere questa azione.");
            if (p.PassoCorrente?.Id != passo.Id)
                return EsitoPasso.Fallito("Non è ancora il tuo turno.");

            esito = MotoreLupus.EseguiPasso(p, passoId, bersaglioId);
            if (esito.Ok)
            {
                stanza.RegistraRisultati();
                stanza.UltimoAggiornamento = DateTime.Now;
            }
        }

        if (esito.Ok)
            await NotificaAsync(stanza.Codice);

        return esito;
    }

    public async Task<EsitoPasso> ConfermaRisultatoGiocatoreAsync(
        Stanza stanza,
        string token,
        string passoId)
    {
        lock (_lock)
        {
            var giocatore = stanza.GiocatoreDi(token);
            if (giocatore == null)
                return EsitoPasso.Fallito("Sessione non valida: rientra dalla stanza.");

            var p = stanza.Partita;
            if (p.Fase != FasePartita.Notte)
                return EsitoPasso.Fallito("Non è il momento delle azioni notturne.");
            if (!giocatore.Vivo)
                return EsitoPasso.Fallito("Sei fuori gioco: non puoi agire.");

            var passo = p.PassiNotte.FirstOrDefault(x => x.Id == passoId);
            if (passo == null)
                return EsitoPasso.Fallito("Azione non trovata.");
            if (!StatoGiocatoreFactory.Autorizzato(giocatore, passo))
                return EsitoPasso.Fallito("Non puoi confermare questa azione.");
            if (p.PassoCorrente?.Id != passo.Id)
                return EsitoPasso.Fallito("Non è ancora il tuo turno.");

            MotoreLupus.ConfermaRisultato(p, passoId);
            stanza.RegistraRisultati();
            stanza.UltimoAggiornamento = DateTime.Now;
        }

        await NotificaAsync(stanza.Codice);
        return EsitoPasso.Successo();
    }

    public async Task<EsitoPasso> EseguiPassoNarratoreAsync(
        Stanza stanza,
        string hostToken,
        string passoId,
        string bersaglioId)
    {
        if (!ValidaHost(stanza, hostToken))
            return EsitoPasso.Fallito("Token narratore non valido.");

        EsitoPasso esito;

        lock (_lock)
        {
            var p = stanza.Partita;
            if (p.Fase != FasePartita.Notte)
                return EsitoPasso.Fallito("Non è il momento delle azioni notturne.");
            if (p.PassoCorrente?.Id != passoId)
                return EsitoPasso.Fallito("Non è il passo corrente: aggiorna la pagina.");

            esito = MotoreLupus.EseguiPasso(p, passoId, bersaglioId);
            if (esito.Ok)
            {
                stanza.RegistraRisultati();
                stanza.UltimoAggiornamento = DateTime.Now;
            }
        }

        if (esito.Ok)
            await NotificaAsync(stanza.Codice);

        return esito;
    }

    public async Task<EsitoPasso> ConfermaRisultatoNarratoreAsync(
        Stanza stanza,
        string hostToken,
        string passoId)
    {
        if (!ValidaHost(stanza, hostToken))
            return EsitoPasso.Fallito("Token narratore non valido.");

        lock (_lock)
        {
            var p = stanza.Partita;
            if (p.PassoCorrente?.Id != passoId)
                return EsitoPasso.Fallito("Non è il passo corrente: aggiorna la pagina.");

            MotoreLupus.ConfermaRisultato(p, passoId);
            stanza.RegistraRisultati();
            stanza.UltimoAggiornamento = DateTime.Now;
        }

        await NotificaAsync(stanza.Codice);
        return EsitoPasso.Successo();
    }

    public async Task<EsitoPasso> ImpostaDurataTimerAsync(
        Stanza stanza,
        string hostToken,
        int secondi)
    {
        if (!ValidaHost(stanza, hostToken))
            return EsitoPasso.Fallito("Token narratore non valido.");

        lock (_lock)
        {
            stanza.Partita.TimerSecondi = Math.Clamp(secondi, 30, 3600);
            stanza.UltimoAggiornamento = DateTime.Now;
        }

        await NotificaAsync(stanza.Codice);
        return EsitoPasso.Successo();
    }

    // ---------------------------------------------------------------------
    // Voto del rogo
    // ---------------------------------------------------------------------

    public async Task<EsitoPasso> RegistraVotoAsync(Stanza stanza, string token, string bersaglioId)
    {
        lock (_lock)
        {
            var giocatore = stanza.GiocatoreDi(token);
            if (giocatore == null)
                return EsitoPasso.Fallito("Sessione non valida: rientra dalla stanza.");

            var p = stanza.Partita;
            if (p.Fase != FasePartita.Voto)
                return EsitoPasso.Fallito("Non è il momento del voto.");
            if (stanza.VotoChiuso)
                return EsitoPasso.Fallito("Il voto è già chiuso.");
            if (!giocatore.Vivo)
                return EsitoPasso.Fallito("Sei fuori gioco: non puoi votare.");
            if (stanza.HaVotato(giocatore.Id))
                return EsitoPasso.Fallito("Hai già votato.");

            var bersaglio = p.Giocatore(bersaglioId);
            if (bersaglio == null || !bersaglio.Vivo)
                return EsitoPasso.Fallito("Scegli un giocatore vivo.");
            if (bersaglio.Id == giocatore.Id)
                return EsitoPasso.Fallito("Non puoi votare per te stesso.");

            stanza.RegistraVoto(giocatore.Id, bersaglio.Id);
            stanza.UltimoAggiornamento = DateTime.Now;
        }

        await NotificaAsync(stanza.Codice);
        return EsitoPasso.Successo();
    }

    public async Task<RisultatoChiusura> ChiudiVotoAsync(
        Stanza stanza,
        string hostToken,
        string? sceltaManuale = null)
    {
        if (!ValidaHost(stanza, hostToken))
            return RisultatoChiusura.Fallito("Token narratore non valido.");

        RisultatoChiusura risultato;

        lock (_lock)
        {
            var p = stanza.Partita;
            if (p.Fase != FasePartita.Voto)
                return RisultatoChiusura.Fallito("Non è il momento del voto.");
            if (stanza.VotoChiuso && !stanza.VotoPareggio)
                return RisultatoChiusura.Fallito("Il voto è già chiuso.");

            if (stanza.VotoPareggio)
            {
                var scelto = p.Giocatore(sceltaManuale);
                if (scelto == null || !scelto.Vivo)
                    return RisultatoChiusura.Fallito("Scegli un giocatore vivo per sciogliere il pareggio.");

                stanza.VotoPareggio = false;
                stanza.CandidatiPareggio.Clear();
                risultato = ApplicaVoto(
                    p,
                    stanza,
                    scelto,
                    $"Il narratore ha scelto {scelto.Nome} per sciogliere il pareggio.");
                stanza.UltimoAggiornamento = DateTime.Now;
            }
            else if (!string.IsNullOrWhiteSpace(sceltaManuale))
            {
                var scelto = p.Giocatore(sceltaManuale);
                if (scelto == null || !scelto.Vivo)
                    return RisultatoChiusura.Fallito("Scegli un giocatore vivo.");

                risultato = ApplicaVoto(p, stanza, scelto, $"Il narratore ha scelto {scelto.Nome}.");
                stanza.UltimoAggiornamento = DateTime.Now;
            }
            else
            {
                var voti = stanza.SnapshotVoti();
                if (voti.Count == 0)
                    return RisultatoChiusura.Fallito(
                        "Nessun voto registrato. Puoi forzare il rogo dal pannello del narratore.");

                var gruppi = voti
                    .GroupBy(kv => kv.Value)
                    .Select(g => new { Id = g.Key, Voti = g.Count() })
                    .OrderByDescending(x => x.Voti)
                    .ToList();

                var massimo = gruppi[0].Voti;
                var testa = gruppi
                    .Where(x => x.Voti == massimo)
                    .Select(x => x.Id)
                    .ToList();

                if (testa.Count > 1)
                {
                    stanza.VotoChiuso = true;
                    stanza.VotoPareggio = true;
                    stanza.CandidatiPareggio.Clear();
                    stanza.CandidatiPareggio.AddRange(testa);
                    stanza.EsitoVoto =
                        $"Pareggio tra {string.Join(", ", testa.Select(p.NomeDi))}: il narratore deve scegliere.";
                    stanza.UltimoAggiornamento = DateTime.Now;

                    risultato = RisultatoChiusura.PareggioCon(
                        testa.Select(id => p.Giocatore(id)).OfType<Giocatore>().ToList(),
                        stanza.EsitoVoto);
                }
                else
                {
                    var scelto = p.Giocatore(testa[0])!;
                    risultato = ApplicaVoto(
                        p,
                        stanza,
                        scelto,
                        $"Il villaggio ha scelto {scelto.Nome} ({massimo} voti).");
                    stanza.UltimoAggiornamento = DateTime.Now;
                }
            }
        }

        await NotificaAsync(stanza.Codice);
        return risultato;
    }

    public async Task<EsitoPasso> RegistraVotoBallottaggioAsync(
        Stanza stanza,
        string token,
        string bersaglioId)
    {
        lock (_lock)
        {
            var giocatore = stanza.GiocatoreDi(token);
            if (giocatore == null)
                return EsitoPasso.Fallito("Sessione non valida: rientra dalla stanza.");

            var p = stanza.Partita;
            if (p.Fase != FasePartita.Ballottaggio)
                return EsitoPasso.Fallito("Non è il momento del ballottaggio.");
            if (stanza.BallottaggioChiuso)
                return EsitoPasso.Fallito("Il ballottaggio è già chiuso.");
            if (!giocatore.Vivo)
                return EsitoPasso.Fallito("Sei fuori gioco: non puoi votare.");
            if (stanza.HaVotatoBallottaggio(giocatore.Id))
                return EsitoPasso.Fallito("Hai già votato.");

            if (!p.BallottaggioIds.Contains(bersaglioId))
                return EsitoPasso.Fallito("Scegli uno dei candidati al ballottaggio.");

            var bersaglio = p.Giocatore(bersaglioId);
            if (bersaglio == null || !bersaglio.Vivo)
                return EsitoPasso.Fallito("Il candidato non è più in vita.");
            if (bersaglio.Id == giocatore.Id)
                return EsitoPasso.Fallito("Non puoi votare per te stesso.");

            stanza.RegistraVotoBallottaggio(giocatore.Id, bersaglio.Id);
            stanza.UltimoAggiornamento = DateTime.Now;
        }

        await NotificaAsync(stanza.Codice);
        return EsitoPasso.Successo();
    }

    public async Task<RisultatoChiusura> ChiudiBallottaggioAsync(
        Stanza stanza,
        string hostToken,
        string? sceltaManuale = null)
    {
        if (!ValidaHost(stanza, hostToken))
            return RisultatoChiusura.Fallito("Token narratore non valido.");

        RisultatoChiusura risultato;

        lock (_lock)
        {
            var p = stanza.Partita;
            if (p.Fase != FasePartita.Ballottaggio)
                return RisultatoChiusura.Fallito("Non è il momento del ballottaggio.");
            if (stanza.BallottaggioChiuso && !stanza.BallottaggioPareggio)
                return RisultatoChiusura.Fallito("Il ballottaggio è già chiuso.");

            if (stanza.BallottaggioPareggio)
            {
                var scelto = p.Giocatore(sceltaManuale);
                if (scelto == null || !scelto.Vivo || !p.BallottaggioIds.Contains(scelto.Id))
                    return RisultatoChiusura.Fallito("Scegli uno dei candidati al ballottaggio.");

                stanza.BallottaggioPareggio = false;
                stanza.CandidatiPareggioBallottaggio.Clear();
                risultato = ApplicaBallottaggio(
                    p,
                    stanza,
                    scelto,
                    $"Il narratore ha scelto {scelto.Nome} per sciogliere il pareggio.");
                stanza.UltimoAggiornamento = DateTime.Now;
            }
            else if (!string.IsNullOrWhiteSpace(sceltaManuale))
            {
                var scelto = p.Giocatore(sceltaManuale);
                if (scelto == null || !scelto.Vivo || !p.BallottaggioIds.Contains(scelto.Id))
                    return RisultatoChiusura.Fallito("Scegli uno dei candidati al ballottaggio.");

                risultato = ApplicaBallottaggio(
                    p,
                    stanza,
                    scelto,
                    $"Il narratore ha scelto {scelto.Nome}.");
                stanza.UltimoAggiornamento = DateTime.Now;
            }
            else
            {
                var voti = stanza.SnapshotVotiBallottaggio();
                if (voti.Count == 0)
                    return RisultatoChiusura.Fallito(
                        "Nessun voto registrato. Puoi forzare il rogo dal pannello del narratore.");

                var gruppi = voti
                    .GroupBy(kv => kv.Value)
                    .Select(g => new { Id = g.Key, Voti = g.Count() })
                    .OrderByDescending(x => x.Voti)
                    .ToList();

                var massimo = gruppi[0].Voti;
                var testa = gruppi
                    .Where(x => x.Voti == massimo)
                    .Select(x => x.Id)
                    .ToList();

                if (testa.Count > 1)
                {
                    stanza.BallottaggioChiuso = true;
                    stanza.BallottaggioPareggio = true;
                    stanza.CandidatiPareggioBallottaggio.Clear();
                    stanza.CandidatiPareggioBallottaggio.AddRange(testa);
                    stanza.EsitoBallottaggio =
                        $"Pareggio tra {string.Join(", ", testa.Select(p.NomeDi))}: il narratore deve scegliere.";
                    stanza.UltimoAggiornamento = DateTime.Now;

                    risultato = RisultatoChiusura.PareggioCon(
                        testa.Select(id => p.Giocatore(id)).OfType<Giocatore>().ToList(),
                        stanza.EsitoBallottaggio);
                }
                else
                {
                    var scelto = p.Giocatore(testa[0])!;
                    risultato = ApplicaBallottaggio(
                        p,
                        stanza,
                        scelto,
                        $"Il ballottaggio ha scelto {scelto.Nome} ({massimo} voti).");
                    stanza.UltimoAggiornamento = DateTime.Now;
                }
            }
        }

        await NotificaAsync(stanza.Codice);
        return risultato;
    }

    private static RisultatoChiusura ApplicaVoto(
        Partita p,
        Stanza stanza,
        Giocatore scelto,
        string messaggio)
    {
        var esito = MotoreLupus.Vota(p, scelto.Id);
        if (!esito.Ok)
            return RisultatoChiusura.Fallito(esito.Errore ?? "Voto non riuscito.");

        stanza.VotoChiuso = true;
        stanza.VotoPareggio = false;
        stanza.CandidatiPareggio.Clear();
        stanza.EsitoVoto = messaggio;

        if (p.Fase == FasePartita.Ballottaggio)
        {
            stanza.AzzeraVotiBallottaggio();
            stanza.EsitoVoto =
                $"{messaggio} Ballottaggio con {string.Join(", ", p.BallottaggioIds.Select(p.NomeDi))}.";
        }
        else if (p.Fase is FasePartita.Esecuzione or FasePartita.Finita)
        {
            stanza.EsitoVoto = $"{messaggio} {p.NomeDi(p.UltimoRogoId)} è andato al rogo.";
        }

        return RisultatoChiusura.Successo(stanza.EsitoVoto);
    }

    private static RisultatoChiusura ApplicaBallottaggio(
        Partita p,
        Stanza stanza,
        Giocatore scelto,
        string messaggio)
    {
        var esito = MotoreLupus.ScegliBallottaggio(p, scelto.Id);
        if (!esito.Ok)
            return RisultatoChiusura.Fallito(esito.Errore ?? "Ballottaggio non riuscito.");

        stanza.BallottaggioChiuso = true;
        stanza.BallottaggioPareggio = false;
        stanza.CandidatiPareggioBallottaggio.Clear();
        stanza.EsitoBallottaggio = $"{messaggio} {p.NomeDi(p.UltimoRogoId)} è andato al rogo.";

        return RisultatoChiusura.Successo(stanza.EsitoBallottaggio);
    }

    // ---------------------------------------------------------------------
    // Rivelazioni controllate dal narratore
    // ---------------------------------------------------------------------

    public async Task<EsitoPasso> ImpostaRivelaRuoliMortiAsync(
        Stanza stanza,
        string hostToken,
        bool rivela)
    {
        if (!ValidaHost(stanza, hostToken))
            return EsitoPasso.Fallito("Token narratore non valido.");

        lock (_lock)
        {
            stanza.RivelaRuoliMorti = rivela;
            stanza.UltimoAggiornamento = DateTime.Now;
        }

        await NotificaAsync(stanza.Codice);
        return EsitoPasso.Successo();
    }

    public async Task<EsitoPasso> ImpostaRivelaRuoloRogoAsync(
        Stanza stanza,
        string hostToken,
        bool rivela)
    {
        if (!ValidaHost(stanza, hostToken))
            return EsitoPasso.Fallito("Token narratore non valido.");

        lock (_lock)
        {
            stanza.RivelaRuoloRogo = rivela;
            stanza.UltimoAggiornamento = DateTime.Now;
        }

        await NotificaAsync(stanza.Codice);
        return EsitoPasso.Successo();
    }

    // ---------------------------------------------------------------------
    // Chiusura stanza
    // ---------------------------------------------------------------------

    public async Task<EsitoPasso> AbbandonaAsync(Stanza stanza, string hostToken)
    {
        if (!ValidaHost(stanza, hostToken))
            return EsitoPasso.Fallito("Token narratore non valido.");

        List<Func<Task>>? callbacks = null;

        lock (_lock)
        {
            _stanze.Remove(stanza.Codice);
            if (_sottoscrittori.TryGetValue(stanza.Codice, out var lista))
            {
                callbacks = lista.ToList();
                _sottoscrittori.Remove(stanza.Codice);
            }
        }

        if (callbacks != null)
            await EseguiCallbacksAsync(callbacks);

        return EsitoPasso.Successo();
    }

    // ---------------------------------------------------------------------
    // Sottoscrizioni dei circuiti Blazor
    // ---------------------------------------------------------------------

    public void Sottoscrivi(string codice, Func<Task> callback)
    {
        if (string.IsNullOrWhiteSpace(codice)) return;

        lock (_lock)
        {
            if (!_sottoscrittori.TryGetValue(codice, out var lista))
            {
                lista = new List<Func<Task>>();
                _sottoscrittori[codice] = lista;
            }

            if (!lista.Contains(callback))
                lista.Add(callback);
        }
    }

    public void AnnullaSottoscrizione(string codice, Func<Task> callback)
    {
        if (string.IsNullOrWhiteSpace(codice)) return;

        lock (_lock)
        {
            if (_sottoscrittori.TryGetValue(codice, out var lista))
                lista.Remove(callback);
        }
    }

    private async Task NotificaAsync(string codice)
    {
        Func<Task>[] callbacks;

        lock (_lock)
        {
            if (!_sottoscrittori.TryGetValue(codice, out var lista) || lista.Count == 0)
                return;

            callbacks = lista.ToArray();
        }

        await EseguiCallbacksAsync(callbacks);
    }

    private static async Task EseguiCallbacksAsync(IEnumerable<Func<Task>> callbacks)
    {
        var tasks = callbacks.Select(async callback =>
        {
            try
            {
                await callback();
            }
            catch
            {
                // Un telefono può essersi disconnesso: gli altri devono
                // comunque ricevere l'aggiornamento.
            }
        });

        await Task.WhenAll(tasks);
    }

    // ---------------------------------------------------------------------
    // Pulizia e codici
    // ---------------------------------------------------------------------

    private Stanza? Trova(string? codice)
    {
        if (string.IsNullOrWhiteSpace(codice)) return null;
        return _stanze.TryGetValue(codice.Trim(), out var stanza) ? stanza : null;
    }

    private void Pulizia()
    {
        var limite = DateTime.Now.AddHours(-24);
        var daRimuovere = _stanze
            .Where(kv => kv.Value.UltimoAggiornamento < limite)
            .Select(kv => kv.Key)
            .ToList();

        foreach (var codice in daRimuovere)
        {
            _stanze.Remove(codice);
            _sottoscrittori.Remove(codice);
        }
    }

    private static string GeneraCodice()
    {
        Span<char> buffer = stackalloc char[5];
        for (int i = 0; i < buffer.Length; i++)
            buffer[i] = Alfabeto[Random.Shared.Next(Alfabeto.Length)];

        return new string(buffer);
    }
}

public sealed record PostoStanza(string GiocatoreId, string Nome, string Iniziali, bool Occupato);

public sealed class RisultatoChiusura
{
    public bool Ok { get; init; }
    public string? Errore { get; init; }
    public bool Pareggio { get; init; }
    public IReadOnlyList<Giocatore> Candidati { get; init; } = Array.Empty<Giocatore>();
    public string? Messaggio { get; init; }

    public static RisultatoChiusura Successo(string? messaggio = null) =>
        new() { Ok = true, Messaggio = messaggio };

    public static RisultatoChiusura Fallito(string errore) =>
        new() { Ok = false, Errore = errore };

    public static RisultatoChiusura PareggioCon(
        IReadOnlyList<Giocatore> candidati,
        string? messaggio) =>
        new() { Ok = true, Pareggio = true, Candidati = candidati, Messaggio = messaggio };
}
