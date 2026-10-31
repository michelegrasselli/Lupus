namespace Lupus.Core;

/// <summary>
/// Motore di gioco. Tutte le transizioni di stato passano da qui, così la UI
/// (e in futuro le API per i telefoni dei giocatori) restano semplici.
/// </summary>
public static class MotoreLupus
{
    // ---------------------------------------------------------------------
    // Avvio e fasi
    // ---------------------------------------------------------------------

    public static void Avvia(Partita p)
    {
        p.Turno = 1;
        p.Esito = null;
        p.Fase = FasePartita.NotteIntro;
        p.PreparaNuovaNotte();

        p.AggiungiLog($"Partita avviata con {p.Giocatori.Count} giocatori.", TipoLog.Configurazione);

        var lupi = p.LupiVivi.ToList();
        if (lupi.Count > 0 && p.Capobranco == null)
            AssegnaCapobrancoCasuale(p);

        var composizione = string.Join(", ",
            p.Giocatori.GroupBy(g => g.Ruolo)
                .Select(g => $"{Ruoli.Nome(g.Key)} x{g.Count()}"));
        p.AggiungiLog($"Composizione: {composizione}.", TipoLog.Configurazione);

        ControllaVittoria(p);
    }

    public static void IniziaNotte(Partita p)
    {
        if (p.Fase != FasePartita.NotteIntro) return;

        p.Fase = FasePartita.Notte;
        p.PreparaNuovaNotte();
        p.AggiungiLog($"Notte {p.Turno}: tutti dormono...", TipoLog.Notte);

        var lupi = p.LupiVivi.ToList();
        if (lupi.Count == 0)
        {
            ControllaVittoria(p);
            return;
        }

        // Se il capobranco è caduto (o è stato trasformato), il branco ne
        // sceglie un altro a caso tra i Lupi ancora vivi.
        if (p.Capobranco == null)
            AssegnaCapobrancoCasuale(p);

        foreach (var info in Ruoli.Tutti.OrderBy(r => r.Priorita))
        {
            if (!info.AzioneNotturna) continue;

            // Anche i morti vengono chiamati: il loro passo è un segnaposto
            // già chiuso, così l'ordine delle chiamate non rivela quali ruoli
            // sono caduti.
            var attori = p.Giocatori.Where(g => g.Ruolo == info.Ruolo).ToList();
            if (attori.Count == 0) continue;

            switch (info.Ruolo)
            {
                case Ruolo.Lupo:
                    AggiungiPassoLupi(p);
                    break;
                case Ruolo.Imputato:
                    // I passi dell'Imputato dipendono da chi lo ha puntato:
                    // vengono generati quando tutti i poteri precedenti sono completati.
                    // I morti ricevono subito il loro segnaposto.
                    foreach (var attore in attori.Where(a => !a.Vivo))
                        AggiungiPassoMorto(p, attore, info.Priorita);
                    break;
                case Ruolo.Becchino:
                    foreach (var attore in attori) AggiungiPassoBecchino(p, attore);
                    break;
                case Ruolo.Cacciatore:
                    foreach (var attore in attori) AggiungiPassoCacciatore(p, attore);
                    break;
                case Ruolo.Copione:
                case Ruolo.Copione2:
                    foreach (var attore in attori)
                    {
                        AggiungiPassoCopione(
                            p,
                            attore,
                            info.Ruolo == Ruolo.Copione ? Potere.Copione : Potere.Copione2,
                            info.Priorita);
                    }
                    break;
                default:
                    foreach (var attore in attori)
                    {
                        AggiungiPassoAzione(p, attore, PoterePerRuolo(info.Ruolo), info.Priorita);
                    }
                    break;
            }
        }

        if (p.PassiNotte.Count == 0)
        {
            p.AggiungiLog("Nessun potere da usare stanotte.", TipoLog.Info);
        }

        // Se non ci sono poteri con priorità inferiore a 10, l'Imputato
        // può essere generato subito (o registrare che non ha poteri).
        AggiornaPassiImputato(p);
    }

    public static void VaiAlGiorno(Partita p)
    {
        if (p.Fase != FasePartita.Alba) return;
        if (p.Esito != null)
        {
            p.Fase = FasePartita.Finita;
            return;
        }

        p.Fase = FasePartita.Giorno;
        p.AggiungiLog("Buongiorno a tutti! È aperta la discussione.", TipoLog.Info);
    }

    public static void ApriVoto(Partita p)
    {
        if (p.Fase != FasePartita.Giorno) return;
        p.Fase = FasePartita.Voto;
        p.AggiungiLog("È il momento del voto: chi va al rogo?", TipoLog.Voto);
    }

    public static void ProssimaNotte(Partita p)
    {
        if (p.Fase != FasePartita.Esecuzione) return;

        p.Turno++;
        p.Fase = FasePartita.NotteIntro;
        p.PreparaNuovaNotte();
        p.AggiungiLog($"Turno {p.Turno}: cala la notte.", TipoLog.Notte);
    }

    // ---------------------------------------------------------------------
    // Azioni notturne
    // ---------------------------------------------------------------------

