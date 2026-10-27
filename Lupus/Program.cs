using Lupus;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Runtime.CompilerServices;

//setup partita
#region
Console.WriteLine("Inserisci il numero di giocatori...");
string numberOfPlayers = Console.ReadLine();
bool inputValidoNumeri = false;
int numberOfPlayer = 0;
do
{
    try
    {
        numberOfPlayer = int.Parse(numberOfPlayers);
        inputValidoNumeri = true;
    }
    catch (Exception)
    {
        Console.WriteLine("Non hai inserito un numero intero di giocatori, riprova...");
        numberOfPlayers = Console.ReadLine();
    }
} while (!inputValidoNumeri);
Console.Clear();
Console.WriteLine("Inserisci i personaggi della partita...");
List<Personaggio> listaPersonaggi = new List<Personaggio>();
for (int i = 0; i < numberOfPlayer; i++)
{
    bool inputValido = false;
    do
    {
        string ruoloInserito = Console.ReadLine().ToLower();
        switch (ruoloInserito)
        {
            case "lupo":
                listaPersonaggi.Add(new Lupo());
                inputValido = true;
                break;
            case "puttana":
                listaPersonaggi.Add(new Puttana());
                inputValido = true;
                break;
            case "veggente":
                listaPersonaggi.Add(new Veggente());
                inputValido = true;
                break;
            case "guardiano":
                listaPersonaggi.Add(new Guardiano());
                inputValido = true;
                break;
            case "becchino":
                listaPersonaggi.Add(new Becchino());
                inputValido = true;
                break;
            case "annullatore":
                listaPersonaggi.Add(new Annullatore());
                inputValido = true;
                break;
            case "baby lupo":
                listaPersonaggi.Add(new BabyLupo());
                inputValido = true;
                break;
            case "babylupo":
                listaPersonaggi.Add(new BabyLupo());
                inputValido = true;
                break;
            case "copione":
                listaPersonaggi.Add(new Copione());
                inputValido = true;
                break;
            case "spia":
                listaPersonaggi.Add(new Spia());
                inputValido = true;
                break;
            case "imputato":
                listaPersonaggi.Add(new Imputato());
                inputValido = true;
                break;
            case "copione2":
                listaPersonaggi.Add(new Copione2());
                inputValido = true;
                break;
            case "diavolo":
                listaPersonaggi.Add(new Diavolo());
                inputValido = true;
                break;
            case "contadino":
                listaPersonaggi.Add(new Contadino());
                inputValido = true;
                break;
            case "vvelenatu":
                listaPersonaggi.Add(new Vvelenatu());
                inputValido = true;
                break;
            case "cacciatore":
                listaPersonaggi.Add(new Cacciatore());
                inputValido = true;
                break;
            case "kamikaze":
                listaPersonaggi.Add(new Kamikaze());
                inputValido = true;
                break;
            case "gufo":
                listaPersonaggi.Add(new Gufo());
                inputValido = true;
                break;
            case "sfigato":
                listaPersonaggi.Add(new Sfigato());
                inputValido = true;
                break;
            default:
                Console.WriteLine("Non hai inserito un ruolo valido, riprova...");
                break;
        }
    } while (!inputValido);
}
int vittoria = 2;
Console.Clear();
#endregion

