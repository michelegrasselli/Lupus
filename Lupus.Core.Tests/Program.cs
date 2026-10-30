using Lupus.Core;

int passati = 0;
int falliti = 0;

var tests = new (string Nome, Action Test)[]
{
    ("Notte base: protezione del Guardiano", NotteBaseProtezione),
    ("Notte base: morte senza protezione", NotteSenzaProtezione),
    ("Trappola: salva la vittima e uccide il capobranco", Trappola),
    ("Kamikaze: esplode e uccide il capobranco", Kamikaze),
    ("Baby Lupo: diventa Lupo", BabyLupo),
    ("Puttana: muore con un Lupo", PuttanaConLupo),
    ("Annullatore: blocca la Veggente", Annullatore),
    ("Diavolo: sopravvive e vince se votato", Diavolo),
    ("Copione: copia il ruolo", Copione),
    ("Gufo: ballottaggio", GufoBallottaggio),
    ("Imputato: copia i poteri", Imputato),
    ("Serializzazione: salva e ricarica", Salvataggio),
    ("Validazione: composizioni", Validazione),
    ("Vittoria: condizioni base", Vittoria)
};

foreach (var (nome, test) in tests)
{
    try
    {
        test();
        Console.WriteLine($"  OK  {nome}");
        passati++;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  KO  {nome}: {ex.Message}");
        falliti++;
    }
}

Console.WriteLine();
Console.WriteLine($"{passati} test superati, {falliti} falliti.");
return falliti == 0 ? 0 : 1;

// ---------------------------------------------------------------------------
// Helper
// ---------------------------------------------------------------------------

static Partita Crea(params (string Nome, Ruolo Ruolo)[] giocatori)
{
    var p = new Partita();
    foreach (var (nome, ruolo) in giocatori)
    {
        p.Giocatori.Add(new Giocatore { Nome = nome, Ruolo = ruolo });
    }

    var lupo = p.Giocatori.FirstOrDefault(g => g.Ruolo == Ruolo.Lupo);
    if (lupo != null) lupo.Capobranco = true;

    MotoreLupus.Avvia(p);
    return p;
}

static void Notte(Partita p)
{
    MotoreLupus.IniziaNotte(p);
    Require(p.Fase == FasePartita.Notte, "La partita non è entrata in fase notte.");
}

static void EseguiNotte(Partita p, Func<PassoNotte, string?> scelta)
{
    int guardia = 0;

    while (!p.NotteCompleta)
    {
        Require(guardia++ < 200, "Troppi passi notturni: possibile ciclo infinito.");

        var passo = p.PassoCorrente;
        Require(passo != null, "Nessun passo corrente nonostante la notte non sia completa.");

        var targetId = scelta(passo) ?? passo.OpzioniIds.FirstOrDefault();
        Require(targetId != null, $"Nessun bersaglio disponibile per {passo.Titolo}.");

        var esito = MotoreLupus.EseguiPasso(p, passo.Id, targetId);
        Require(esito.Ok, $"Passo '{passo.Titolo}': {esito.Errore}");

        if (!passo.Completato)
            MotoreLupus.ConfermaRisultato(p, passo.Id);
    }

    var risultato = MotoreLupus.RisolviNotte(p);
    Require(risultato.Ok, $"Risoluzione notte: {risultato.Errore}");
}

static string? Nome(Partita p, string nome) => p.PerNome(nome)?.Id;

static void Require(bool condizione, string messaggio)
{
    if (!condizione) throw new Exception(messaggio);
}

static Giocatore G(Partita p, string nome) =>
    p.PerNome(nome) ?? throw new Exception($"Giocatore '{nome}' non trovato.");

// ---------------------------------------------------------------------------
// Test
// ---------------------------------------------------------------------------