    public static EsitoPasso EseguiPasso(Partita p, string passoId, string? bersaglioId)
    {
        if (p.Fase != FasePartita.Notte)
            return EsitoPasso.Fallito("Non è il momento delle azioni notturne.");

        var passo = p.PassiNotte.FirstOrDefault(x => x.Id == passoId);
        if (passo == null)
            return EsitoPasso.Fallito("Passo non trovato.");
        if (passo.Completato)
            return EsitoPasso.Fallito("Questo passo è già stato completato.");

        if (passo.Tipo == TipoPasso.Capobranco)
        {
            var scelto = p.Giocatore(bersaglioId);
            if (scelto == null || !scelto.Vivo || scelto.Ruolo != Ruolo.Lupo)
                return EsitoPasso.Fallito("Scegli un Lupo vivo.");

            foreach (var g in p.Giocatori) g.Capobranco = false;
            scelto.Capobranco = true;
            passo.Completato = true;
            passo.Risultato = $"{scelto.Nome} è il nuovo capobranco.";
            p.AggiungiLog(passo.Risultato, TipoLog.Configurazione);
            AggiornaPassiImputato(p);
            return EsitoPasso.Successo();
        }

        var attore = p.Giocatore(passo.AttoreId);
        if (attore == null)
            return EsitoPasso.Fallito("Attore non trovato.");

        // Il branco agisce tramite il capobranco attuale, anche se è stato
        // eletto dopo la creazione del passo.
        if (passo.Potere == Potere.Lupo)
        {
            attore = p.Capobranco ?? attore;
            passo.AttoreId = attore.Id;
        }

        if (!attore.Vivo)
        {
            passo.Completato = true;
            passo.Saltato = true;
            passo.Risultato = $"{attore.Nome} non è in vita: il turno viene saltato.";
            AggiornaPassiImputato(p);
            return EsitoPasso.Successo();
        }

        if (string.IsNullOrWhiteSpace(bersaglioId) || !passo.OpzioniIds.Contains(bersaglioId))
            return EsitoPasso.Fallito("Scegli un bersaglio valido.");

        var bersaglio = p.Giocatore(bersaglioId);
        if (bersaglio == null)
            return EsitoPasso.Fallito("Bersaglio non trovato.");

        if (IsBloccato(p, attore, passo.Potere))
        {
            passo.Completato = true;
            passo.Saltato = true;
            passo.Risultato = $"{attore.Nome} è stato annullato per questa notte.";
            p.AggiungiLog(passo.Risultato, TipoLog.Potere);
            AggiornaPassiImputato(p);
            return EsitoPasso.Successo();
        }

        p.AzioniNotte.Add(new AzioneNotte
        {
            Potere = passo.Potere,
            AttoreId = attore.Id,
            BersaglioId = bersaglio.Id
        });

        passo.Risultato = CalcolaRisultato(p, attore, bersaglio, passo.Potere);
        passo.Completato = !passo.RichiedeConferma;

        // Il diario è solo del narratore: qui possiamo dire ciò che il
        // giocatore non deve sapere.
        var nota = NotaNarratore(attore, bersaglio, passo.Potere);
        p.AggiungiLog(
            $"{attore.Nome} usa {EtichettaPotere(passo.Potere)} su {bersaglio.Nome}. {passo.Risultato}"
            + (nota is null ? "" : $" {nota}"),
            TipoLog.Potere);

        AggiornaPassiImputato(p);
        return EsitoPasso.Successo();
    }

    public static void ConfermaRisultato(Partita p, string passoId)
    {
        var passo = p.PassiNotte.FirstOrDefault(x => x.Id == passoId);
        if (passo == null || passo.Completato || passo.Risultato == null) return;

        passo.Completato = true;
        AggiornaPassiImputato(p);
    }