//assegnazioni nomi ai ruoli
#region
Console.WriteLine("Buonanotte a tutti, vi sveglierò uno ad uno per conoscere la vostra identità...");
Thread.Sleep(5000);
Console.Clear();
foreach (Personaggio personaggio in listaPersonaggi)
{
    Console.WriteLine($"Si sveglia il personaggio {personaggio.GetType().Name}");
    bool nomeGiaUsato = false;
    do
    {
        Console.WriteLine($"Dimmi il tuo nome {personaggio.GetType().Name}");
        string inputNomePersona = Console.ReadLine();
        bool usato = false;
        foreach (Personaggio personaggio2 in listaPersonaggi)
        {
            if (personaggio2.nomePersona == inputNomePersona)
            {
                usato = true;  
            }
        }
        if (!usato)
        {
            personaggio.nomePersona = inputNomePersona;
            nomeGiaUsato = true;
        }
        else
        {
            Console.WriteLine("C'è già un personaggio con questo nome, scegli un nome diverso...");
        }
    } while (!nomeGiaUsato);

    
    //check capobranco
    #region
    if (personaggio is Lupo)
    {
        bool capobrancoPresente = false;
        int conteggioLupi = 0;
        foreach (Personaggio lupo in listaPersonaggi)
        {
            if (lupo is Lupo)
            {
                conteggioLupi++;
                if (lupo.capobranco)
                {
                    capobrancoPresente = true;
                }
            }
        }
        if (!capobrancoPresente && conteggioLupi > 1)
        {
            Console.WriteLine("Sei il capobranco?");
            bool inputValidoSiNo = false;
            string domandaCapobranco = "";
            do
            {
                domandaCapobranco = Console.ReadLine();
                if (domandaCapobranco == "si")
                {
                    personaggio.capobranco = true;
                    inputValidoSiNo = true;
                }
                else if (domandaCapobranco == "no")
                {
                    personaggio.capobranco = false;
                    inputValidoSiNo = true;
                }
                else
                {
                    Console.WriteLine("Rispondi si oppure no...");
                }
            }
            while (!inputValidoSiNo);
        }
        else
        {
            personaggio.capobranco = true;
        }
    }
    #endregion
    Console.WriteLine($"Buonanotte al personaggio {personaggio.GetType().Name}");
    Thread.Sleep(3000);
    Console.Clear();

    //personaggio.nomePersona = personaggio.GetType().Name.ToLower(); ///DA CANCELLAREEEEEEEEEEEE
}
#endregion

