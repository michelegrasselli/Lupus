using Lupus.Core;

namespace Lupus.Web.Services;

/// <summary>
/// Tutto ciò che un giocatore può sapere in un dato momento. Non contiene
/// mai i ruoli degli altri giocatori vivi: quelli restano al narratore.
/// </summary>
public sealed class StatoGiocatore
{
    public string Codice { get; init; } = "";
    public string GiocatoreId { get; init; } = "";
    public string Nome { get; init; } = "";
    public bool Avviata { get; init; }
    public bool Vivo { get; init; }

    public Ruolo? Ruolo { get; init; }
    public RuoloInfo? Info { get; init; }
    public bool Capobranco { get; init; }
    public bool Trasformato { get; init; }
    public Ruolo? RuoloIniziale { get; init; }

    public FasePartita Fase { get; init; }
    public int Turno { get; init; }
    public EsitoPartita? Esito { get; init; }
    public bool Finita { get; init; }
    public bool InLobby => !Avviata;

    public IReadOnlyList<GiocatorePubblico> Giocatori { get; init; } = Array.Empty<GiocatorePubblico>();
    public IReadOnlyList<GiocatorePubblico> Vivi { get; init; } = Array.Empty<GiocatorePubblico>();
    public IReadOnlyList<GiocatorePubblico> MortiNotte { get; init; } = Array.Empty<GiocatorePubblico>();
    public IReadOnlyList<GiocatorePubblico> CandidatiGufo { get; init; } = Array.Empty<GiocatorePubblico>();
    public IReadOnlyList<string> CompagniBranco { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Per un Copione trasformato: il nome di chi gli ha prestato il ruolo.
    /// Lo sa sempre, dal momento della copia, come i lupi conoscono il branco.
    /// </summary>
    public string? CopiaDaNome { get; init; }

    /// <summary>
    /// Per il copiato: i Copioni che hanno preso il suo ruolo. La lista resta
    /// vuota il primo giorno e compare dalla notte successiva, quando i due
    /// si svegliano insieme.
    /// </summary>
    public IReadOnlyList<string> CopiatoDaNomi { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Il ruolo attuale agisce di notte: serve a scrivere "vi sveglierete
    /// insieme" solo quando è vero (un Copione che copia un Contadino non ha
    /// nessun passo notturno da condividere).
    /// </summary>
    public bool RuoloAgisceDiNotte { get; init; }

    public IReadOnlyList<AzioneGiocatore> Azioni { get; init; } = Array.Empty<AzioneGiocatore>();
    public IReadOnlyList<string> EventiAlba { get; init; } = Array.Empty<string>();

    /// <summary>Messaggi della piazza del villaggio (solo fase Giorno).</summary>
    public IReadOnlyList<MessaggioChat> Chat { get; init; } = Array.Empty<MessaggioChat>();

    /// <summary>Giocatori AI ancora vivi: sono gli unici a cui si può chiedere.</summary>
    public IReadOnlyList<GiocatorePubblico> AiVivi { get; init; } = Array.Empty<GiocatorePubblico>();

    public StatoVoto? Voto { get; init; }
    public StatoVoto? Ballottaggio { get; init; }
    public bool SecondoVoto { get; init; }
    public GiocatorePubblico? UltimoRogo { get; init; }

    public bool RuoliMortiRivelati { get; init; }
    public bool RuoloRogoRivelato { get; init; }
}

/// <summary>
/// Vista pubblica di un giocatore. Il ruolo è valorizzato solo quando è
/// davvero pubblico (proprio ruolo, caduti rivelati dal narratore, finale).
/// </summary>
public sealed record GiocatorePubblico(
    string Id,
    string Nome,
    string Iniziali,
    bool Vivo,
    bool Capobranco,
    bool Trasformato,
    Ruolo? Ruolo,
    Ruolo? RuoloIniziale,
    bool Ai = false)
{
    public string Accento => Ruolo is { } r ? Ruoli.Info(r).Accento : "#8b93a7";
    public string Icona => Ruolo is { } r ? Ruoli.Info(r).Icona : "❔";
    public string? NomeRuolo => Ruolo is { } r ? Ruoli.Nome(r) : null;
}

public sealed record AzioneGiocatore(
    string PassoId,
    string Titolo,
    string Istruzione,
    string Icona,
    bool Completato,
    bool RichiedeConferma,
    bool Saltato,
    string? Risultato,
    IReadOnlyList<GiocatorePubblico> Opzioni,
    bool PuoAgire,
    Potere Potere,
    IReadOnlyList<string> CompagniPasso);

public sealed record ConteggioVoto(string Id, string Nome, int Voti, IReadOnlyList<string> Elettori);

public sealed class StatoVoto
{
    public bool Aperto { get; init; }
    public bool Chiuso { get; init; }
    public bool PuoVotare { get; init; }
    public bool HaVotato { get; init; }
    public string? MioVotoNome { get; init; }
    public int Votanti { get; init; }
    public int Vivi { get; init; }
    public IReadOnlyList<ConteggioVoto> Conteggi { get; init; } = Array.Empty<ConteggioVoto>();
    public IReadOnlyList<string> NonVotanti { get; init; } = Array.Empty<string>();
    public string? Esito { get; init; }
    public IReadOnlyList<GiocatorePubblico> Candidati { get; init; } = Array.Empty<GiocatorePubblico>();
}

public static class StatoGiocatoreFactory
{
    public static StatoGiocatore Crea(Stanza stanza, Giocatore giocatore)
    {
        var p = stanza.Partita;
        var avviata = stanza.Avviata;
        var finale = p.Fase == FasePartita.Finita || p.Esito != null;

        var mostraRuoliMorti = stanza.RivelaRuoliMorti || finale;
        var mostraRuoloRogo = stanza.RivelaRuoloRogo || finale;
        var lupo = avviata && giocatore.Ruolo == Ruolo.Lupo;
        var mostraProprio = avviata || finale;

        var postiAi = stanza.Posti
            .Where(p => p.IsAi)
            .Select(p => p.GiocatoreId)
            .ToHashSet();

        GiocatorePubblico Pub(Giocatore g)
        {
            Ruolo? ruolo = null;
            Ruolo? iniziale = null;
            var suoProprio = mostraProprio && g.Id == giocatore.Id;
            var dettagli = finale || suoProprio;

            if (dettagli)
            {
                ruolo = g.Ruolo;
                iniziale = g.RuoloIniziale;
            }
            else if (!g.Vivo &&
                     (mostraRuoliMorti ||
                      (g.Id == p.UltimoRogoId && mostraRuoloRogo)))
            {
                ruolo = g.Ruolo;
            }

            // Il capobranco è noto al suo branco; il resto del villaggio lo
            // scopre solo alla fine. "Trasformato" resta un dettaglio privato.
            var capobrancoVisibile = dettagli || (lupo && g.Ruolo == Ruolo.Lupo);

            return new GiocatorePubblico(
                g.Id,
                g.Nome,
                g.Iniziali,
                g.Vivo,
                capobrancoVisibile && g.Capobranco,
                dettagli && g.Trasformato,
                ruolo,
                iniziale,
                postiAi.Contains(g.Id));
        }

        var giocatori = p.Giocatori
            .OrderByDescending(g => g.Vivo)
            .ThenBy(g => g.Nome)
            .Select(Pub)
            .ToList();

        var vivi = p.Vivi
            .OrderBy(g => g.Nome)
            .Select(Pub)
            .ToList();

        var mortiNotte = (p.RisultatoNotte?.MortiIds ?? new List<string>())
            .Select(id => p.Giocatore(id))
            .OfType<Giocatore>()
            .Select(Pub)
            .ToList();

        var candidatiGufo = p.Fase is FasePartita.Alba or FasePartita.Giorno or FasePartita.Voto
            ? p.BallottaggioIds
                .Select(id => p.Giocatore(id))
                .OfType<Giocatore>()
                .Where(g => g.Vivo)
                .Select(Pub)
                .ToList()
            : new List<GiocatorePubblico>();

        var compagni = lupo
            ? p.Vivi
                .Where(g => g.Ruolo == Ruolo.Lupo && g.Id != giocatore.Id)
                .Select(g => g.Nome)
                .OrderBy(n => n)
                .ToList()
            : new List<string>();

        // Il Copione sa sempre di chi ha copiato il ruolo. Il copiato scopre
        // il doppione solo dalla notte successiva: durante il primo giorno la
        // copia resta segreta, poi i due si svegliano insieme.
        var copiaDa = avviata && giocatore.CopiaDaId is { } copiatoId
            ? p.NomeDi(copiatoId)
            : null;
        if (copiaDa == "?") copiaDa = null;

        var copiatoDaNomi = avviata
            ? giocatore.CopieSubite
                // Il copiato scopre il doppione solo dalla notte successiva,
                // quando si svegliano insieme: se nel frattempo il Copione è
                // morto non c'è nessuna veglia condivisa da annunciare (e il
                // suo ruolo resterebbe nascosto come quello degli altri morti).
                .Where(c => finale || (p.Turno > c.Turno && p.Giocatore(c.CopioneId)?.Vivo == true))
                .Select(c => p.NomeDi(c.CopioneId))
                .Where(n => n != "?")
                .Distinct()
                .OrderBy(n => n)
                .ToList()
            : new List<string>();

        var azioni = new List<AzioneGiocatore>();
        if (avviata && giocatore.Vivo && p.Fase == FasePartita.Notte)
        {
            var corrente = p.PassoCorrente;

            foreach (var passo in p.PassiNotte)
            {
                if (!Autorizzato(giocatore, passo)) continue;

                var opzioni = passo.OpzioniIds
                    .Select(id => p.Giocatore(id))
                    .OfType<Giocatore>()
                    .Select(Pub)
                    .ToList();

                var compagniPasso = passo.ElencoAttori()
                    .Where(id => id != giocatore.Id)
                    .Select(id => p.NomeDi(id))
                    .Where(n => n != "?")
                    .ToList();

                azioni.Add(new AzioneGiocatore(
                    passo.Id,
                    passo.Titolo,
                    passo.Istruzione,
                    IconaPasso(passo),
                    passo.Completato,
                    passo.RichiedeConferma,
                    passo.Saltato,
                    passo.Risultato,
                    opzioni,
                    !passo.Completato && corrente?.Id == passo.Id,
                    passo.Potere,
                    compagniPasso));
            }
        }

        var eventiAlba = p.RisultatoNotte == null
            ? new List<string>()
            : p.RisultatoNotte.Eventi
                .Where(e => !e.Riservato)
                .Select(e => e.Testo)
                .ToList();

        var voti = stanza.SnapshotVoti();
        var votiBallottaggio = stanza.SnapshotVotiBallottaggio();

        var voto = CreaStatoVoto(
            p,
            giocatore,
            voti,
            aperto: p.Fase == FasePartita.Voto && !stanza.VotoChiuso,
            chiuso: stanza.VotoChiuso,
            esito: stanza.EsitoVoto,
            // Si vota chiunque sia vivo: anche se stessi e anche i compagni di
            // branco. Nessuna esclusione legata al ruolo, altrimenti la lista
            // dei candidati racconterebbe qualcosa sulle alleanze.
            candidati: p.Fase == FasePartita.Voto
                ? p.Vivi.ToList()
                : new List<Giocatore>(),
            mostraConteggi: stanza.VotoChiuso || p.Fase is FasePartita.Ballottaggio or FasePartita.Esecuzione or FasePartita.Finita,
            Pub);

        var ballottaggio = CreaStatoVoto(
            p,
            giocatore,
            votiBallottaggio,
            aperto: p.Fase == FasePartita.Ballottaggio && !stanza.BallottaggioChiuso,
            chiuso: stanza.BallottaggioChiuso,
            esito: stanza.EsitoBallottaggio,
            candidati: p.Fase == FasePartita.Ballottaggio
                ? p.BallottaggioIds
                    .Select(id => p.Giocatore(id))
                    .OfType<Giocatore>()
                    .Where(g => g.Vivo)
                    .ToList()
                : new List<Giocatore>(),
            mostraConteggi: stanza.BallottaggioChiuso || p.Fase is FasePartita.Esecuzione or FasePartita.Finita,
            Pub);

        var ultimoRogo = p.Giocatore(p.UltimoRogoId) is { } rogo ? Pub(rogo) : null;

        var chat = p.Fase == FasePartita.Giorno
            ? stanza.SnapshotChat()
            : Array.Empty<MessaggioChat>();

        var aiVivi = vivi.Where(g => g.Ai && g.Vivo && g.Id != giocatore.Id).ToList();

        return new StatoGiocatore
        {
            Codice = stanza.Codice,
            GiocatoreId = giocatore.Id,
            Nome = giocatore.Nome,
            Avviata = avviata,
            Vivo = giocatore.Vivo,
            Ruolo = avviata ? giocatore.Ruolo : null,
            Info = avviata ? Ruoli.Info(giocatore.Ruolo) : null,
            Capobranco = avviata && giocatore.Capobranco,
            Trasformato = avviata && giocatore.Trasformato,
            RuoloIniziale = avviata ? giocatore.RuoloIniziale : null,
            Fase = p.Fase,
            Turno = p.Turno,
            Esito = p.Esito,
            Finita = finale,
            Giocatori = giocatori,
            Vivi = vivi,
            MortiNotte = mortiNotte,
            CandidatiGufo = candidatiGufo,
            CompagniBranco = compagni,
            CopiaDaNome = copiaDa,
            CopiatoDaNomi = copiatoDaNomi,
            RuoloAgisceDiNotte = avviata && Ruoli.Info(giocatore.Ruolo).AzioneNotturna,
            Azioni = azioni,
            EventiAlba = eventiAlba,
            Chat = chat,
            AiVivi = aiVivi,
            Voto = voto,
            Ballottaggio = ballottaggio,
            SecondoVoto = p.Fase == FasePartita.Ballottaggio && stanza.BallottaggioDaPareggio,
            UltimoRogo = ultimoRogo,
            RuoliMortiRivelati = mostraRuoliMorti,
            RuoloRogoRivelato = mostraRuoloRogo
        };
    }

    private static StatoVoto CreaStatoVoto(
        Partita p,
        Giocatore giocatore,
        IReadOnlyDictionary<string, string> voti,
        bool aperto,
        bool chiuso,
        string? esito,
        IReadOnlyList<Giocatore> candidati,
        bool mostraConteggi,
        Func<Giocatore, GiocatorePubblico> pub)
    {
        var haVotato = voti.ContainsKey(giocatore.Id);
        var mioVoto = voti.TryGetValue(giocatore.Id, out var target) ? p.NomeDi(target) : null;

        var conteggi = mostraConteggi
            ? voti
                .GroupBy(kv => kv.Value)
                .Select(g => new ConteggioVoto(
                    g.Key,
                    p.NomeDi(g.Key),
                    g.Count(),
                    g.Select(kv => p.NomeDi(kv.Key)).OrderBy(n => n).ToList()))
                .OrderByDescending(c => c.Voti)
                .ThenBy(c => c.Nome)
                .ToList()
            : new List<ConteggioVoto>();

        var nonVotanti = mostraConteggi
            ? p.Vivi
                .Where(g => !voti.ContainsKey(g.Id))
                .Select(g => g.Nome)
                .OrderBy(n => n)
                .ToList()
            : new List<string>();

        var candidatiPubblici = candidati.Select(pub).ToList();

        return new StatoVoto
        {
            Aperto = aperto,
            Chiuso = chiuso,
            PuoVotare = aperto && giocatore.Vivo && !haVotato,
            HaVotato = haVotato,
            MioVotoNome = mioVoto,
            Votanti = voti.Count,
            Vivi = p.TotaleVivi,
            Conteggi = conteggi,
            NonVotanti = nonVotanti,
            Esito = esito,
            Candidati = candidatiPubblici
        };
    }

    public static bool Autorizzato(Giocatore giocatore, PassoNotte passo)
    {
        // I segnaposto dei morti non sono mai azioni: senza questo controllo
        // il Potere predefinito (Lupo) li renderebbe visibili al branco.
        if (passo.AttoreMorto)
            return false;

        if (passo.Tipo == TipoPasso.Capobranco)
            return giocatore.Ruolo == Ruolo.Lupo;

        if (passo.Potere == Potere.Lupo)
            return giocatore.Ruolo == Ruolo.Lupo;

        // Con i ruoli duplicati il passo appartiene a un gruppo: chiunque ne
        // faccia parte può agire e vede il risultato condiviso.
        return passo.ElencoAttori().Contains(giocatore.Id);
    }

    public static string IconaPasso(PassoNotte passo)
    {
        if (passo.Tipo == TipoPasso.Capobranco) return "👑";
        if (passo.AttoreMorto) return "💀";

        return passo.Potere switch
        {
            Potere.Lupo or Potere.ImputatoLupo => "🐺",
            Potere.Guardiano or Potere.ImputatoGuardiano => "🛡️",
            Potere.Annullatore => "🚫",
            Potere.Becchino => "⚰️",
            Potere.Veggente or Potere.ImputatoVeggente => "🔮",
            Potere.Puttana or Potere.ImputatoPuttana => "💃",
            Potere.Spia or Potere.ImputatoSpia => "🕵️",
            Potere.Cacciatore or Potere.ImputatoCacciatore => "🪤",
            Potere.Gufo or Potere.ImputatoGufo => "🦉",
            Potere.Copione or Potere.Copione2 => "🎭",
            _ => "✨"
        };
    }
}