    public static EsitoPasso RisolviNotte(Partita p)
    {
        if (p.Fase != FasePartita.Notte)
            return EsitoPasso.Fallito("Non è il momento di risolvere la notte.");
        if (!p.NotteCompleta)
            return EsitoPasso.Fallito("Ci sono ancora azioni da completare.");

        var risultato = new RisultatoNotte();
        var azioni = p.AzioniNotte.ToList();

        var protetti = new HashSet<string>(azioni
            .Where(a => a.Potere is Potere.Guardiano or Potere.ImputatoGuardiano)
            .Select(a => a.BersaglioId));

        var trappole = new HashSet<string>(azioni
            .Where(a => a.Potere is Potere.Cacciatore or Potere.ImputatoCacciatore)
            .Select(a => a.BersaglioId));

        var gufati = azioni
            .Where(a => a.Potere is Potere.Gufo or Potere.ImputatoGufo)
            .Select(a => a.BersaglioId)
            .Distinct()
            .ToList();

        var morti = new HashSet<string>();
        var trasformazioni = new List<(string Id, Ruolo Nuovo)>();
        var cause = new Dictionary<string, string>();

        // --- Attacchi del branco e attacchi copiati dall'Imputato ---
        foreach (var attacco in azioni.Where(a => a.Potere is Potere.Lupo or Potere.ImputatoLupo))
        {
            var vittima = p.Giocatore(attacco.BersaglioId);
            if (vittima == null || !vittima.Vivo) continue;

            bool kamikaze = vittima.Ruolo == Ruolo.Kamikaze && !protetti.Contains(vittima.Id);
            bool trappola = trappole.Contains(vittima.Id) || kamikaze;

            if (trappola)
            {
                if (attacco.Potere == Potere.Lupo)
                {
                    var capo = p.Capobranco;
                    if (capo != null && !protetti.Contains(capo.Id))
                    {
                        morti.Add(capo.Id);
                        cause[capo.Id] = "è caduto nella trappola del Cacciatore";
                    }
                }
                else
                {
                    var imp = p.Giocatore(attacco.AttoreId);
                    if (imp != null && !protetti.Contains(imp.Id))
                    {
                        morti.Add(imp.Id);
                        cause[imp.Id] = "è caduto nella trappola del Cacciatore";
                    }
                }

                // La trappola (o il Kamikaze) salva la vittima da questo attacco.
                continue;
            }

            if (protetti.Contains(vittima.Id)) continue;

            if (vittima.Ruolo == Ruolo.BabyLupo)
            {
                if (!trasformazioni.Any(t => t.Id == vittima.Id))
                    trasformazioni.Add((vittima.Id, Ruolo.Lupo));
                continue;
            }

            if (vittima.Ruolo == Ruolo.Diavolo)
            {
                vittima.DiavoloSbranato = true;
                p.AggiungiEvento(risultato,
                    $"{vittima.Nome} è il Diavolo: è stato sbranato ma sopravvive...", true);
                continue;
            }

            morti.Add(vittima.Id);
            cause[vittima.Id] = attacco.Potere == Potere.Lupo
                ? "è stato sbranato dai lupi"
                : "è stato sbranato dal potere copiato dell'Imputato";
        }

        // --- Puttana (o Imputato con potere copiato) che sceglie un Lupo ---
        foreach (var azione in azioni.Where(a => a.Potere is Potere.Puttana or Potere.ImputatoPuttana))
        {
            var bersaglio = p.Giocatore(azione.BersaglioId);
            if (bersaglio?.Ruolo != Ruolo.Lupo) continue;

            var attore = p.Giocatore(azione.AttoreId);
            if (attore == null || !attore.Vivo || protetti.Contains(attore.Id)) continue;

            morti.Add(attore.Id);
            cause[attore.Id] = "è morto dopo aver passato la notte con un Lupo";
        }

        // --- Applica i decessi ---
        foreach (var id in morti)
        {
            var g = p.Giocatore(id);
            if (g == null || !g.Vivo) continue;

            g.Vivo = false;
            g.MortoInNotte = true;
            g.Capobranco = false;
            risultato.MortiIds.Add(id);

            p.AggiungiLog(
                $"Stanotte ci ha lasciato {g.Nome} ({cause.GetValueOrDefault(id, "causa sconosciuta")}).",
                TipoLog.Morte);
            p.AggiungiEvento(risultato, $"Stanotte ci ha lasciato {g.Nome}...");
        }

        // --- Baby Lupo ---
        foreach (var (id, nuovo) in trasformazioni)
        {
            var g = p.Giocatore(id);
            if (g == null || !g.Vivo) continue;

            g.RuoloIniziale ??= g.Ruolo;
            g.Ruolo = nuovo;
            g.Trasformato = true;
            g.Capobranco = false;
            risultato.TrasformatiIds.Add(id);

            p.AggiungiLog($"{g.Nome} era il Baby Lupo: stanotte è nato un nuovo Lupo.", TipoLog.Trasformazione);
            p.AggiungiEvento(risultato, "Stanotte è nato un nuovo Lupo...");
        }

        // --- Copioni ---
        // I Copioni possono puntarsi a vicenda. La copia si risolve sui ruoli
        // com'erano all'inizio dell'alba, così l'ordine dei passi non cambia
        // l'esito: chi punta un Copione resta un Copione e potrà copiare di
        // nuovo la notte successiva.
        var ruoliPrimaDellAlba = p.Giocatori.ToDictionary(g => g.Id, g => g.Ruolo);

        foreach (var azione in azioni.Where(a => a.Potere is Potere.Copione or Potere.Copione2))
        {
            var attore = p.Giocatore(azione.AttoreId);
            var bersaglio = p.Giocatore(azione.BersaglioId);
            if (attore == null || !attore.Vivo || bersaglio == null) continue;

            var ruoloCopiato = ruoliPrimaDellAlba.GetValueOrDefault(bersaglio.Id, bersaglio.Ruolo);

            if (ruoloCopiato is Ruolo.Copione or Ruolo.Copione2)
            {
                p.AggiungiLog(
                    $"{attore.Nome} ha puntato {bersaglio.Nome}, che è ancora un Copione: resta un Copione.",
                    TipoLog.Trasformazione);
                continue;
            }

            attore.RuoloIniziale ??= attore.Ruolo;
            attore.Ruolo = ruoloCopiato;
            attore.Trasformato = true;
            attore.Capobranco = false;
            risultato.TrasformatiIds.Add(attore.Id);

            var testo = $"{attore.Nome} ha copiato il ruolo di {bersaglio.Nome}: ora è {Ruoli.Nome(ruoloCopiato)}.";
            p.AggiungiLog(testo, TipoLog.Trasformazione);
            p.AggiungiEvento(risultato, testo, true);
        }

        if (risultato.MortiIds.Count == 0)
        {
            p.AggiungiEvento(risultato, "Stanotte non è morto nessuno!");
            p.AggiungiLog("Stanotte non è morto nessuno.", TipoLog.Info);
        }

        p.BallottaggioIds = gufati
            .Where(id => p.Giocatore(id)?.Vivo == true)
            .Distinct()
            .ToList();

        p.RisultatoNotte = risultato;
        p.AzioniNotte.Clear();
        p.Fase = FasePartita.Alba;
        p.AggiungiLog("Sorge il sole...", TipoLog.Notte);

        // Se il capobranco è caduto o ha cambiato ruolo, il branco ne sceglie
        // subito un altro a caso tra i Lupi vivi.
        if (p.Capobranco == null)
            AssegnaCapobrancoCasuale(p);

        ControllaVittoria(p);
        return EsitoPasso.Successo();
    }