//turno
int turno = 1;
while (vittoria == 2)
{
    //check condizioni vittoria
    #region
    int counterLupi = 0;
    int counterAltri = 0;
    foreach (Personaggio personaggio in listaPersonaggi)
    {
        if (personaggio is Lupo && personaggio.morto == false)
        {
            counterLupi++;
        }
        else if (personaggio.morto == false)
        {
            counterAltri++;
        }
    }
    if (counterLupi == 0 && counterAltri == 0)
    {
        vittoria = 4;
        Console.WriteLine("Tutti i giocatori sono morti, la partita finisce in parità...");
        break;
    }
    else if (counterLupi == 0)
    {
        vittoria = 1;
        Console.WriteLine("I buoni vincono la partita!");
        break;
    }
    else if (counterLupi >= counterAltri)
    {
        vittoria = 0;
        Console.WriteLine("I lupi hanno vinto la partita...");
        break;
    }
    #endregion

    //turno notte

    #region
    Console.WriteLine($"TURNO {turno}");
    Thread.Sleep(2000);
    Console.WriteLine("E' notte e tutti dormono...");
    Thread.Sleep(5000);
    Console.Clear();

    //lista priorita

    #region
    List<int> listaPriorita = new List<int>();
    foreach (Personaggio personaggio in listaPersonaggi)
    {
        if (!listaPriorita.Contains(personaggio.priorita))
        {
            listaPriorita.Add(personaggio.priorita);
        }
        else if (listaPersonaggi[listaPriorita.IndexOf(personaggio.priorita)].morto)
        {
            listaPriorita[listaPriorita.IndexOf(personaggio.priorita)] = 1000;
            listaPriorita.Add(personaggio.priorita);
        }
        else
        {
            listaPriorita.Add(1000);
        }
    }
    #endregion

    //azioni
    while (listaPriorita.Min() < 1000)
    {
        Personaggio personaggioCorrente = listaPersonaggi[listaPriorita.IndexOf(listaPriorita.Min())];
        Console.WriteLine($"Si sveglia il personaggio {personaggioCorrente.GetType().Name}");
        //check capobranco
        #region
        if (personaggioCorrente is Lupo)
        {
            bool capobrancoPresente = false;
            int conteggioLupi = 0;
            foreach (Personaggio lupo in listaPersonaggi)
            {
                if (lupo is Lupo && !lupo.morto)
                {
                    conteggioLupi++;
                    if (lupo.capobranco)
                    {
                        capobrancoPresente = true;
                    }
                }
            }
            if (!capobrancoPresente && conteggioLupi > 1)
            {
                do
                {
                    foreach (Personaggio lupo in listaPersonaggi)
                    {
                        bool capobrancoPresente2 = false;
                        foreach (Personaggio lupo2 in listaPersonaggi)
                        {
                            if (lupo2 is Lupo && !lupo2.morto)
                            {
                                if (lupo2.capobranco)
                                {
                                    capobrancoPresente2 = true;
                                }
                            }
                        }
                        if (lupo is Lupo && lupo.morto == false && !capobrancoPresente2)
                        {
                            Console.WriteLine($"{lupo.nomePersona} sei il nuovo capobranco?");
                            string rispostaCapobranco = "";
                            bool inputValidoNoSi = false;
                            do
                            {
                                rispostaCapobranco = Console.ReadLine();
                                if (rispostaCapobranco == "si")
                                {
                                    lupo.capobranco = true;
                                    capobrancoPresente = true;
                                    inputValidoNoSi = true;
                                }
                                else if (rispostaCapobranco == "no")
                                {
                                    lupo.capobranco = false;
                                    inputValidoNoSi = true;
                                }
                                else
                                {
                                    Console.WriteLine("Rispondi con si oppure no...");
                                }
                            }
                            while (!inputValidoNoSi);
                        }
                    }
                    if (!capobrancoPresente)
                    {
                        Console.WriteLine("Non avete scelto un capobranco fra voi, sceglietene uno...");
                    }
                }
                while (!capobrancoPresente);
            }
            else
            {
                personaggioCorrente.capobranco = true;
            }
        }
        #endregion
        if (!personaggioCorrente.morto)
        {
            if (!personaggioCorrente.puntatoDallAnnullatore || (personaggioCorrente is Lupo && counterLupi > 1))
            {
                personaggioCorrente.Agisci(listaPersonaggi);
            }
            else
            {
                Console.WriteLine("Sei stato annullato per questa notte!");
            }
        }
        else
        {
            personaggioCorrente.AgisciDaMorto();
        }
        listaPriorita[listaPriorita.IndexOf(listaPriorita.Min())] = 1000;
        Thread.Sleep(1000);
        Console.WriteLine($"Buonanotte al personaggio {personaggioCorrente.GetType().Name}");
        Thread.Sleep(3000);
        Console.Clear();
    }
    #endregion

    //turno giorno

    #region
    Thread.Sleep(3000);
    Console.WriteLine("Buongiorno a tutti!");

    var listaPersonaggiCopia = new List<Personaggio>(listaPersonaggi);
    bool mortiQuestoTurno = false;
    bool lupoCadutoInTrappola = false;
    bool imputatoCadutoInTrappola = false;
    foreach (Personaggio personaggio in listaPersonaggiCopia)
    {
        if (personaggio.puntatoDalLupo && (personaggio.puntatoDalCacciatore || personaggio.puntatoDallImputatoComeCacciatore))
        {
            lupoCadutoInTrappola = true;
        }
        if ((personaggio is Kamikaze) && personaggio.puntatoDalLupo && !personaggio.puntatoDalGuardiano && !personaggio.puntatoDallImputatoComeGuardiano)
        {
            lupoCadutoInTrappola = true;
        }
        if (personaggio.puntatoDallImputatoComeLupo && (personaggio.puntatoDalCacciatore || personaggio.puntatoDallImputatoComeCacciatore))
        {
            imputatoCadutoInTrappola = true;
        }
        if ((personaggio is Kamikaze) && personaggio.puntatoDallImputatoComeLupo && !personaggio.puntatoDalGuardiano && !personaggio.puntatoDallImputatoComeGuardiano)
        {
            imputatoCadutoInTrappola = true;
        }
    }
    foreach (Personaggio personaggio in listaPersonaggiCopia)
    {
        if (personaggio is BabyLupo)
        {
            personaggio.MuoriBaby(listaPersonaggi);
        }
        else if (personaggio.capobranco && lupoCadutoInTrappola && !personaggio.puntatoDalGuardiano && !personaggio.puntatoDallImputatoComeGuardiano)
        {
            personaggio.morto = true;
            personaggio.mortoQuestoTurno = true;
            personaggio.capobranco = false;
            Console.WriteLine($"Stanotte ci ha lasciato {personaggio.nomePersona}...");
        }
        else if ((personaggio is Imputato) && imputatoCadutoInTrappola && !personaggio.puntatoDalGuardiano && !personaggio.puntatoDallImputatoComeGuardiano)
        {
            personaggio.morto = true;
            personaggio.mortoQuestoTurno = true;
            Console.WriteLine($"Stanotte ci ha lasciato {personaggio.nomePersona}...");
        }
        else
        {
            personaggio.Muori();
        }
        if (personaggio.mortoQuestoTurno)
        {
            mortiQuestoTurno = true;
        }
        personaggio.puntatoDalLupo = false;
        personaggio.puntatoDalGuardiano = false;
        personaggio.puntatoDallAnnullatore = false;
        personaggio.mortoQuestoTurno = false;
        personaggio.puntatoDalBecchino = false;
        personaggio.puntatoDallaVeggente = false;
        personaggio.puttanaAndataConUnLupo = false;
        personaggio.puntatoDallaPuttana = false;
        personaggio.puntatoDalCopione = false;
        personaggio.puntatoDallaSpia = false;
        personaggio.puntatoDallImputatoComeLupo = false;
        personaggio.puntatoDallImputatoComeGuardiano = false;
        personaggio.puntatoDallImputatoComePuttana = false;
        personaggio.puntatoDallImputatoComeVeggente = false;
        personaggio.puntatoDallImputatoComeSpia = false;
        personaggio.puntatoDallImputatoComeCacciatore = false;
        personaggio.puntatoDalCopione2 = false;
        personaggio.diavoloSbranato = false;
        personaggio.puntatoDalCacciatore = false;
    }
    if (!mortiQuestoTurno)
    {
        Console.WriteLine("Stanotte non è morto nessuno!");
    }
    Thread.Sleep(3000);

    //check condizioni vittoria
    #region
    counterLupi = 0;
    counterAltri = 0;
    foreach (Personaggio personaggio in listaPersonaggi)
    {
        if (personaggio is Lupo && personaggio.morto == false)
        {
            counterLupi++;
        }
        else if (personaggio.morto == false)
        {
            counterAltri++;
        }
    }
    if (counterLupi == 0 && counterAltri == 0)
    {
        vittoria = 4;
        Console.WriteLine("Tutti i giocatori sono morti, la partita finisce in parità...");
        break;
    }
    if (counterLupi == 0)
    {
        vittoria = 1;
        Console.WriteLine("I buoni vincono la partita!");
        break;
    }
    if (counterLupi >= counterAltri)
    {
        vittoria = 0;
        Console.WriteLine("I lupi hanno vinto la partita...");
        break;
    }
   
    #endregion

    Console.WriteLine("E' aperta la discussione per chi mandare al rogo...");
    Thread.Sleep(1000);
    Console.WriteLine("Scrivete il nome di chi è stato votato...");
    string personaggioVotato = Console.ReadLine();
    personaggioVotato = personaggioVotato.ToLower();
    bool inputValido = false;
    do
    {
        foreach (Personaggio personaggio in listaPersonaggi)
        {
            if (personaggio.nomePersona == personaggioVotato)
            {
                inputValido = true;
            }
        }
        if (!inputValido)
        {
            Console.WriteLine("Il nome che avete scritto non è tra quello delle persone che stanno giocando, riprovate...");
            personaggioVotato = Console.ReadLine();
        }
    } while (!inputValido);
    bool gufato = false;
    foreach (Personaggio personaggio in listaPersonaggi)
    {
        if (personaggio.puntatoDalGufo)
        {
            Console.WriteLine($"{personaggio.nomePersona} è stato puntato dal gufo, ed andrà al ballottaggio con {personaggioVotato}");
            gufato = true;
        }
        if (personaggio.puntatoDallImputatoComeGufo)
        {
            Console.WriteLine($"{personaggio.nomePersona} è stato puntato dal gufo, ed andrà al ballottaggio con {personaggioVotato}");
            gufato = true;
        }
    }
    if (gufato)
    {
        Console.WriteLine("Scrivete il nome di chi è stato votato...");
        personaggioVotato = Console.ReadLine();
    }
    foreach (Personaggio personaggio in listaPersonaggi)
    {
        personaggio.puntatoDalGufo = false;
        personaggio.puntatoDallImputatoComeGufo = false;
        if (personaggio.nomePersona == personaggioVotato)
        {
            personaggio.morto = true;
            if (personaggio.capobranco)
            {
                personaggio.capobranco = false;
            }
            Console.WriteLine($"{personaggio.nomePersona} è andanto al rogo.");
            if (personaggio.diavoloSbranato)
            {
                Thread.Sleep(3000);
                Console.Clear();
                Console.WriteLine("Il diavolo è stato sbranato e votato lo stesso turno e vince la partita!");
                vittoria = 3;
            }
        }
        personaggio.diavoloSbranato = false;
    }

    #endregion
    turno++;
    Thread.Sleep(5000);
    if (vittoria != 3)
    {
        Console.Clear();
    }
}

Console.ReadLine();