static void NotteBaseProtezione()
{
    var p = Crea(
        ("Lupo", Ruolo.Lupo),
        ("Veggente", Ruolo.Veggente),
        ("Guardiano", Ruolo.Guardiano),
        ("Anna", Ruolo.Contadino),
        ("Bruno", Ruolo.Contadino),
        ("Carla", Ruolo.Contadino));

    Notte(p);
    EseguiNotte(p, passo => passo.Potere switch
    {
        Potere.Lupo => Nome(p, "Anna"),
        Potere.Guardiano => Nome(p, "Anna"),
        Potere.Veggente => Nome(p, "Lupo"),
        _ => null
    });

    Require(G(p, "Anna").Vivo, "Anna doveva essere protetta.");
    Require(p.RisultatoNotte!.NessunMorto, "Non doveva morire nessuno.");
    Require(p.PassiNotte.Any(x => x.Potere == Potere.Veggente && x.Risultato?.Contains("CATTIVO") == true),
        "La Veggente doveva vedere il Lupo come CATTIVO.");
}

static void NotteSenzaProtezione()
{
    var p = Crea(
        ("Lupo", Ruolo.Lupo),
        ("Veggente", Ruolo.Veggente),
        ("Guardiano", Ruolo.Guardiano),
        ("Anna", Ruolo.Contadino),
        ("Bruno", Ruolo.Contadino),
        ("Carla", Ruolo.Contadino));

    Notte(p);
    EseguiNotte(p, passo => passo.Potere switch
    {
        Potere.Lupo => Nome(p, "Anna"),
        Potere.Guardiano => Nome(p, "Bruno"),
        Potere.Veggente => Nome(p, "Lupo"),
        _ => null
    });

    Require(!G(p, "Anna").Vivo, "Anna doveva morire.");
    Require(p.RisultatoNotte!.MortiIds.Contains(G(p, "Anna").Id), "Anna doveva essere nel report.");
}

static void Trappola()
{
    var p = Crea(
        ("Lupo", Ruolo.Lupo),
        ("Cacciatore", Ruolo.Cacciatore),
        ("Veggente", Ruolo.Veggente),
        ("Anna", Ruolo.Contadino),
        ("Bruno", Ruolo.Contadino),
        ("Carla", Ruolo.Contadino));

    Notte(p);
    EseguiNotte(p, passo => passo.Potere switch
    {
        Potere.Cacciatore => Nome(p, "Anna"),
        Potere.Lupo => Nome(p, "Anna"),
        Potere.Veggente => Nome(p, "Lupo"),
        _ => null
    });

    Require(G(p, "Anna").Vivo, "La trappola doveva salvare Anna.");
    Require(!G(p, "Lupo").Vivo, "Il capobranco doveva morire nella trappola.");
    Require(p.Esito == EsitoPartita.Buoni, "Senza lupi, i buoni dovevano vincere.");
}

static void Kamikaze()
{
    var p = Crea(
        ("Lupo", Ruolo.Lupo),
        ("Kamikaze", Ruolo.Kamikaze),
        ("Veggente", Ruolo.Veggente),
        ("Anna", Ruolo.Contadino),
        ("Bruno", Ruolo.Contadino),
        ("Carla", Ruolo.Contadino));

    Notte(p);
    EseguiNotte(p, passo => passo.Potere switch
    {
        Potere.Lupo => Nome(p, "Kamikaze"),
        Potere.Veggente => Nome(p, "Lupo"),
        _ => null
    });

    Require(G(p, "Kamikaze").Vivo, "Il Kamikaze doveva sopravvivere.");
    Require(!G(p, "Lupo").Vivo, "Il capobranco doveva morire nell'esplosione.");
}

static void BabyLupo()
{
    var p = Crea(
        ("Lupo", Ruolo.Lupo),
        ("Cucciolo", Ruolo.BabyLupo),
        ("Veggente", Ruolo.Veggente),
        ("Anna", Ruolo.Contadino),
        ("Bruno", Ruolo.Contadino),
        ("Carla", Ruolo.Contadino));

    Notte(p);
    EseguiNotte(p, passo => passo.Potere switch
    {
        Potere.Lupo => Nome(p, "Cucciolo"),
        Potere.Veggente => Nome(p, "Lupo"),
        _ => null
    });

    Require(G(p, "Cucciolo").Vivo, "Il Baby Lupo non doveva morire.");
    Require(G(p, "Cucciolo").Ruolo == Ruolo.Lupo, "Il Baby Lupo doveva trasformarsi.");
    Require(p.RisultatoNotte!.TrasformatiIds.Contains(G(p, "Cucciolo").Id), "Trasformazione assente dal report.");
}