    // ---------------------------------------------------------------------
    // Voto
    // ---------------------------------------------------------------------

    public static EsitoPasso Vota(Partita p, string giocatoreId)
    {
        if (p.Fase != FasePartita.Voto)
            return EsitoPasso.Fallito("Non è il momento del voto.");

        var g = p.Giocatore(giocatoreId);
        if (g == null || !g.Vivo)
            return EsitoPasso.Fallito("Scegli un giocatore vivo.");

        p.VotatoId = g.Id;

        var gufati = p.BallottaggioIds
            .Where(id => p.Giocatore(id)?.Vivo == true)
            .Distinct()
            .ToList();

        var candidati = new List<string> { g.Id };
        candidati.AddRange(gufati);
        candidati = candidati.Distinct().ToList();

        if (candidati.Count > 1)
        {
            p.BallottaggioIds = candidati;
            p.Fase = FasePartita.Ballottaggio;
            p.AggiungiLog(
                $"Il Gufo manda al ballottaggio: {string.Join(", ", candidati.Select(p.NomeDi))}.",
                TipoLog.Voto);
            return EsitoPasso.Successo();
        }

        return EseguiRogo(p, g.Id);
    }

    public static EsitoPasso ScegliBallottaggio(Partita p, string giocatoreId)
    {
        if (p.Fase != FasePartita.Ballottaggio)
            return EsitoPasso.Fallito("Non è il momento del ballottaggio.");
        if (!p.BallottaggioIds.Contains(giocatoreId))
            return EsitoPasso.Fallito("Scegli uno dei candidati al ballottaggio.");

        return EseguiRogo(p, giocatoreId);
    }

    private static EsitoPasso EseguiRogo(Partita p, string giocatoreId)
    {
        var g = p.Giocatore(giocatoreId);
        if (g == null || !g.Vivo)
            return EsitoPasso.Fallito("Il giocatore non è più in vita.");

        g.Vivo = false;
        g.MortoAlRogo = true;
        g.Capobranco = false;
        p.UltimoRogoId = g.Id;
        p.AggiungiLog($"{g.Nome} è andato al rogo.", TipoLog.Voto);

        // Se è caduto il capobranco, il branco ne sceglie subito un altro.
        if (p.Capobranco == null)
            AssegnaCapobrancoCasuale(p);

        if (g.Ruolo == Ruolo.Diavolo && g.DiavoloSbranato)
        {
            p.Esito = EsitoPartita.Diavolo;
            p.Fase = FasePartita.Finita;
            p.AggiungiLog(
                "Il Diavolo è stato sbranato e votato lo stesso turno: vince la partita!",
                TipoLog.Vittoria);
            return EsitoPasso.Successo();
        }

        ControllaVittoria(p);
        if (p.Esito != null)
        {
            p.Fase = FasePartita.Finita;
            return EsitoPasso.Successo();
        }

        p.Fase = FasePartita.Esecuzione;
        foreach (var x in p.Giocatori) x.DiavoloSbranato = false;
        p.VotatoId = null;
        p.BallottaggioIds.Clear();
        p.AggiungiLog("Il villaggio si prepara a una nuova notte...", TipoLog.Info);
        return EsitoPasso.Successo();
    }

    // ---------------------------------------------------------------------
    // Vittoria e capobranco
    // ---------------------------------------------------------------------

    public static void ControllaVittoria(Partita p)
    {
        if (p.Esito != null) return;

        int lupi = p.LupiViviCount;
        int altri = p.AltriViviCount;

        if (lupi == 0 && altri == 0)
        {
            p.Esito = EsitoPartita.Pareggio;
            p.Fase = FasePartita.Finita;
            p.AggiungiLog("Tutti i giocatori sono morti: la partita finisce in parità.", TipoLog.Vittoria);
        }
        else if (lupi == 0)
        {
            p.Esito = EsitoPartita.Buoni;
            p.Fase = FasePartita.Finita;
            p.AggiungiLog("I buoni vincono la partita!", TipoLog.Vittoria);
        }
        else if (lupi >= altri)
        {
            p.Esito = EsitoPartita.Lupi;
            p.Fase = FasePartita.Finita;
            p.AggiungiLog("I lupi hanno vinto la partita...", TipoLog.Vittoria);
        }
    }

    public static EsitoPasso ImpostaCapobranco(Partita p, string giocatoreId)
    {
        var g = p.Giocatore(giocatoreId);
        if (g == null || !g.Vivo || g.Ruolo != Ruolo.Lupo)
            return EsitoPasso.Fallito("Il capobranco deve essere un Lupo vivo.");

        foreach (var x in p.Giocatori) x.Capobranco = false;
        g.Capobranco = true;
        p.AggiungiLog($"{g.Nome} è il capobranco.", TipoLog.Configurazione);
        return EsitoPasso.Successo();
    }

