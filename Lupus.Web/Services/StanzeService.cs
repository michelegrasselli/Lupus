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
        bool assegnaCasualmente = true,
        IReadOnlyCollection<int>? postiAi = null)
    {
        var partita = CreazionePartita.Crea(nomi, composizione, assegnaCasualmente);
        return CreaDaPartita(partita, avviata: false, postiAi);
    }

    /// <summary>
    /// Crea una stanza da un elenco esplicito di giocatori con il proprio
    /// ruolo, come li aggiunge il narratore dalla home.
    /// </summary>
    public (Stanza Stanza, string HostToken) CreaDaRuoli(
        IReadOnlyList<(string Nome, Ruolo Ruolo)> giocatori)
    {
        var partita = CreazionePartita.CreaDaRuoli(giocatori);
        return CreaDaPartita(partita, avviata: false);
    }

    /// <summary>
    /// Crea una stanza da una partita già esistente (import JSON o ripresa).
    /// <paramref name="postiAi"/> elenca gli indici dei giocatori (nell'ordine
    /// della partita) che devono essere giocati dal server.
    /// </summary>
    public (Stanza Stanza, string HostToken) CreaDaPartita(
        Partita partita,
        bool avviata,
        IReadOnlyCollection<int>? postiAi = null)
    {
        partita.Normalizza();

        // Un salvataggio importato potrebbe non avere il capobranco (o averlo
        // perso): il branco ne sceglie uno a caso tra i Lupi vivi.
        if (partita.Capobranco == null && partita.LupiVivi.Any())
            MotoreLupus.AssegnaCapobrancoCasuale(partita);

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

            var indice = 0;
            foreach (var giocatore in partita.Giocatori)
            {
                var ai = postiAi?.Contains(indice) == true;

                // Un posto AI nasce già occupato: nessun telefono deve
                // reclamarlo, e il server usa il token per agire al suo posto.
                stanza.Posti.Add(new Posto
                {
                    GiocatoreId = giocatore.Id,
                    IsAi = ai,
                    Token = ai ? Guid.NewGuid().ToString("N") : null,
                    UltimoAccesso = ai ? DateTime.Now : null
                });

                indice++;
            }

            _stanze[codice] = stanza;
            _sottoscrittori[codice] = new List<Func<Task>>();

            return (stanza, stanza.HostToken);
        }
    }

    /// <summary>
    /// Copia delle stanze attive, usata dal giro di gioco dei giocatori AI.
    /// Il lock è preso solo per copiare l'elenco: chi consuma la lista lavora
    /// poi sulle singole stanze tramite i metodi pubblici del servizio.
    /// </summary>
    public IReadOnlyList<Stanza> Stanze()
    {
        lock (_lock)
        {
            Pulizia();
            return _stanze.Values.ToList();
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
                        posto.Occupato,
                        posto.IsAi);
                })
                .ToList();
        }
    }

    /// <summary>
    /// Posti affidati al server, con il token che il giro degli AI usa per
    /// agire esattamente come farebbe un telefono.
    /// </summary>
    public IReadOnlyList<(string Token, string GiocatoreId)> PostiAi(Stanza stanza)
    {
        lock (_lock)
        {
            return stanza.Posti
                .Where(p => p.IsAi && p.Occupato)
                .Select(p => (p.Token!, p.GiocatoreId))
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
            posto.IsAi = false;
            stanza.UltimoAggiornamento = DateTime.Now;
        }

        await NotificaAsync(stanza.Codice);
        return EsitoPasso.Successo();
    }

    /// <summary>
    /// Affida un posto ancora libero a un giocatore AI: la stanza lo tratta
    /// come occupato e il server gioca al suo posto. Si può fare solo in lobby,
    /// prima dell'avvio: dopo i ruoli sono già stati distribuiti.
    /// </summary>
    public async Task<EsitoPasso> RiempiConAiAsync(Stanza stanza, string hostToken, string giocatoreId)
    {
        if (!ValidaHost(stanza, hostToken))
            return EsitoPasso.Fallito("Token narratore non valido.");

        lock (_lock)
        {
            if (stanza.Avviata)
                return EsitoPasso.Fallito("Aggiungi i giocatori AI prima di avviare la partita.");

            var posto = stanza.PostoDi(giocatoreId);
            if (posto == null)
                return EsitoPasso.Fallito("Posto non trovato.");
            if (posto.Occupato)
                return EsitoPasso.Fallito("Questo posto è già occupato.");

            posto.IsAi = true;
            posto.Token = Guid.NewGuid().ToString("N");
            posto.UltimoAccesso = DateTime.Now;

            stanza.Partita.AggiungiLog(
                $"Il posto di {stanza.Partita.NomeDi(giocatoreId)} è ora un giocatore AI.",
                TipoLog.Configurazione);
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
    // Chat della piazza (fase Giorno)
    // ---------------------------------------------------------------------

    /// <summary>
    /// Copia dei messaggi della piazza per il narratore. La chat è pubblica:
    /// non contiene ruoli né informazioni riservate.
    /// </summary>
    public IReadOnlyList<MessaggioChat> ChatPubblica(Stanza stanza)
    {
        lock (_lock)
        {
            return stanza.SnapshotChat();
        }
    }

    /// <summary>Vero se in stanza c'è almeno un giocatore AI ancora vivo.</summary>
    public bool HaAiVivi(Stanza stanza)
    {
        lock (_lock)
        {
            var p = stanza.Partita;
            return stanza.Posti.Any(x => x.IsAi && p.Giocatore(x.GiocatoreId)?.Vivo == true);
        }
    }

    /// <summary>
    /// Un giocatore vivo scrive nella piazza. Con <paramref name="destinatarioId"/>
    /// il messaggio è una domanda rivolta a un giocatore AI; con
    /// <paramref name="rispostaA"/> un AI risponde (o rifiuta di rispondere)
    /// a una domanda aperta.
    /// </summary>
    public async Task<EsitoPasso> InviaMessaggioChatAsync(
        Stanza stanza,
        string? token,
        string testo,
        string? destinatarioId = null,
        string? rispostaA = null)
    {
        var pulito = (testo ?? "").Trim();
        if (pulito.Length == 0)
            return EsitoPasso.Fallito("Scrivi qualcosa prima di parlare.");
        if (pulito.Length > 280)
            pulito = pulito[..280];

        lock (_lock)
        {
            var giocatore = stanza.GiocatoreDi(token);
            if (giocatore == null)
                return EsitoPasso.Fallito("Sessione non valida: rientra dalla stanza.");

            var p = stanza.Partita;
            if (p.Fase != FasePartita.Giorno)
                return EsitoPasso.Fallito("La piazza parla solo durante la discussione del giorno.");
            if (!giocatore.Vivo)
                return EsitoPasso.Fallito("Sei fuori gioco: non puoi parlare nella piazza.");

            var aiVivo = stanza.Posti.Any(x => x.IsAi && p.Giocatore(x.GiocatoreId)?.Vivo == true);
            if (!aiVivo)
                return EsitoPasso.Fallito("Non ci sono giocatori AI in vita: la piazza resta chiusa.");

            var posto = stanza.PostoDi(giocatore.Id);

            if (!string.IsNullOrWhiteSpace(rispostaA))
            {
                if (posto?.IsAi != true)
                    return EsitoPasso.Fallito("Solo un giocatore AI può rispondere a una domanda.");

                var domanda = stanza.MessaggioChatDi(rispostaA);
                if (domanda == null || !domanda.Domanda)
                    return EsitoPasso.Fallito("La domanda non esiste più.");
                if (domanda.AutoreId == giocatore.Id)
                    return EsitoPasso.Fallito("Non puoi rispondere a te stesso.");
                if (domanda.DestinatarioId != null && domanda.DestinatarioId != giocatore.Id)
                    return EsitoPasso.Fallito("La domanda è rivolta a un altro giocatore.");
                if (stanza.DomandaGiaGestita(domanda.Id))
                    return EsitoPasso.Fallito("Qualcuno ha già risposto a questa domanda.");
            }

            string? destinatarioNome = null;
            if (!string.IsNullOrWhiteSpace(destinatarioId))
            {
                var bersaglio = p.Giocatore(destinatarioId);
                if (bersaglio == null || !bersaglio.Vivo)
                    return EsitoPasso.Fallito("Puoi fare domande solo a un giocatore vivo.");
                if (bersaglio.Id == giocatore.Id)
                    return EsitoPasso.Fallito("Non puoi fare domande a te stesso.");

                var postoBersaglio = stanza.PostoDi(bersaglio.Id);
                if (postoBersaglio?.IsAi != true)
                    return EsitoPasso.Fallito("Puoi fare domande solo ai giocatori AI.");

                destinatarioNome = bersaglio.Nome;
            }

            var domandaPubblica = rispostaA == null
                && destinatarioId == null
                && pulito.Contains('?');

            stanza.AggiungiMessaggioChat(
                giocatore.Id,
                giocatore.Nome,
                posto?.IsAi == true,
                pulito,
                p.Turno,
                string.IsNullOrWhiteSpace(destinatarioId) ? null : destinatarioId,
                rispostaA == null && (destinatarioNome != null || domandaPubblica),
                string.IsNullOrWhiteSpace(rispostaA) ? null : rispostaA);

            stanza.UltimoAggiornamento = DateTime.Now;
        }

        await NotificaAsync(stanza.Codice);
        return EsitoPasso.Successo();
    }

    /// <summary>Domande che un AI può ancora gestire in questo turno.</summary>
    public IReadOnlyList<MessaggioChat> DomandeApertePerAi(Stanza stanza, string giocatoreId, int turno)
    {
        lock (_lock)
        {
            return stanza.DomandeApertePerAi(giocatoreId, turno);
        }
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
            if (stanza.VotoChiuso)
                return RisultatoChiusura.Fallito("Il voto è già chiuso.");

            if (!string.IsNullOrWhiteSpace(sceltaManuale))
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
                        "Nessun voto registrato. Puoi forzare il rogo o saltarlo dal pannello del narratore.");

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
                    // Pareggio: seconda votazione solo tra i più votati, più
                    // gli eventuali candidati del Gufo che restano in gara.
                    var candidati = testa
                        .Concat(p.BallottaggioIds.Where(id => p.Giocatore(id)?.Vivo == true))
                        .Distinct()
                        .ToList();

                    var esito = MotoreLupus.ApriBallottaggio(p, candidati);
                    if (!esito.Ok)
                        return RisultatoChiusura.Fallito(esito.Errore ?? "Ballottaggio non riuscito.");

                    stanza.VotoChiuso = true;
                    stanza.AzzeraVotiBallottaggio();
                    stanza.BallottaggioDaPareggio = true;
                    stanza.EsitoVoto =
                        $"Pareggio tra {string.Join(", ", testa.Select(p.NomeDi))}: secondo voto tra "
                        + $"{string.Join(", ", p.BallottaggioIds.Select(p.NomeDi))}.";
                    stanza.UltimoAggiornamento = DateTime.Now;

                    risultato = RisultatoChiusura.Successo(stanza.EsitoVoto);
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

    /// <summary>
    /// Il narratore salta il rogo di questo turno: nessuno muore e si passa
    /// all'esecuzione vuota, da cui si cala la notte.
    /// </summary>
    public async Task<EsitoPasso> SaltaRogoAsync(Stanza stanza, string hostToken)
    {
        if (!ValidaHost(stanza, hostToken))
            return EsitoPasso.Fallito("Token narratore non valido.");

        EsitoPasso risultato;

        lock (_lock)
        {
            var p = stanza.Partita;
            var daVoto = p.Fase == FasePartita.Voto;
            var daBallottaggio = p.Fase == FasePartita.Ballottaggio;

            risultato = MotoreLupus.SaltaRogo(p);
            if (!risultato.Ok) return risultato;

            const string messaggio = "Nessuno va al rogo: il narratore ha saltato il voto.";

            if (daVoto)
            {
                stanza.VotoChiuso = true;
                stanza.EsitoVoto = messaggio;
            }

            if (daBallottaggio)
            {
                stanza.BallottaggioChiuso = true;
                stanza.EsitoBallottaggio = messaggio;
            }

            stanza.UltimoAggiornamento = DateTime.Now;
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
            if (stanza.BallottaggioChiuso)
                return RisultatoChiusura.Fallito("Il ballottaggio è già chiuso.");

            if (!string.IsNullOrWhiteSpace(sceltaManuale))
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
                        "Nessun voto registrato. Puoi forzare il rogo o saltarlo dal pannello del narratore.");

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
                    // Anche il secondo voto è in parità: nessuno va al rogo.
                    var esito = MotoreLupus.SaltaRogo(p);
                    if (!esito.Ok)
                        return RisultatoChiusura.Fallito(esito.Errore ?? "Rogo non saltato.");

                    stanza.BallottaggioChiuso = true;
                    stanza.EsitoBallottaggio =
                        $"Pareggio tra {string.Join(", ", testa.Select(p.NomeDi))}: nessuno va al rogo stanotte.";
                    stanza.UltimoAggiornamento = DateTime.Now;

                    risultato = RisultatoChiusura.Successo(stanza.EsitoBallottaggio);
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

public sealed record PostoStanza(string GiocatoreId, string Nome, string Iniziali, bool Occupato, bool Ai = false);

public sealed class RisultatoChiusura
{
    public bool Ok { get; init; }
    public string? Errore { get; init; }
    public string? Messaggio { get; init; }

    public static RisultatoChiusura Successo(string? messaggio = null) =>
        new() { Ok = true, Messaggio = messaggio };

    public static RisultatoChiusura Fallito(string errore) =>
        new() { Ok = false, Errore = errore };
}