static void PuttanaConLupo()
{
    var p = Crea(
        ("Lupo", Ruolo.Lupo),
        ("Puttana", Ruolo.Puttana),
        ("Veggente", Ruolo.Veggente),
        ("Anna", Ruolo.Contadino),
        ("Bruno", Ruolo.Contadino),
        ("Carla", Ruolo.Contadino));

    Notte(p);
    EseguiNotte(p, passo => passo.Potere switch
    {
        Potere.Lupo => Nome(p, "Anna"),
        Potere.Puttana => Nome(p, "Lupo"),
        Potere.Veggente => Nome(p, "Lupo"),
        _ => null
    });

    Require(!G(p, "Puttana").Vivo, "La Puttana doveva morire dopo la notte con il Lupo.");
    Require(!G(p, "Anna").Vivo, "Anna doveva essere sbranata.");

    // Privacy: la Puttana non deve sapere di aver scelto un Lupo, né che
    // morirà. Il dettaglio resta nel diario del narratore.
    var passoPuttana = p.PassiNotte.First(x => x.Potere == Potere.Puttana);
    var risultato = passoPuttana.Risultato ?? "";

    Require(risultato.Contains(G(p, "Lupo").Nome),
        "La Puttana deve comunque sapere chi ha scelto.");
    Require(!risultato.Contains("è un Lupo", StringComparison.OrdinalIgnoreCase),
        $"Il risultato della Puttana rivela il Lupo: '{risultato}'");
    Require(!risultato.Contains("morirà", StringComparison.OrdinalIgnoreCase),
        $"Il risultato della Puttana anticipa la morte: '{risultato}'");
    Require(!risultato.Contains("Attenzione", StringComparison.OrdinalIgnoreCase),
        $"Il risultato della Puttana è troppo allarmante: '{risultato}'");

    Require(p.Log.Any(v => v.Testo.Contains("Solo narratore", StringComparison.OrdinalIgnoreCase)),
        "Il narratore deve comunque vedere nel diario che il bersaglio era un Lupo.");
}

static void Annullatore()
{
    var p = Crea(
        ("Lupo", Ruolo.Lupo),
        ("Annullatore", Ruolo.Annullatore),
        ("Veggente", Ruolo.Veggente),
        ("Anna", Ruolo.Contadino),
        ("Bruno", Ruolo.Contadino),
        ("Carla", Ruolo.Contadino));

    Notte(p);
    EseguiNotte(p, passo => passo.Potere switch
    {
        Potere.Annullatore => Nome(p, "Veggente"),
        Potere.Lupo => Nome(p, "Anna"),
        Potere.Veggente => Nome(p, "Lupo"),
        _ => null
    });

    var passoVeggente = p.PassiNotte.First(x => x.Potere == Potere.Veggente);
    Require(passoVeggente.Saltato, "La Veggente doveva essere annullata.");
    Require(passoVeggente.Risultato?.Contains("annullato") == true, "Manca il messaggio di annullamento.");
    Require(!p.AzioniNotte.Any(x => x.Potere == Potere.Veggente), "La Veggente non doveva registrare azioni.");
    Require(!G(p, "Anna").Vivo, "Anna doveva morire.");
}