    /// <summary>
    /// Sceglie a caso il capobranco tra i Lupi vivi. Viene usato all'avvio e
    /// ogni volta che il capobranco precedente muore o cambia ruolo.
    /// </summary>
    public static void AssegnaCapobrancoCasuale(Partita p)
    {
        var lupi = p.LupiVivi.ToList();
        if (lupi.Count == 0) return;

        foreach (var g in p.Giocatori) g.Capobranco = false;

        var scelto = lupi[Random.Shared.Next(lupi.Count)];
        scelto.Capobranco = true;
        p.AggiungiLog($"Capobranco: {scelto.Nome}.", TipoLog.Configurazione);
    }

    // ---------------------------------------------------------------------
    // Generazione dei passi
    // ---------------------------------------------------------------------

    private static void AggiungiPassoLupi(Partita p)
    {
        var capo = p.Capobranco ?? p.LupiVivi.FirstOrDefault();
        if (capo == null) return;

        var opzioni = p.Vivi
            .Where(g => g.Ruolo != Ruolo.Lupo)
            .Select(g => g.Id)
            .ToList();

        if (opzioni.Count == 0)
        {
            p.PassiNotte.Add(PassoCompletato(
                p, capo, Potere.Lupo, 3, "🐺 Il branco dei lupi",
                "Nessun bersaglio valido: tutti i superstiti sono Lupi.", TipoLog.Info));
            return;
        }

        p.PassiNotte.Add(new PassoNotte
        {
            Tipo = TipoPasso.Azione,
            Potere = Potere.Lupo,
            AttoreId = capo.Id,
            Priorita = 3,
            Titolo = "🐺 Il branco dei lupi",
            Istruzione = "Chi verrà mangiato stanotte? Scegli un giocatore non lupo.",
            OpzioniIds = opzioni
        });
    }

    private static void AggiungiPassoAzione(Partita p, Giocatore attore, Potere potere, int priorita)
    {
        if (!attore.Vivo)
        {
            AggiungiPassoMorto(p, attore, priorita);
            return;
        }

        var opzioni = OpzioniPerPotere(p, attore, potere);
        var titolo = $"{Ruoli.Info(attore.Ruolo).Icona} {Ruoli.Nome(attore.Ruolo)} · {attore.Nome}";

        if (opzioni.Count == 0)
        {
            p.PassiNotte.Add(PassoCompletato(
                p, attore, potere, priorita, titolo,
                "Nessun bersaglio valido per questa notte.", TipoLog.Info));
            return;
        }

        p.PassiNotte.Add(new PassoNotte
        {
            Tipo = TipoPasso.Azione,
            Potere = potere,
            AttoreId = attore.Id,
            Priorita = priorita,
            Titolo = titolo,
            Istruzione = DescrizionePotere(potere),
            OpzioniIds = opzioni,
            RichiedeConferma = RichiedeConferma(potere)
        });
    }

    private static void AggiungiPassoBecchino(Partita p, Giocatore attore)
    {
        if (!attore.Vivo)
        {
            AggiungiPassoMorto(p, attore, 6);
            return;
        }

        var opzioni = OpzioniPerPotere(p, attore, Potere.Becchino);
        var titolo = $"⚰️ Becchino · {attore.Nome}";

        if (opzioni.Count == 0)
        {
            p.PassiNotte.Add(PassoCompletato(
                p, attore, Potere.Becchino, 6, titolo,
                "Non ci sono ancora morti di cui puoi sapere...", TipoLog.Info));
            return;
        }

        p.PassiNotte.Add(new PassoNotte
        {
            Tipo = TipoPasso.Azione,
            Potere = Potere.Becchino,
            AttoreId = attore.Id,
            Priorita = 6,
            Titolo = titolo,
            Istruzione = DescrizionePotere(Potere.Becchino),
            OpzioniIds = opzioni,
            RichiedeConferma = true
        });
    }

    private static void AggiungiPassoCacciatore(Partita p, Giocatore attore)
    {
        if (!attore.Vivo)
        {
            AggiungiPassoMorto(p, attore, 2);
            return;
        }

        var titolo = $"🪤 Cacciatore · {attore.Nome}";

        if (attore.TrappolaInCooldown)
        {
            attore.TrappolaInCooldown = false;
            p.PassiNotte.Add(PassoCompletato(
                p, attore, Potere.Cacciatore, 2, titolo,
                "Hai già piazzato una trappola la scorsa notte: stanotte riposi.", TipoLog.Info));
            return;
        }

        AggiungiPassoAzione(p, attore, Potere.Cacciatore, 2);
    }

