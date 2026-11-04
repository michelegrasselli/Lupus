# 🐺 Lupus in Tabula · Multiplayer

Applicazione web per giocare a **Lupus in Tabula** con i telefoni: il narratore conduce
la partita da un dispositivo, ogni giocatore entra dal proprio telefono — anche da
un'altra rete — e vede solo il proprio ruolo, le proprie azioni notturne e il proprio voto.

Niente carta, niente appunti, niente account: la stanza vive sul server e si raggiunge
con un codice a 5 lettere.

## Requisiti

- [.NET SDK 10.0.200](https://dotnet.microsoft.com/) o superiore (target `net10.0`).
- Un browser moderno su narratore e giocatori.
- I telefoni possono stare sulla **stessa Wi-Fi** del narratore oppure usare il **link
  pubblico** del tunnel: il gioco funziona anche in 4G.

Nessun database, nessun account, nessun package NuGet esterno.

## Avvio rapido

Dalla root del repository:

```powershell
# Avvia l'app web (profilo HTTP, senza certificati)
dotnet run --project Lupus.Web --launch-profile http
```

L'app ascolta su `http://0.0.0.0:5092`:

- narratore: <http://localhost:5092>
- telefoni, stessa Wi-Fi: `http://<IP-del-PC>:5092` (l'app mostra l'indirizzo corretto nella home)
- telefoni, qualsiasi rete: l'URL `https://...` del tunnel, indicato come **Link pubblico**
  nella lobby del narratore

### Accesso pubblico con Cloudflare Tunnel

L'app resta accesa sul PC di casa e `cloudflared` apre una connessione **in uscita** verso
Cloudflare: non serve port forwarding, IP statico o porta aperta nel firewall. Cloudflare
inoltra le richieste a Kestrel su `http://localhost:5092`.

Requisiti: PC accesso, corrente elettrica e internet di casa attiva. Se internet cade, il
tunnel si chiude e restano validi solo i link di rete locale.

**Fase 1 · quick tunnel (prova veloce)**

```powershell
cloudflared tunnel --url http://localhost:5092
```

cloudflared stampa un URL `https://<parole-a-caso>.trycloudflare.com`. Non richiede account e
può bastare anche per la serata della partita, **a patto di non riavviare cloudflared**:
l'URL cambia solo quando il tunnel riparte. Per un indirizzo stabile nel tempo serve il
named tunnel.

Se non hai un dominio, il quick tunnel è la strada più rapida. Un dominio `.it`/`.com` costa
circa 10 € all'anno e si sposta su Cloudflare gratis (cambiando i nameserver).

**Fase 2 · named tunnel (URL fisso)**

Serve un dominio gestito da Cloudflare. Una volta sola:

```powershell
cloudflared tunnel login
cloudflared tunnel create lupus
cloudflared tunnel route dns lupus lupus.<tuo-dominio>
```

Poi un file `%USERPROFILE%\.cloudflared\config.yml`:

```yaml
tunnel: lupus
credentials-file: C:\Users\<utente>\.cloudflared\<id-tunnel>.json

ingress:
  - hostname: lupus.<tuo-dominio>
    service: http://localhost:5092
  - service: http_status:404
```

Avvio e installazione come servizio Windows (parte da solo all'accensione del PC):

```powershell
cloudflared tunnel run lupus                        # prova manuale
cloudflared service install                          # servizio, usa config.yml
sc.exe query cloudflared                             # stato del servizio
```

Con il named tunnel l'URL è sempre lo stesso; il link mostrato nella lobby usa da solo
l'host pubblico (arriva da `NavigationManager.BaseUri`), quindi non va aggiornato a mano.

**Cosa cambia tra i due**

| | Quick tunnel | Named tunnel |
| --- | --- | --- |
| URL | casuale, cambia a ogni avvio | fisso sul tuo dominio |
| Account Cloudflare | non serve | serve, con dominio |
| Uso consigliato | prove e serata senza dominio | URL stabile nel tempo |
| Limiti | cambia URL a ogni riavvio del tunnel | serve un dominio su Cloudflare |

Il piano gratuito Cloudflare non ha costi per questo uso, ma non offre SLA: il tunnel può
cadere se cade internet o se Cloudflare ha un disservizio. In quel caso chi è in casa può
sempre usare il link di rete locale mostrato sotto quello pubblico.

**Sovrascrivere la porta di Kestrel**

`appsettings.json` imposta `"Urls": "http://0.0.0.0:5092"`. Per un uso solo-tunnel puoi
legare Kestrel al loopback senza toccare il file:

```powershell
$env:ASPNETCORE_URLS = "http://127.0.0.1:5092"
dotnet run --project Lupus.Web --no-launch-profile
```

`ASPNETCORE_URLS` vince su `"Urls"`; `dotnet run --launch-profile http` continua a usare
`launchSettings.json` e resta il comando comodo per lo sviluppo in LAN.

### Eseguibile (publish)

Per ottenere un `.exe` avviabile con doppio clic (con CSS, JavaScript e Blazor inclusi):

```powershell
dotnet publish Lupus.Web/Lupus.Web.csproj -c Release
```

Poi avvia:

```powershell
.\Lupus.Web\bin\Release\net10.0\publish\Lupus.Web.exe
```

> **Attenzione:** non avviare l'exe dentro `bin\Debug\net10.0`. Quella cartella è
> l'output intermedio di build: non contiene `wwwroot` né `_framework`, quindi l'app
> parte sulla porta 5000 e serve CSS/JS vuoti o in errore. Usa `dotnet run` per lo
> sviluppo oppure `dotnet publish` per l'eseguibile.

### Firewall di Windows

Se i telefoni in Wi-Fi non raggiungono il PC, apri la porta TCP 5092 (PowerShell da
amministratore):

```powershell
New-NetFirewallRule -DisplayName "Lupus 5092" -Direction Inbound -Protocol TCP -LocalPort 5092 -Action Allow -Profile Private
```

Con Cloudflare Tunnel la regola **non serve**: `cloudflared` si collega in uscita e
raggiunge Kestrel su `localhost`.

### Build e test

```powershell
# Build dell'intera soluzione
dotnet build Lupus.slnx

# Test del motore di gioco (25 scenari)
dotnet run --project Lupus.Core.Tests/Lupus.Core.Tests.csproj
```

## Come si gioca

1. **Crea la stanza** (`/`): imposta il numero di giocatori, la composizione dei ruoli e i
   nomi. Il server assegna subito i ruoli (mescolandoli), ma i telefoni non li vedono finché
   non avvii. Ogni riga dei nomi ha un pulsante **👤 Umano / 🤖 AI**: se al tavolo siete in
   tre ma volete giocare in cinque, lasciate due posti all'AI. Puoi aggiungere AI anche
   dalla lobby, con **🤖 AI** sui posti liberi.
2. **Fai entrare i giocatori**: mostra il **Link pubblico** della lobby (o il link di rete
   locale se siete tutti sulla stessa Wi-Fi) e il codice a 5 lettere. Ogni giocatore tocca
   il proprio nome e reclama il posto. I posti AI nascono già occupati e non vanno reclamati.
3. **Avvia la partita** dal pannello del narratore: ogni telefono mostra il proprio ruolo;
   anche gli AI ricevono il loro, ma restano sul server.
4. **Notte**: il motore genera i passi in ordine di priorità. Il giocatore di turno sceglie
   il bersaglio dal telefono; il narratore vede lo stato e può sempre agire al posto suo.
5. **Alba**: eventi pubblici e caduti. Il narratore decide se rivelare ai telefoni i ruoli
   dei morti (utile per il Becchino) con l'apposito interruttore.
6. **Giorno / Voto**: il villaggio discute con il timer. Se al tavolo c'è almeno un AI,
   nella fase di giorno compare anche **la piazza**: gli AI commentano da soli, gli umani
   possono fare domande o raccontare informazioni, e gli AI decidono se fidarsi. Poi il voto
   si raccoglie dai telefoni con conteggio in tempo reale. In caso di pareggio si vota di
   nuovo solo tra i più votati; se resta il pareggio nessuno va al rogo. Il narratore può
   forzare o saltare il rogo.
7. **Ballottaggio**: se il Gufo ha aggiunto candidati, i telefoni votano tra loro; anche qui
   un pareggio significa nessun rogo.
8. **Esecuzione**: il narratore annuncia il ruolo del condannato (se c'è) e cala la notte.
9. **Vittoria**: tutti i ruoli vengono rivelati a tutti i telefoni.

### Cosa vede ogni telefono

- Il **proprio ruolo**, la fazione e i compagni di branco (solo per i lupi).
- Solo le **proprie azioni notturne** e i relativi risultati, mostrati nella card del passo
  mentre accade. Non c'è nessun quaderno: ricordare le informazioni raccolte è compito del
  giocatore.
- Lo **stato pubblico**: vivi, morti, eventi dell'alba, candidati al ballottaggio.
- Mai i ruoli degli altri giocatori vivi.

### Giocatori AI

Un posto AI è affidato al server: agisce, conferma i risultati e vota al rogo come un
giocatore umano, con attese casuali di qualche secondo per non sembrare un pulsante
automatico. Non vede la partita grezza: legge la stessa vista filtrata del telefono
(`StatoGiocatoreFactory`), quindi conosce solo il proprio ruolo, i compagni di branco e i
risultati delle proprie indagini. Se il branco ha almeno un lupo umano, la scelta della
preda spetta agli umani; gli AI lupi intervengono solo in un branco tutto artificiale.

I posti AI si impostano in due modi:

- nella **Home**, con il pulsante per riga accanto al nome;
- nella **lobby**, con **🤖 AI** su un posto ancora libero.

Prima di avviare puoi sempre riportare un posto AI a umano con **Rendi umano** (in Home
basta premere di nuovo il pulsante della riga). Dopo l'avvio i ruoli sono già distribuiti.

### La piazza: chat diurna

Se nella stanza c'è almeno un **AI ancora vivo**, durante il giorno compare la chat **La
piazza** sul telefono dei giocatori e nel pannello del narratore. Negli altri casi la chat
non c'è e si passa direttamente al voto.

- Gli AI **commentano di loro iniziativa** (al massimo due volte per turno), con frasi
  generiche, accuse, difese o piccoli bluff. Un lupo AI mente più spesso e non attacca i
  compagni di branco.
- Un umano può scegliere **Tutti** per parlare alla piazza, oppure un AI dal menu **Chiedi
  a**: in quel caso il messaggio è una domanda. L'AI risponde oppure **rifiuta** con una
  motivazione, per esempio "Preferisco non espormi per ora.".
- Gli umani possono anche **raccontare informazioni** libere: se un messaggio cita un
  giocatore vivo con un'accusa o una difesa, ogni AI decide in privato se **fidarsi o
  dubitare** di chi parla (chi ha già mentito o è sospettato viene creduto meno). Le
  informazioni credute cambiano i suoi sospetti e quindi il voto; le altre restano parole.
- La chat è **pubblica**: non mostra ruoli né informazioni private. I morti e gli AI morti
  non parlano; i morti possono leggere.
- Il narratore vede la stessa piazza ma non scrive: resta il regista della serata.

## Funzionalità

- **18 ruoli** con composizione scelta dal narratore (contatori +/−) e nomi personalizzati.
- **Macchina a stati** esplicita: `NotteIntro → Notte → Alba → Giorno → Voto → Ballottaggio → Esecuzione → …`
- **Stanze in memoria** con codice a 5 lettere, posti reclamabili e token per dispositivo.
- **Azioni notturne dal telefono**, con fallback del narratore.
- **Giocatori AI** che giocano come persone: scelgono di notte, confermano i risultati e
  votano al rogo, con memoria privata delle proprie indagini.
- **Chat diurna "La piazza"**: gli AI commentano e bluffano, gli umani fanno domande o
  passano informazioni, gli AI rispondono, si rifiutano o decidono se fidarsi.
- **Voto del rogo e ballottaggio in tempo reale**, con conteggio e gestione dei pareggi
  (secondo voto tra i più votati, altrimenti nessuno al rogo).
- **Diario di gioco** filtrabile, con eventi riservati al narratore.
- **Timer** configurabile per discussione e voto, con beep a fine tempo.
- **Rivelazioni controllate**: ruoli dei caduti e del condannato solo quando decide il narratore.
- **Export/import JSON** della partita e **ripresa** delle sessioni dal browser.
- **Tema scuro** responsive, pensato prima per il telefono.

## Struttura

```
Lupus.slnx
├── Lupus/              # vecchia app console (riferimento storico, invariata)
├── Lupus.Core/         # motore di gioco puro e serializzabile
├── Lupus.Core.Tests/   # harness console con 25 scenari di regole
└── Lupus.Web/          # Blazor Server, UI italiana, stanze multiplayer
```

Il motore (`Lupus.Core`) non conosce Blazor, JavaScript, localStorage o HTTP. Le stanze
(`Lupus.Web/Services/StanzeService.cs`) usano i circuiti Blazor Server per notificare in
tempo reale tutti i dispositivi collegati. I giocatori AI
(`Lupus.Web/Services/GiocatoriAi.cs`) sono un `BackgroundService` che ogni secondo fa
muovere i posti artificiali usando le stesse API dei telefoni.

## Persistenza e limiti

- Le stanze vivono **in memoria** sul server: un riavvio le cancella. Esporta il JSON prima
  di spegnere se vuoi conservare la partita.
- Le stanze inattive vengono rimosse dopo 24 ore.
- Il browser ricorda host e giocatore in `localStorage` (`lupus.sessioni.v1`), così puoi
  riprendere dalla home. Un browser = una sessione giocatore per stanza.
- Il timer non conserva i secondi rimanenti tra un reload: riparte dalla durata configurata.
- L'import di un JSON non conserva i posti AI: la stanza importata nasce con tutti i posti
  liberi, da riassegnare dalla lobby.
- Non c'è autenticazione oltre ai token: chi ha il link può reclamare un posto libero. Con
  il tunnel il link è raggiungibile da Internet, quindi trattalo come un segreto della
  serata, non pubblicarlo e chiudi la stanza a fine partita.

## Documentazione

- `AGENTS.md` — contesto completo del progetto: architettura, regole, correzioni rispetto
  alla versione console, convenzioni e flusso multiplayer.
- `Lupus.Core.Tests/Program.cs` — scenari di regole verificabili.