static void Diavolo()
{
    var p = Crea(
        ("Lupo", Ruolo.Lupo),
        ("Diavolo", Ruolo.Diavolo),
        ("Veggente", Ruolo.Veggente),
        ("Anna", Ruolo.Contadino),
        ("Bruno", Ruolo.Contadino),
        ("Carla", Ruolo.Contadino));

    Notte(p);
    EseguiNotte(p, passo => passo.Potere switch
    {
        Potere.Lupo => Nome(p, "Diavolo"),
        Potere.Veggente => Nome(p, "Diavolo"),
        _ => null
    });

    Require(G(p, "Diavolo").Vivo, "Il Diavolo doveva sopravvivere allo sbrano.");
    Require(G(p, "Diavolo").DiavoloSbranato, "Il flag DiavoloSbranato doveva essere attivo.");
    Require(p.RisultatoNotte!.NessunMorto, "Non doveva morire nessuno.");

    MotoreLupus.VaiAlGiorno(p);
    MotoreLupus.ApriVoto(p);
    var esito = MotoreLupus.Vota(p, G(p, "Diavolo").Id);
    Require(esito.Ok, esito.Errore);
    Require(p.Esito == EsitoPartita.Diavolo, "Il Diavolo doveva vincere.");
}

static void Copione()
{
    var p = Crea(
        ("Lupo", Ruolo.Lupo),
        ("Copione", Ruolo.Copione),
        ("Veggente", Ruolo.Veggente),
        ("Anna", Ruolo.Contadino),
        ("Bruno", Ruolo.Contadino),
        ("Carla", Ruolo.Contadino));

    Notte(p);
    EseguiNotte(p, passo => passo.Potere switch
    {
        Potere.Lupo => Nome(p, "Anna"),
        Potere.Veggente => Nome(p, "Lupo"),
        Potere.Copione => Nome(p, "Veggente"),
        _ => null
    });

    Require(G(p, "Copione").Vivo, "Il Copione doveva essere vivo.");
    Require(G(p, "Copione").Ruolo == Ruolo.Veggente, "Il Copione doveva diventare Veggente.");
    Require(G(p, "Copione").Trasformato, "Il flag Trasformato doveva essere attivo.");
}

static void GufoBallottaggio()
{
    var p = Crea(
        ("Lupo", Ruolo.Lupo),
        ("Gufo", Ruolo.Gufo),
        ("Veggente", Ruolo.Veggente),
        ("Anna", Ruolo.Contadino),
        ("Bruno", Ruolo.Contadino),
        ("Carla", Ruolo.Contadino));

    Notte(p);
    EseguiNotte(p, passo => passo.Potere switch
    {
        Potere.Lupo => Nome(p, "Bruno"),
        Potere.Gufo => Nome(p, "Anna"),
        Potere.Veggente => Nome(p, "Lupo"),
        _ => null
    });

    Require(p.BallottaggioIds.Contains(G(p, "Anna").Id), "Anna doveva essere mandata al ballottaggio.");

    MotoreLupus.VaiAlGiorno(p);
    MotoreLupus.ApriVoto(p);
    var esito = MotoreLupus.Vota(p, G(p, "Carla").Id);
    Require(esito.Ok, esito.Errore);
    Require(p.Fase == FasePartita.Ballottaggio, "Doveva iniziare il ballottaggio.");

    esito = MotoreLupus.ScegliBallottaggio(p, G(p, "Anna").Id);
    Require(esito.Ok, esito.Errore);
    Require(!G(p, "Anna").Vivo, "Anna doveva andare al rogo.");
}

static void Imputato()
{
    var p = Crea(
        ("Lupo", Ruolo.Lupo),
        ("Imputato", Ruolo.Imputato),
        ("Veggente", Ruolo.Veggente),
        ("Guardiano", Ruolo.Guardiano),
        ("Anna", Ruolo.Contadino),
        ("Bruno", Ruolo.Contadino),
        ("Carla", Ruolo.Contadino));

    Notte(p);
    EseguiNotte(p, passo => passo.Potere switch
    {
        Potere.Lupo => Nome(p, "Imputato"),
        Potere.Guardiano => Nome(p, "Imputato"),
        Potere.Veggente => Nome(p, "Imputato"),
        Potere.ImputatoLupo => Nome(p, "Anna"),
        Potere.ImputatoGuardiano => Nome(p, "Bruno"),
        Potere.ImputatoVeggente => Nome(p, "Lupo"),
        _ => null
    });

    Require(G(p, "Imputato").Vivo, "L'Imputato doveva essere protetto.");
    Require(!G(p, "Anna").Vivo, "Anna doveva morire per il potere copiato del Lupo.");
    Require(G(p, "Bruno").Vivo, "Bruno doveva essere protetto dal potere copiato.");

    var passiImputato = p.PassiNotte.Where(x => x.AttoreId == G(p, "Imputato").Id).ToList();
    Require(passiImputato.Count == 3, $"L'Imputato doveva avere 3 poteri, ne ha {passiImputato.Count}.");
    Require(passiImputato.Any(x => x.Potere == Potere.ImputatoVeggente &&
                                   x.Risultato?.Contains("CATTIVO") == true),
        "Il potere copiato della Veggente doveva vedere il Lupo come CATTIVO.");
}