    private static void AggiungiPassoCopione(Partita p, Giocatore attore, Potere potere, int priorita)
    {
        if (!attore.Vivo)
        {
            AggiungiPassoMorto(p, attore, priorita);
            return;
        }

        var opzioni = OpzioniPerPotere(p, attore, potere);
        var titolo = $"🎭 {Ruoli.Nome(attore.Ruolo)} · {attore.Nome}";

        if (opzioni.Count == 0)
        {
            p.PassiNotte.Add(PassoCompletato(
                p, attore, potere, priorita, titolo,
                "Nessun giocatore valido da copiare.", TipoLog.Info));
            return;
        }

        p.PassiNotte.Add(new PassoNotte
        {
            Tipo = TipoPasso.Azione,
            Potere = potere,
            AttoreId = attore.Id,
            Priorita = priorita,
            Titolo = titolo,
            Istruzione = DescrizionePotere(potere),
            OpzioniIds = opzioni,
            RichiedeConferma = true
        });
    }

    private static void AggiornaPassiImputato(Partita p)
    {
        if (p.PassiImputatoGenerati) return;
        if (p.PassiNotte.Any(x => !x.Completato && x.Priorita < 10)) return;

        var imputati = p.Vivi.Where(g => g.Ruolo == Ruolo.Imputato).ToList();
        if (imputati.Count == 0)
        {
            p.PassiImputatoGenerati = true;
            return;
        }

        var nuovi = new List<PassoNotte>();

        foreach (var imp in imputati)
        {
            if (IsBloccato(p, imp, Potere.ImputatoLupo))
            {
                nuovi.Add(PassoCompletato(
                    p, imp, Potere.Nessuno, 10, $"⚖️ Imputato · {imp.Nome}",
                    $"{imp.Nome} è stato annullato: nessun potere copiato.", TipoLog.Potere));
                continue;
            }

            var poteri = PoteriCopiati(p, imp);
            if (poteri.Count == 0)
            {
                nuovi.Add(PassoCompletato(
                    p, imp, Potere.Nessuno, 10, $"⚖️ Imputato · {imp.Nome}",
                    $"{imp.Nome} non ha poteri da usare stanotte.", TipoLog.Info));
                continue;
            }

            foreach (var potere in poteri)
            {
                var opzioni = OpzioniPerPotere(p, imp, potere);
                if (opzioni.Count == 0)
                {
                    nuovi.Add(PassoCompletato(
                        p, imp, potere, 10, $"⚖️ Imputato · {imp.Nome}",
                        $"Nessun bersaglio valido per {EtichettaPotere(potere)}.", TipoLog.Info));
                    continue;
                }

                nuovi.Add(new PassoNotte
                {
                    Tipo = TipoPasso.Azione,
                    Potere = potere,
                    AttoreId = imp.Id,
                    Priorita = 10,
                    Titolo = $"⚖️ Imputato · {imp.Nome}",
                    Istruzione = DescrizionePotere(potere),
                    OpzioniIds = opzioni,
                    RichiedeConferma = RichiedeConferma(potere)
                });
            }
        }

        p.PassiImputatoGenerati = true;

        int index = p.PassiNotte.FindIndex(x => x.Priorita > 10);
        if (index < 0) p.PassiNotte.AddRange(nuovi);
        else p.PassiNotte.InsertRange(index, nuovi);
    }

    /// <summary>
    /// Segnaposto per un attore morto: il ruolo viene comunque "chiamato"
    /// ogni notte, ma il passo nasce già completato e non produce azioni.
    /// Serve a non far dedurre ai vivi quali ruoli sono caduti dall'ordine
    /// o dal numero delle chiamate.
    /// </summary>
    private static void AggiungiPassoMorto(Partita p, Giocatore attore, int priorita)
    {
        var titolo = $"{Ruoli.Info(attore.Ruolo).Icona} {Ruoli.Nome(attore.Ruolo)} · {attore.Nome}";
        var passo = PassoCompletato(
            p, attore, Potere.Nessuno, priorita, titolo,
            $"{attore.Nome} è morto: stanotte non agisce.", TipoLog.Info);

        passo.AttoreMorto = true;
        p.PassiNotte.Add(passo);
    }

    private static PassoNotte PassoCompletato(
        Partita p, Giocatore attore, Potere potere, int priorita,
        string titolo, string risultato, TipoLog tipo)
    {
        p.AggiungiLog(risultato, tipo);

        return new PassoNotte
        {
            Tipo = TipoPasso.Azione,
            Potere = potere,
            AttoreId = attore.Id,
            Priorita = priorita,
            Titolo = titolo,
            Completato = true,
            Saltato = true,
            Risultato = risultato
        };
    }

    // ---------------------------------------------------------------------
    // Regole e testi
    // ---------------------------------------------------------------------

    private static bool IsBloccato(Partita p, Giocatore attore, Potere potere)
    {
        bool annullato = p.AzioniNotte.Any(a =>
            a.Potere == Potere.Annullatore && a.BersaglioId == attore.Id);

        if (!annullato) return false;

        // I lupi in branco sono immuni all'Annullatore.
        if (attore.Ruolo == Ruolo.Lupo && p.LupiViviCount > 1) return false;

        return true;
    }

