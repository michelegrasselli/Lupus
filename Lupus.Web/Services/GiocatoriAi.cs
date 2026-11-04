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

                case FasePartita.Giorno:
                    await GiocaGiornoAsync(stanza, token, giocatoreId, stato);
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
    /// sospetti delle indagini, poi — se è un lupo — di solito qualcuno fuori
    /// dal branco, altrimenti un vivo a caso. Il voto resta segreto come per
    /// gli umani.
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

        // Il branco di solito risparmia i compagni, ma non sempre: se il lupo
        // non votasse mai un altro lupo, chi guarda il tally capirebbe chi è
        // nel branco. Ogni tanto un compagno viene sacrificato per sembrare
        // puliti, quindi i lupi restano candidati come tutti.
        if (stato.Ruolo == Ruolo.Lupo && Random.Shared.NextDouble() > 0.25)
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
    // Giorno: la piazza e la chat
    // ---------------------------------------------------------------------

    private static readonly string[] CommentiAccusa =
    {
        "C'è qualcosa in {0} che non mi torna. Stanotte dormiva troppo tranquillo.",
        "Io una parola con {0} la farei prima di votare.",
        "{0} parla poco e guarda troppo. A me non piace.",
        "Se devo fare un nome, dico {0}: ieri sera girava troppo tardi.",
        "{0} mi sembra più preoccupato degli altri. Chiedetegli dov'era stanotte.",
        "Non mi fiderei di {0}. Ha risposte troppo pronte."
    };

    private static readonly string[] CommentiDifesa =
    {
        "Lo dico chiaro: io sono dalla parte del villaggio.",
        "Prima di puntare il dito, guardate chi tace.",
        "Non ho niente da nascondere. Votate con la testa.",
        "Se cercate un colpevole, non guardate me: ho dormito e basta."
    };

    private static readonly string[] CommentiGenerici =
    {
        "Qualcuno stanotte ha dormito male. E non sono io.",
        "Il villaggio ha perso gente buona. Impariamo a guardarci in faccia.",
        "Io aspetto il voto, ma non prometto che sarò gentile.",
        "C'è troppa calma stamattina. Non mi piace."
    };

    private static readonly string[] CommentiBluff =
    {
        "Stanotte ho sentito dei passi fuori casa. Qualcuno non dormiva.",
        "Non fidatevi delle facce tranquille: qualcuno qui mente.",
        "Ho visto un'ombra muoversi verso il bosco. Non so chi fosse, ma non era un animale.",
        "Ho dormito poco e ho sentito una porta cigolare. Tenete gli occhi aperti."
    };

    private static readonly string[] Rifiuti =
    {
        "Preferisco non espormi per ora.",
        "Non è il momento di dire tutto quello che so.",
        "Me lo tengo per me: ho le mie ragioni.",
        "Risponderei, ma non voglio regalare informazioni a chi non le merita.",
        "Ho un sospetto, ma non è ancora il momento di farlo uscire."
    };

    private static readonly string[] ParoleNegative =
    {
        "lupo", "lupi", "cattivo", "cattiva", "colpevole", "sospetto", "sospetta",
        "bugiardo", "bugiarda", "assassino", "non fidarti", "non mi fido",
        "sbranato", "pericoloso", "pericolosa", "mente"
    };

    private static readonly string[] ParolePositive =
    {
        "buono", "buona", "innocente", "pulito", "pulita", "fidati", "sicuro", "sicura",
        "contadino", "contadina", "onesto", "onesta"
    };

    private async Task GiocaGiornoAsync(
        Stanza stanza,
        string token,
        string giocatoreId,
        StatoGiocatore stato)
    {
        var memoria = Memoria(stanza, giocatoreId);

        if (memoria.TurnoChat != stato.Turno)
        {
            memoria.TurnoChat = stato.Turno;
            memoria.CommentiChat = 0;
            memoria.RisposteChat = 0;
            memoria.ReazioniChat = 0;
        }

        // 1. Legge le informazioni raccontate dagli umani e decide se crederci.
        ValutaMessaggiUmani(stato, memoria);

        // 2. Risponde a una domanda aperta (diretta o pubblica).
        if (memoria.RisposteChat < 4)
        {
            var domande = _stanze.DomandeApertePerAi(stanza, giocatoreId, stato.Turno);
            foreach (var domanda in domande)
            {
                var chiaveDomanda = $"{stanza.Codice}|{giocatoreId}|chat|{domanda.Id}";
                if (!AttesaScaduta(chiaveDomanda, 2200, 6500))
                    return;

                var testo = ComponiRisposta(stato, memoria, domanda);
                var esito = await _stanze.InviaMessaggioChatAsync(
                    stanza, token, testo, rispostaA: domanda.Id);

                if (esito.Ok)
                    memoria.RisposteChat++;

                // Una risposta per giro: le altre domande restano per dopo.
                return;
            }
        }

        // 3. Reagisce a un'informazione appena letta: si fida o dubita.
        var daReagire = memoria.Valutazioni
            .Where(kv => !kv.Value.ReazioneGestita && kv.Value.Turno == stato.Turno)
            .Select(kv => kv.Key)
            .FirstOrDefault();

        if (daReagire != null && memoria.ReazioniChat < 3)
        {
            var chiaveReazione = $"{stanza.Codice}|{giocatoreId}|reazione|{daReagire}";
            if (!AttesaScaduta(chiaveReazione, 3000, 9000))
                return;

            var valutazione = memoria.Valutazioni[daReagire];
            valutazione.ReazioneGestita = true;

            // A volte l'AI ascolta e tace: non deve sembrare un registratore.
            // Ma se l'accusa è contro di lui, si difende sempre.
            if (valutazione.ControDiSe || Random.Shared.NextDouble() < 0.6)
            {
                var esito = await _stanze.InviaMessaggioChatAsync(
                    stanza, token, ComponiReazione(stato, valutazione));

                if (esito.Ok)
                    memoria.ReazioniChat++;
            }

            return;
        }

        // 4. Commento spontaneo, al massimo due per turno.
        if (memoria.CommentiChat >= 2)
            return;

        var chiaveCommento = $"{stanza.Codice}|{giocatoreId}|{stato.Turno}|chatcom|{memoria.CommentiChat}";
        var minimo = memoria.CommentiChat == 0 ? 5000 : 14000;
        var massimo = memoria.CommentiChat == 0 ? 11000 : 26000;
        if (!AttesaScaduta(chiaveCommento, minimo, massimo))
            return;

        var commento = ComponiCommento(stato, memoria);
        if (commento == null)
            return;

        var risultato = await _stanze.InviaMessaggioChatAsync(stanza, token, commento);
        if (risultato.Ok)
            memoria.CommentiChat++;
    }

    /// <summary>
    /// Legge i messaggi pubblici degli umani: se citano un giocatore vivo con
    /// un'accusa o una difesa, l'AI decide se fidarsi. La decisione aggiorna i
    /// sospetti e viene usata per il voto; il messaggio può essere un bluff.
    /// </summary>
    private void ValutaMessaggiUmani(StatoGiocatore stato, MemoriaAi memoria)
    {
        foreach (var messaggio in stato.Chat)
        {
            if (messaggio.AutoreAi || messaggio.Domanda)
                continue;
            if (!memoria.MessaggiValutati.Add(messaggio.Id))
                continue;

            // Un'accusa può riguardare anche l'AI stesso: deve poter dubitare di
            // chi lo accusa, quindi si cercano i nomi tra tutti i vivi.
            var candidati = stato.Vivi.Count > 0 ? stato.Vivi : AltriVivi(stato);
            if (candidati.Count == 0)
                continue;

            // Il primo nome citato nel testo è il soggetto dell'informazione.
            GiocatorePubblico? citato = null;
            var posizione = int.MaxValue;

            foreach (var g in candidati)
            {
                var i = messaggio.Testo.IndexOf(g.Nome, StringComparison.OrdinalIgnoreCase);
                if (i >= 0 && i < posizione)
                {
                    posizione = i;
                    citato = g;
                }
            }

            if (citato == null)
                continue;

            var negativo = ContieneParola(messaggio.Testo, ParoleNegative);
            var positivo = !negativo && ContieneParola(messaggio.Testo, ParolePositive);
            if (!negativo && !positivo)
                continue;

            var fidato = DecideFiducia(stato, memoria, messaggio, citato, negativo);

            if (fidato)
            {
                // Nessuno mette se stesso tra i sospetti: l'accusa contro di sé
                // serve solo a non fidarsi di chi la lancia.
                if (citato.Id != stato.GiocatoreId)
                {
                    if (negativo)
                        memoria.Sospetti.Add(citato.Id);
                    else
                        memoria.Sospetti.Remove(citato.Id);
                }

                memoria.AutoriFidati.Add(messaggio.AutoreId);
                memoria.AutoriDiffidati.Remove(messaggio.AutoreId);
            }
            else
            {
                memoria.AutoriDiffidati.Add(messaggio.AutoreId);
                memoria.AutoriFidati.Remove(messaggio.AutoreId);
            }

            memoria.Valutazioni[messaggio.Id] = new ValutazioneMessaggio
            {
                Fidato = fidato,
                Negativo = negativo,
                Nome = citato.Nome,
                AutoreNome = messaggio.AutoreNome,
                Turno = messaggio.Turno,
                ControDiSe = citato.Id == stato.GiocatoreId
            };
        }

        if (memoria.Valutazioni.Count > 120)
        {
            foreach (var vecchio in memoria.Valutazioni.Keys
                         .Take(memoria.Valutazioni.Count - 120)
                         .ToList())
            {
                memoria.Valutazioni.Remove(vecchio);
            }
        }
    }

    private static bool DecideFiducia(
        StatoGiocatore stato,
        MemoriaAi memoria,
        MessaggioChat messaggio,
        GiocatorePubblico citato,
        bool negativo)
    {
        // Se l'accusa è rivolta a lui, l'AI non si fida di chi la lancia:
        // vale anche per il lupo, che non si autodenuncia certo in piazza.
        if (citato.Id == stato.GiocatoreId && negativo)
            return false;

        // Il lupo non crede alle accuse contro i compagni, ma sfrutta volentieri
        // quelle contro il villaggio: è un bluff dentro le regole.
        if (stato.Ruolo == Ruolo.Lupo)
        {
            var compagno = stato.CompagniBranco.Contains(citato.Nome);
            return negativo && !compagno;
        }

        var probabilita = 0.58;
        if (memoria.AutoriFidati.Contains(messaggio.AutoreId)) probabilita += 0.22;
        if (memoria.AutoriDiffidati.Contains(messaggio.AutoreId)) probabilita -= 0.22;
        if (memoria.Sospetti.Contains(messaggio.AutoreId)) probabilita -= 0.25;

        return Random.Shared.NextDouble() < Math.Clamp(probabilita, 0.1, 0.9);
    }

    private string? ComponiCommento(StatoGiocatore stato, MemoriaAi memoria)
    {
        var altri = AltriVivi(stato);
        if (altri.Count == 0)
            return null;

        if (stato.Ruolo == Ruolo.Lupo)
        {
            var fuoriBranco = altri
                .Where(g => !stato.CompagniBranco.Contains(g.Nome))
                .ToList();
            var compagni = altri
                .Where(g => stato.CompagniBranco.Contains(g.Nome))
                .ToList();

            // Ogni tanto il branco difende un compagno senza scoprirsi.
            if (compagni.Count > 0 && Random.Shared.NextDouble() < 0.3)
            {
                return $"Contro {Scegli(compagni).Nome} non ho nulla. Se volete un nome, ne ho un altro.";
            }

            // Ma a volte attacca un compagno: se difendesse sempre gli stessi
            // nomi, il villaggio capirebbe chi sono i lupi.
            if (compagni.Count > 0 && Random.Shared.NextDouble() < 0.15)
            {
                return string.Format(Scegli(CommentiAccusa), Scegli(compagni).Nome);
            }

            if (fuoriBranco.Count > 0)
            {
                var sospetti = fuoriBranco
                    .Where(g => memoria.Sospetti.Contains(g.Id))
                    .ToList();
                var bersaglio = sospetti.Count > 0 && Random.Shared.NextDouble() < 0.7
                    ? Scegli(sospetti)
                    : Scegli(fuoriBranco);

                return string.Format(Scegli(CommentiAccusa), bersaglio.Nome);
            }
        }

        var mieiSospetti = altri.Where(g => memoria.Sospetti.Contains(g.Id)).ToList();
        if (mieiSospetti.Count > 0 && Random.Shared.NextDouble() < 0.75)
        {
            var nome = Scegli(mieiSospetti).Nome;

            if (stato.Ruolo is Ruolo.Veggente or Ruolo.Becchino)
                return $"Ho i miei motivi per non fidarmi di {nome}. Non ve li dico tutti.";

            return string.Format(Scegli(CommentiAccusa), nome);
        }

        // Anche un buono può raccontare una balla per muovere il villaggio.
        if (Random.Shared.NextDouble() < 0.18)
            return Scegli(CommentiBluff);

        if (Random.Shared.NextDouble() < 0.4)
            return Scegli(CommentiDifesa);

        return Scegli(CommentiGenerici);
    }

    private string ComponiRisposta(StatoGiocatore stato, MemoriaAi memoria, MessaggioChat domanda)
    {
        // Una domanda pubblica non ha un destinatario: l'AI si presenta.
        var prefisso = domanda.DestinatarioId == null
            ? "Rispondo io: "
            : $"A {domanda.AutoreNome}: ";

        // Ogni tanto la risposta è un rifiuto motivato: è una scelta di gioco.
        if (Random.Shared.NextDouble() < 0.3)
            return prefisso + Scegli(Rifiuti);

        var altri = AltriVivi(stato);

        if (stato.Ruolo == Ruolo.Lupo)
        {
            // Il lupo mente: dichiara di aver dormito o sposta i sospetti.
            if (Random.Shared.NextDouble() < 0.45)
                return prefisso + "Ho dormito tutta la notte, come un sasso. Non ho visto niente.";

            var fuoriBranco = altri
                .Where(g => !stato.CompagniBranco.Contains(g.Nome))
                .ToList();

            if (fuoriBranco.Count > 0)
            {
                var sospetti = fuoriBranco
                    .Where(g => memoria.Sospetti.Contains(g.Id))
                    .ToList();
                var nome = Scegli(sospetti.Count > 0 ? sospetti : fuoriBranco).Nome;
                return prefisso + $"Guarderei bene {nome}. Non dico che sia colpevole, ma non mi fido.";
            }

            return prefisso + "Io sto dalla parte del villaggio. Fidati di chi ti parla chiaro.";
        }

        var mieiSospetti = altri.Where(g => memoria.Sospetti.Contains(g.Id)).ToList();
        if (mieiSospetti.Count > 0)
        {
            var nome = Scegli(mieiSospetti).Nome;
            return prefisso + $"Ho i miei sospetti su {nome}. Non ho prove, ma state in guardia.";
        }

        return prefisso + "Non ho visto niente di strano. Se sapessi qualcosa, lo direi.";
    }

    private string ComponiReazione(
        StatoGiocatore stato,
        ValutazioneMessaggio valutazione)
    {
        if (stato.Ruolo == Ruolo.Lupo)
        {
            // Il branco di solito difende i compagni, ma non sempre: una difesa
            // automatica a ogni accusa sarebbe una firma da lupo.
            if (stato.CompagniBranco.Contains(valutazione.Nome))
            {
                return Random.Shared.NextDouble() < 0.55
                    ? $"Guarda che {valutazione.Nome} non mi sembra il tipo, {valutazione.AutoreNome}."
                    : $"Su {valutazione.Nome} non ho elementi né per difenderlo né per condannarlo.";
            }

            if (valutazione.Negativo)
                return $"Ottimo punto, {valutazione.AutoreNome}. {valutazione.Nome} non mi è mai piaciuto.";

            return $"Difendere {valutazione.Nome} non lo rende innocente, {valutazione.AutoreNome}.";
        }

        if (valutazione.Fidato)
        {
            return valutazione.Negativo
                ? $"Se {valutazione.AutoreNome} dice che {valutazione.Nome} non è pulito, me lo segno. Occhio."
                : $"Va bene, {valutazione.AutoreNome}: per ora mi fido di {valutazione.Nome}.";
        }

        return valutazione.Negativo
            ? $"Puoi anche dirlo, {valutazione.AutoreNome}, ma una parola non è una prova."
            : $"Che tu difenda {valutazione.Nome} non lo rende innocente, {valutazione.AutoreNome}.";
    }

    private static List<GiocatorePubblico> AltriVivi(StatoGiocatore stato) =>
        stato.Vivi.Where(g => g.Id != stato.GiocatoreId).ToList();

    private static bool ContieneParola(string testo, IReadOnlyList<string> parole) =>
        parole.Any(p => testo.Contains(p, StringComparison.OrdinalIgnoreCase));

    private static T Scegli<T>(IReadOnlyList<T> lista) =>
        lista[Random.Shared.Next(lista.Count)];

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