static void Salvataggio()
{
    var p = Crea(
        ("Lupo", Ruolo.Lupo),
        ("Veggente", Ruolo.Veggente),
        ("Guardiano", Ruolo.Guardiano),
        ("Anna", Ruolo.Contadino),
        ("Bruno", Ruolo.Contadino),
        ("Carla", Ruolo.Contadino));

    Notte(p);
    EseguiNotte(p, passo => passo.Potere switch
    {
        Potere.Lupo => Nome(p, "Anna"),
        Potere.Guardiano => Nome(p, "Bruno"),
        Potere.Veggente => Nome(p, "Lupo"),
        _ => null
    });

    var json = Serializzazione.Serializza(p);
    var ricaricata = Serializzazione.Deserializza(json);
    Require(ricaricata != null, "La partita non è stata deserializzata.");
    Require(ricaricata!.Giocatori.Count == p.Giocatori.Count, "Numero giocatori diverso.");
    Require(ricaricata.Turno == p.Turno, "Turno diverso.");
    Require(ricaricata.Fase == p.Fase, "Fase diversa.");
    Require(ricaricata.Log.Count == p.Log.Count, "Log diverso.");
    Require(ricaricata.RisultatoNotte?.MortiIds.Count == p.RisultatoNotte?.MortiIds.Count, "Report diverso.");
}

static void Validazione()
{
    var composizione = new Dictionary<Ruolo, int>
    {
        [Ruolo.Lupo] = 2,
        [Ruolo.Veggente] = 1,
        [Ruolo.Contadino] = 3
    };

    var errori = CreazionePartita.Valida(6, composizione, new[] { "A", "B", "C", "D", "E", "F" });
    Require(errori.Count == 0, $"Composizione valida ma errori: {string.Join(" ", errori)}");

    var troppiLupi = new Dictionary<Ruolo, int>
    {
        [Ruolo.Lupo] = 4,
        [Ruolo.Contadino] = 2
    };
    errori = CreazionePartita.Valida(6, troppiLupi, new[] { "A", "B", "C", "D", "E", "F" });
    Require(errori.Count > 0, "Una composizione con troppi lupi doveva essere rifiutata.");

    var nomiDuplicati = CreazionePartita.Valida(6, composizione, new[] { "A", "A", "C", "D", "E", "F" });
    Require(nomiDuplicati.Count > 0, "I nomi duplicati dovevano essere rifiutati.");
}

static void Vittoria()
{
    var p = new Partita();
    p.Giocatori.Add(new Giocatore { Nome = "Lupo", Ruolo = Ruolo.Lupo });
    p.Giocatori.Add(new Giocatore { Nome = "Anna", Ruolo = Ruolo.Contadino });
    MotoreLupus.Avvia(p);
    Require(p.Esito == EsitoPartita.Lupi, "Con 1 lupo e 1 buono dovevano vincere i lupi.");

    var p2 = new Partita();
    p2.Giocatori.Add(new Giocatore { Nome = "Anna", Ruolo = Ruolo.Contadino });
    p2.Giocatori.Add(new Giocatore { Nome = "Bruno", Ruolo = Ruolo.Contadino });
    MotoreLupus.Avvia(p2);
    Require(p2.Esito == EsitoPartita.Buoni, "Senza lupi dovevano vincere i buoni.");
}