    private static List<Potere> PoteriCopiati(Partita p, Giocatore imputato)
    {
        var poteri = new List<Potere>();

        void Aggiungi(Potere sorgente, Potere copiato)
        {
            if (poteri.Contains(copiato)) return;
            if (p.AzioniNotte.Any(a => a.BersaglioId == imputato.Id && a.Potere == sorgente))
                poteri.Add(copiato);
        }

        Aggiungi(Potere.Lupo, Potere.ImputatoLupo);
        Aggiungi(Potere.Guardiano, Potere.ImputatoGuardiano);
        Aggiungi(Potere.Puttana, Potere.ImputatoPuttana);
        Aggiungi(Potere.Veggente, Potere.ImputatoVeggente);
        Aggiungi(Potere.Spia, Potere.ImputatoSpia);
        Aggiungi(Potere.Cacciatore, Potere.ImputatoCacciatore);
        Aggiungi(Potere.Gufo, Potere.ImputatoGufo);

        return poteri;
    }

    private static string CalcolaRisultato(Partita p, Giocatore attore, Giocatore bersaglio, Potere potere)
    {
        switch (potere)
        {
            case Potere.Lupo:
                return $"Il branco ha scelto {bersaglio.Nome}.";

            case Potere.Annullatore:
                return $"{bersaglio.Nome} non potrà usare il suo potere stanotte.";

            case Potere.Guardiano:
                return $"{bersaglio.Nome} sarà protetto stanotte.";

            case Potere.Cacciatore:
                attore.TrappolaInCooldown = true;
                return $"Trappola piazzata su {bersaglio.Nome}. La prossima notte non potrai piazzarne un'altra.";

            case Potere.Gufo:
                return $"{bersaglio.Nome} andrà al ballottaggio del prossimo giorno.";

            case Potere.Puttana:
                // La Puttana non deve sapere se ha scelto un Lupo: lo scopre
                // solo all'alba, quando eventualmente muore.
                return $"{attore.Nome} passerà la notte con {bersaglio.Nome}.";

            case Potere.Veggente:
            case Potere.ImputatoVeggente:
                return $"{bersaglio.Nome} è {Allineamento(bersaglio)}.";

            case Potere.Becchino:
                return $"{bersaglio.Nome} era {Allineamento(bersaglio)}.";

            case Potere.Spia:
            case Potere.ImputatoSpia:
                return CalcolaSpia(p, attore, bersaglio);

            case Potere.Copione:
            case Potere.Copione2:
                // Puntare un Copione è lecito: non c'è nulla da copiare e si
                // resta Copioni, potendo riprovare la notte successiva.
                if (bersaglio.Ruolo is Ruolo.Copione or Ruolo.Copione2)
                    return $"{bersaglio.Nome} è ancora un Copione: resterai un Copione.";
                return $"Dalla prossima notte {attore.Nome} sarà {Ruoli.Nome(bersaglio.Ruolo)}.";

            case Potere.ImputatoLupo:
                return $"Tramite il potere copiato del Lupo, il branco ha scelto {bersaglio.Nome}.";

            case Potere.ImputatoGuardiano:
                return $"{bersaglio.Nome} sarà protetto stanotte (potere copiato).";

            case Potere.ImputatoCacciatore:
                attore.TrappolaInCooldown = true;
                return $"Trappola copiata piazzata su {bersaglio.Nome}.";

            case Potere.ImputatoGufo:
                return $"{bersaglio.Nome} andrà al ballottaggio del prossimo giorno (potere copiato).";

            case Potere.ImputatoPuttana:
                return $"{attore.Nome} passerà la notte con {bersaglio.Nome} (potere copiato).";

            default:
                return "";
        }
    }

    /// <summary>
    /// Dettagli che restano al narratore: il giocatore non deve scoprire ciò
    /// che il suo potere non gli dice esplicitamente. Finiscono solo nel
    /// diario, che non viene mai serializzato verso i telefoni.
    /// </summary>
    private static string? NotaNarratore(Giocatore attore, Giocatore bersaglio, Potere potere)
    {
        if (potere is not (Potere.Puttana or Potere.ImputatoPuttana))
            return null;

        if (bersaglio.Ruolo != Ruolo.Lupo)
            return null;

        return $"(Solo narratore: {bersaglio.Nome} è un Lupo, quindi {attore.Nome} morirà all'alba se non protetto.)";
    }

    private static string CalcolaSpia(Partita p, Giocatore attore, Giocatore bersaglio)
    {
        var azioni = p.AzioniNotte
            .Where(a => a.BersaglioId == bersaglio.Id)
            .Where(a => !(a.AttoreId == attore.Id &&
                          (a.Potere == Potere.Spia || a.Potere == Potere.ImputatoSpia)))
            .ToList();

        if (azioni.Count == 0)
            return $"Nessuno ha puntato {bersaglio.Nome} stanotte.";

        // La Spia scopre solo CHI ha puntato il bersaglio, non con quale
        // potere: aggiungere il ruolo dell'attore le regalerebbe un'informazione
        // che il suo potere non prevede.
        var righe = azioni
            .Select(a => p.Giocatore(a.AttoreId)?.Nome ?? "?")
            .Distinct()
            .ToList();

        return $"{bersaglio.Nome} è stato puntato da: {string.Join(", ", righe)}.";
    }

    private static string Allineamento(Giocatore g) =>
        Ruoli.Info(g.Ruolo).Buono ? "BUONO" : "CATTIVO";

