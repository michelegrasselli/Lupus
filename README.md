# 🐺 Lupus in Tabula · Multiplayer

Applicazione web per giocare a **Lupus in Tabula** con i telefoni: il narratore conduce
la partita da un dispositivo, ogni giocatore entra dalla stessa Wi-Fi e vede solo il
proprio ruolo, le proprie azioni notturne e il proprio voto.

Niente carta, niente appunti, niente account: la stanza vive sul server e si raggiunge
con un codice a 5 lettere.

## Requisiti

- [.NET SDK 10.0.200](https://dotnet.microsoft.com/) o superiore (target `net10.0`).
- Un browser moderno su narratore e giocatori.
- Telefoni e narratore collegati alla **stessa rete Wi-Fi**.

Nessun database, nessun account, nessun package NuGet esterno.

## Avvio rapido

Dalla root del repository:

```powershell
# Avvia l'app web (profilo HTTP, senza certificati)
dotnet run --project Lupus.Web --launch-profile http
```

L'app ascolta su `http://0.0.0.0:5092`:

- narratore: <http://localhost:5092>
- telefoni: `http://<IP-del-PC>:5092` (l'app mostra l'indirizzo corretto nella home)

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

La prima volta Windows chiede di consentire l'app su reti private. Se i telefoni non
raggiungono il PC, apri la porta TCP 5092 (PowerShell da amministratore):

```powershell
New-NetFirewallRule -DisplayName "Lupus 5092" -Direction Inbound -Protocol TCP -LocalPort 5092 -Action Allow -Profile Private
```

### Build e test

```powershell
# Build dell'intera soluzione
dotnet build Lupus.slnx

# Test del motore di gioco (14 scenari)
dotnet run --project Lupus.Core.Tests/Lupus.Core.Tests.csproj
```

## Come si gioca

1. **Crea la stanza** (`/`): scegli un preset o imposta numero e ruoli, inserisci i nomi.
   Il server assegna subito i ruoli, ma i telefoni non li vedono finché non avvii.
2. **Fai entrare i giocatori**: mostra il link `http://<IP>:5092/entra/<CODICE>` o il
   codice a 5 lettere. Ogni giocatore tocca il proprio nome e reclama il posto.
3. **Avvia la partita** dal pannello del narratore: ogni telefono mostra il proprio ruolo.
4. **Notte**: il motore genera i passi in ordine di priorità. Il giocatore di turno sceglie
   il bersaglio dal telefono; il narratore vede lo stato e può sempre agire al posto suo.
5. **Alba**: eventi pubblici e caduti. Il narratore decide se rivelare ai telefoni i ruoli
   dei morti (utile per il Becchino) con l'apposito interruttore.
6. **Giorno / Voto**: il villaggio discute con il timer; il voto si raccoglie dai telefoni
   con conteggio in tempo reale. In caso di pareggio il narratore sceglie chi va al rogo.
7. **Ballottaggio**: se il Gufo ha aggiunto candidati, i telefoni votano tra loro.
8. **Esecuzione**: il narratore annuncia il ruolo del condannato e cala la notte.
9. **Vittoria**: tutti i ruoli vengono rivelati a tutti i telefoni.

### Cosa vede ogni telefono

- Il **proprio ruolo**, la fazione e i compagni di branco (solo per i lupi).
- Solo le **proprie azioni notturne** e i relativi risultati, mostrati nella card del passo
  mentre accade. Non c'è nessun quaderno: ricordare le informazioni raccolte è compito del
  giocatore.
- Lo **stato pubblico**: vivi, morti, eventi dell'alba, candidati al ballottaggio.
- Mai i ruoli degli altri giocatori vivi.

## Funzionalità

- **18 ruoli** con preset Rapida (6), Classica (8), Villaggio (10), Caos (14), Completa (18).
- **Macchina a stati** esplicita: `NotteIntro → Notte → Alba → Giorno → Voto → Ballottaggio → Esecuzione → …`
- **Stanze in memoria** con codice a 5 lettere, posti reclamabili e token per dispositivo.
- **Azioni notturne dal telefono**, con fallback del narratore.
- **Voto del rogo e ballottaggio in tempo reale**, con conteggio e gestione dei pareggi.
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
├── Lupus.Core.Tests/   # harness console con 14 scenari di regole
└── Lupus.Web/          # Blazor Server, UI italiana, stanze multiplayer
```

Il motore (`Lupus.Core`) non conosce Blazor, JavaScript, localStorage o HTTP. Le stanze
(`Lupus.Web/Services/StanzeService.cs`) usano i circuiti Blazor Server per notificare in
tempo reale tutti i dispositivi collegati.

## Persistenza e limiti

- Le stanze vivono **in memoria** sul server: un riavvio le cancella. Esporta il JSON prima
  di spegnere se vuoi conservare la partita.
- Le stanze inattive vengono rimosse dopo 24 ore.
- Il browser ricorda host e giocatore in `localStorage` (`lupus.sessioni.v1`), così puoi
  riprendere dalla home. Un browser = una sessione giocatore per stanza.
- Il timer non conserva i secondi rimanenti tra un reload: riparte dalla durata configurata.
- Non c'è autenticazione: chi ha il link può reclamare un posto libero. Usa la rete locale
  di casa e non esporre il server su Internet.

## Documentazione

- `AGENTS.md` — contesto completo del progetto: architettura, regole, correzioni rispetto
  alla versione console, convenzioni e flusso multiplayer.
- `Lupus.Core.Tests/Program.cs` — scenari di regole verificabili.
