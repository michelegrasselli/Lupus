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

    public IReadOnlyList<AzioneGiocatore> Azioni { get; init; } = Array.Empty<AzioneGiocatore>();
    public IReadOnlyList<EsitoPrivato> Risultati { get; init; } = Array.Empty<EsitoPrivato>();
    public IReadOnlyList<string> EventiAlba { get; init; } = Array.Empty<string>();

    public StatoVoto? Voto { get; init; }
    public StatoVoto? Ballottaggio { get; init; }
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
    Ruolo? RuoloIniziale)
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
    bool PuoAgire);

public sealed record EsitoPrivato(int Turno, string Titolo, string Testo);

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
    public bool Pareggio { get; init; }
    public IReadOnlyList<GiocatorePubblico> Candidati { get; init; } = Array.Empty<GiocatorePubblico>();
    public IReadOnlyList<GiocatorePubblico> CandidatiPareggio { get; init; } = Array.Empty<GiocatorePubblico>();
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
                iniziale);
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
                    !passo.Completato && corrente?.Id == passo.Id));
            }
        }

        var risultati = avviata
            ? stanza.RisultatiDi(giocatore.Id)
                .OrderByDescending(r => r.Turno)
                .ThenBy(r => r.Titolo)
                .ToList()
            : new List<EsitoPrivato>();

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
            pareggio: stanza.VotoPareggio,
            candidatiPareggioIds: stanza.CandidatiPareggio,
            candidati: p.Fase == FasePartita.Voto
                ? p.Vivi.Where(g => g.Id != giocatore.Id).ToList()
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
            pareggio: stanza.BallottaggioPareggio,
            candidatiPareggioIds: stanza.CandidatiPareggioBallottaggio,
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
            Azioni = azioni,
            Risultati = risultati,
            EventiAlba = eventiAlba,
            Voto = voto,
            Ballottaggio = ballottaggio,
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
        bool pareggio,
        IReadOnlyList<string> candidatiPareggioIds,
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

        var candidatiPareggio = candidatiPareggioIds
            .Select(id => p.Giocatore(id))
            .OfType<Giocatore>()
            .Select(pub)
            .ToList();

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
            Pareggio = pareggio,
            Candidati = candidatiPubblici,
            CandidatiPareggio = candidatiPareggio
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

        return passo.AttoreId == giocatore.Id;
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