    private static Potere PoterePerRuolo(Ruolo ruolo) => ruolo switch
    {
        Ruolo.Lupo => Potere.Lupo,
        Ruolo.Guardiano => Potere.Guardiano,
        Ruolo.Annullatore => Potere.Annullatore,
        Ruolo.Becchino => Potere.Becchino,
        Ruolo.Veggente => Potere.Veggente,
        Ruolo.Puttana => Potere.Puttana,
        Ruolo.Spia => Potere.Spia,
        Ruolo.Cacciatore => Potere.Cacciatore,
        Ruolo.Gufo => Potere.Gufo,
        Ruolo.Copione => Potere.Copione,
        Ruolo.Copione2 => Potere.Copione2,
        _ => throw new InvalidOperationException($"Il ruolo {ruolo} non ha un potere notturno.")
    };

    private static bool RichiedeConferma(Potere potere) => potere is
        Potere.Veggente or Potere.Becchino or Potere.Spia or
        Potere.Copione or Potere.Copione2 or
        Potere.ImputatoVeggente or Potere.ImputatoSpia or
        Potere.ImputatoPuttana or Potere.Puttana;

    public static List<string> OpzioniPerPotere(Partita p, Giocatore attore, Potere potere)
    {
        return potere switch
        {
            Potere.Lupo or Potere.ImputatoLupo =>
                p.Vivi.Where(g => g.Ruolo != Ruolo.Lupo && g.Id != attore.Id)
                    .Select(g => g.Id).ToList(),

            Potere.Becchino =>
                p.Giocatori.Where(g => !g.Vivo).Select(g => g.Id).ToList(),

            Potere.Annullatore =>
                p.Vivi.Where(g => g.Id != attore.Id).Select(g => g.Id).ToList(),

            Potere.Cacciatore or Potere.ImputatoCacciatore =>
                p.Vivi.Select(g => g.Id).ToList(),

            Potere.Guardiano or Potere.ImputatoGuardiano =>
                p.Vivi.Select(g => g.Id).ToList(),

            Potere.Gufo or Potere.ImputatoGufo =>
                p.Vivi.Where(g => g.Id != attore.Id).Select(g => g.Id).ToList(),

            Potere.Puttana or Potere.ImputatoPuttana =>
                p.Vivi.Where(g => g.Id != attore.Id).Select(g => g.Id).ToList(),

            Potere.Veggente or Potere.ImputatoVeggente =>
                p.Vivi.Where(g => g.Id != attore.Id).Select(g => g.Id).ToList(),

            Potere.Spia or Potere.ImputatoSpia =>
                p.Vivi.Select(g => g.Id).ToList(),

            Potere.Copione or Potere.Copione2 =>
                p.Vivi.Where(g => g.Id != attore.Id)
                    .Select(g => g.Id).ToList(),

            _ => new List<string>()
        };
    }

    public static string DescrizionePotere(Potere potere) => potere switch
    {
        Potere.Lupo => "Chi verrà mangiato stanotte?",
        Potere.Annullatore => "A chi annullerai il potere stanotte?",
        Potere.Guardiano => "Chi proteggerai stanotte?",
        Potere.Cacciatore => "Su chi piazzerai una trappola stanotte?",
        Potere.Gufo => "Chi manderai al ballottaggio stanotte?",
        Potere.Becchino => "Di quale morto vuoi sapere stanotte?",
        Potere.Puttana => "Con chi vuoi spassartela stanotte?",
        Potere.Veggente => "Di chi vuoi sapere stanotte?",
        Potere.Spia => "Chi spierai stanotte?",
        Potere.Copione or Potere.Copione2 => "Chi copierai stanotte?",
        Potere.ImputatoLupo => "Chi verrà mangiato stanotte? (potere copiato del Lupo)",
        Potere.ImputatoGuardiano => "Chi proteggerai stanotte? (potere copiato del Guardiano)",
        Potere.ImputatoPuttana => "Con chi vuoi spassartela stanotte? (potere copiato della Puttana)",
        Potere.ImputatoVeggente => "Di chi vuoi sapere stanotte? (potere copiato della Veggente)",
        Potere.ImputatoSpia => "Chi spierai stanotte? (potere copiato della Spia)",
        Potere.ImputatoCacciatore => "Su chi piazzerai una trappola stanotte? (potere copiato del Cacciatore)",
        Potere.ImputatoGufo => "Chi manderai al ballottaggio stanotte? (potere copiato del Gufo)",
        _ => ""
    };

    public static string EtichettaPotere(Potere potere) => potere switch
    {
        Potere.Lupo => "Lupo",
        Potere.Guardiano => "Guardiano",
        Potere.Annullatore => "Annullatore",
        Potere.Becchino => "Becchino",
        Potere.Veggente => "Veggente",
        Potere.Puttana => "Puttana",
        Potere.Spia => "Spia",
        Potere.Cacciatore => "Cacciatore",
        Potere.Gufo => "Gufo",
        Potere.ImputatoLupo => "Imputato (Lupo)",
        Potere.ImputatoGuardiano => "Imputato (Guardiano)",
        Potere.ImputatoPuttana => "Imputato (Puttana)",
        Potere.ImputatoVeggente => "Imputato (Veggente)",
        Potere.ImputatoSpia => "Imputato (Spia)",
        Potere.ImputatoCacciatore => "Imputato (Cacciatore)",
        Potere.ImputatoGufo => "Imputato (Gufo)",
        Potere.Copione => "Copione",
        Potere.Copione2 => "Copione 2",
        _ => potere.ToString()
    };
}
