# AGENTS.md · Lupus in Tabula

File di contesto per chi lavora su questo repository. Leggilo prima di modificare il motore o la UI.

## 1. Obiettivo del progetto

Portare il vecchio narratore da terminale di **Lupus in Tabula** a un gioco web
**multiplayer in tempo reale**: il narratore conduce la partita da un dispositivo, ogni
giocatore entra dal proprio telefono sulla stessa Wi-Fi e vede solo ciò che gli spetta.

Stato attuale:

- **Multiplayer**: stanza con codice a 5 lettere, posti reclamabili, token per dispositivo.
- **Narratore regista**: crea la stanza, avvia la partita, vede tutto, può sostituirsi a un
  giocatore e decide quando rivelare i ruoli dei morti.
- **Giocatori dal telefono**: ruolo privato, azioni notturne, voto
  del rogo e ballottaggio con conteggio in tempo reale.
- **Nessun account, nessun database**: le stanze vivono in memoria sul server; la partita è
  esportabile/importabile come JSON.
- **Motore separato**: `Lupus.Core` è una libreria pura, serializzabile, senza dipendenze da
  ASP.NET o dal browser.

## 2. Stack e prerequisiti

- .NET SDK **10.0.200** (target `net10.0`).
- ASP.NET Core + **Blazor Server** (interactive server, prerender disabilitato).
- CSS puro in `Lupus.Web/wwwroot/app.css`.
- JavaScript minimo in `Lupus.Web/wwwroot/js/lupus.js` per `localStorage`, download JSON,
  copia negli appunti e beep.
- Nessun package NuGet esterno oltre a quelli del framework.

Il realtime usa i **circuiti Blazor Server** già esistenti: `StanzeService` è un singleton
che tiene le stanze e i callback dei componenti; ogni transizione notifica tutti i circuiti
iscritti alla stanza. Non c'è un secondo hub SignalR né un client JS separato.

## 3. Comandi

Esegui dalla root `C:\Users\miche\Desktop\PROGETTI_AI\Lupus`.

```powershell
# Build dell'intera soluzione
dotnet build Lupus.slnx

# Test del motore (harness console, 14 scenari)
dotnet run --project Lupus.Core.Tests/Lupus.Core.Tests.csproj

# Avvio web (profilo HTTP, senza certificati)
dotnet run --project Lupus.Web --launch-profile http
# narratore: http://localhost:5092
# telefoni:  http://<IP-del-PC>:5092

# Build del solo web
dotnet build Lupus.Web/Lupus.Web.csproj

# Eseguibile distribuibile (include wwwroot e _framework)
dotnet publish Lupus.Web/Lupus.Web.csproj -c Release
# → Lupus.Web/bin/Release/net10.0/publish/Lupus.Web.exe
```

> La porta 5092 e il binding `http://0.0.0.0:5092` sono in `appsettings.json` (`"Urls"`),
> quindi valgono anche per l'exe pubblicato. `launchSettings.json` viene usato solo da
> `dotnet run`/IDE. L'exe dentro `bin\Debug\net10.0` funziona anche lanciato direttamente
> perché `Program.cs` abilita `StaticWebAssetsLoader` fuori da Development: senza quel
> loader partirebbe in Production senza `wwwroot` e servirebbe CSS e `_framework` vuoti o
> in errore (UI bianca e senza interattività). Per distribuire resta consigliato `publish`,
> che include `wwwroot` e non dipende dai sorgenti.

Se i telefoni non raggiungono il PC, apri la porta TCP 5092 nel firewall di Windows
(profilo privato):

```powershell
New-NetFirewallRule -DisplayName "Lupus 5092" -Direction Inbound -Protocol TCP -LocalPort 5092 -Action Allow -Profile Private
```

Il vecchio progetto console `Lupus/` resta compilabile e **non va modificato** salvo richiesta esplicita: serve come riferimento storico delle regole.

## 4. Struttura della soluzione

```
Lupus.slnx
├── Lupus/                  # vecchia app console, invariata
├── Lupus.Core/             # motore di gioco puro, riutilizzabile
├── Lupus.Core.Tests/       # harness console con scenari di regole
└── Lupus.Web/              # Blazor Server, UI italiana, stanze multiplayer
```

### Lupus.Core

| File | Responsabilità |
| --- | --- |
| `Ruolo.cs` | Enum `Ruolo`, record `RuoloInfo`, catalogo `Ruoli.Tutti` con nome, descrizione, priorità, fazione, icona, accento. |
| `Giocatore.cs` | Stato del giocatore: ruolo attuale/iniziale, vivo, capobranco, flag morte/trasformazione/Diavolo/trappola. |
| `Partita.cs` | Stato serializzabile: giocatori, turno, fase, passi notte, azioni, risultato, voto, ballottaggio, esito, log, timer. Contiene `Normalizza()` e `PreparaNuovaNotte()`. |
| `Modelli.cs` | Enum `FasePartita`, `EsitoPartita`, `TipoLog`, `TipoPasso`, `Potere`; classi `PassoNotte`, `AzioneNotte`, `VoceLog`, `EventoNotte`, `RisultatoNotte`, `EsitoPasso`. |
| `MotoreLupus.cs` | Tutte le transizioni di stato e le regole notturne/di voto. Unica fonte di verità. |
| `CreazionePartita.cs` | Validazione configurazione, creazione giocatori, assegnazione casuale dei ruoli (o esplicita da una mappa nome→ruolo). |
| `Preset.cs` | 5 preset storici (Rapida 6, Classica 8, Villaggio 10, Caos 14, Completa 18): restano come API del motore, ma la Home non li usa più. |
| `Serializzazione.cs` | JSON con enum come stringhe, wrapper `SalvataggioPartita`, deserializzazione tollerante. |

### Lupus.Web

| File | Responsabilità |
| --- | --- |
| `Program.cs` | Registra Razor Components, `StanzeService` (singleton), `ReteHelper` (singleton), `ArchivioPartita`/`ArchivioSessioni` (scoped); static assets; interactive server. |
| `Components/App.razor` | HTML base italiano, `prerender: false`, CSS, script `lupus.js`. |
| `Components/Pages/Home.razor` | Crea una stanza partendo da zero posti: numero di giocatori, composizione dei ruoli con +/−, nomi. Entra con codice, riprende le sessioni, importa un JSON. |
| `Components/Pages/Entra.razor` | `/entra` e `/entra/{Codice}`: inserimento codice, elenco posti, reclamo del posto, rientro, "Dimentica". |
| `Components/Pages/Gioca.razor` | `/gioca/{Codice}?t=...`: lobby, ruolo privato, azioni notturne, alba, discussione, voto, ballottaggio, esecuzione, finale. |
| `Components/Pages/PartitaGioco.razor` | `/partita/{Codice}?h=...`: dashboard narratore, lobby e link, passi notte, alba, voto live, ballottaggio, rogo, finale, diario, timer, rivelazioni, chiusura stanza. |
| `Components/Shared/PlayerPicker.razor` | Griglia di selezione con ruoli/fazioni: **solo per il narratore**. |
| `Components/Shared/PlayerTargetPicker.razor` | Griglia di selezione senza ruoli né colori di fazione: **per i giocatori**. |
| `Components/Shared/VoteTally.razor` | Conteggio voti con barre, elettori e non votanti. |
| `Components/Shared/TimerCountdown.razor` | Timer con preset, pausa/reset, beep a fine tempo, callback durata. |
| `Services/Stanza.cs` | Stato stanza: posti/token, `Avviata`, voti, esiti, flag di rivelazione, quaderni privati. |
| `Services/StanzeService.cs` | Registro singleton: creazione, join, transizioni, azioni, voti, pareggi, notifiche, pulizia 24h. |
| `Services/StatoGiocatore.cs` | DTO e factory della vista giocatore: filtro privacy, azioni autorizzate, stato voto. |
| `Services/ReteHelper.cs` | Rileva l'IPv4 LAN e costruisce gli URL `http://<ip>:5092/entra/<codice>`. |
| `Services/ArchivioSessioni.cs` | Sessioni host/giocatore in `localStorage` (`lupus.sessioni.v1`). |
| `Services/ArchivioPartita.cs` | Export/import JSON, download, conferma, copia negli appunti. |
| `Services/Testi.cs` | Etichette italiane di fasi, vittorie e classi CSS. |
| `wwwroot/app.css` | Tema scuro "notte", layout responsive, componenti narratore e telefono. |
| `wwwroot/js/lupus.js` | `lupusStorage.save/load/remove/download/copy/beep/scrollLog`. |

> **Nota sul nome della pagina**: il componente si chiama `PartitaGioco`, non `Partita`, per evitare che la classe generata `Lupus.Web.Components.Pages.Partita` oscuri `Lupus.Core.Partita` nel codice generato. La route resta `/partita/{Codice}`.

> `Services/PartitaService.cs` è il vecchio servizio a circuito singolo: non è più registrato
> né usato. Può essere eliminato quando non serve più come riferimento.

## 5. Modello multiplayer

### Stanza e token

- `Stanza.Codice`: 5 caratteri da un alfabeto senza ambiguità (`ABCDEFGHJKLMNPQRSTUVWXYZ23456789`).
- `Stanza.HostToken`: segreto del narratore, passato in query `?h=...` e salvato in `localStorage`.
- `Posto.Token`: segreto del singolo giocatore, passato in query `?t=...` e salvato in `localStorage`.
- `Stanza.Avviata`: finché è `false` i telefoni sono in lobby e **non** vedono il ruolo.
- `Stanza.Posti`: un posto per ogni giocatore creato, con `GiocatoreId` stabile.
- I token sono opachi e non indovinabili (GUID `N`); non c'è autenticazione oltre al token.

### Ciclo di vita

1. `Home` → `StanzeService.Crea(...)` → `CreazionePartita.Crea(...)` (i ruoli vengono
   assegnati e mescolati) → stanza in `NotteIntro` con `Avviata = false`.
2. I giocatori aprono `/entra/<codice>`, scelgono un posto libero → `UnisciAsync` genera il
   token del posto.
3. Il narratore apre `/partita/<codice>?h=<hostToken>` e preme **Avvia la partita** →
   `AvviaAsync` imposta `Avviata = true`; i telefoni mostrano il ruolo.
4. `IniziaNotteAsync` chiama `MotoreLupus.IniziaNotte` e azzera voti e flag di rivelazione.
5. I giocatori eseguono i passi dal telefono (`EseguiPassoGiocatoreAsync`); il narratore può
   sempre eseguirli al posto loro (`EseguiPassoNarratoreAsync`).
6. `RisolviNotteAsync` → alba; `VaiAlGiornoAsync` → giorno; `ApriVotoAsync` → voto.
7. I telefoni votano (`RegistraVotoAsync`), il narratore chiude (`ChiudiVotoAsync`).
8. In caso di pareggio al rogo si apre il **secondo voto** tra i più votati
   (`ApriBallottaggio`); se anche quello è in parità nessuno va al rogo (`SaltaRogo`).
   Se il Gufo ha aggiunto candidati si passa al ballottaggio (`ChiudiBallottaggioAsync`).
   Il narratore può sempre forzare o saltare il rogo dal pannello.
9. `ProssimaNotteAsync` torna a `NotteIntro` con il turno successivo.
10. `AbbandonaAsync` rimuove la stanza e notifica i telefoni, che mostrano "stanza chiusa".

### Privacy (regola d'oro)

La vista giocatore è costruita **solo** da `StatoGiocatoreFactory.Crea`. Non passare mai
direttamente `Partita` o `Giocatore` a `Gioca.razor`.

- `StatoGiocatore.Ruolo`/`Info` sono `null` finché `Avviata == false`.
- `GiocatorePubblico.Ruolo` è valorizzato solo per: il proprio ruolo, i morti se il narratore
  ha attivato `RivelaRuoliMorti`, il condannato se ha attivato `RivelaRuoloRogo`, e tutti
  alla fine.
- `GiocatorePubblico.Capobranco` è visibile al branco e a se stessi; `Trasformato` resta
  privato (o finale).
- `AzioneGiocatore.Risultato` è popolato solo per i passi autorizzati al giocatore.
- `EventiAlba` esclude gli eventi con `Riservato = true`.
- Il **risultato di un passo non deve rivelare ciò che il potere non dice**: la Puttana
  (e l'Imputato con potere copiato) sa con chi ha passato la notte, **non** se era un Lupo
  né che morirà. La Spia scopre solo **chi** ha puntato il bersaglio, **non** con quale
  potere: non aggiungere il ruolo dell'attore al risultato. I dettagli extra vanno in
  `MotoreLupus.NotaNarratore`, che finisce solo nel diario del narratore. Vale per ogni nuovo
  potere: prima di scrivere `passo.Risultato`, chiedersi se quella frase è lecita per il
  ruolo che la legge.
- Il diario (`Partita.Log`) è **solo del narratore** e non viene mai serializzato verso il
  telefono.
- `PlayerPicker` espone ruoli e accenti: usalo solo nel pannello narratore. Per i giocatori
  usa `PlayerTargetPicker`.

### Autorizzazione delle azioni

`StatoGiocatoreFactory.Autorizzato(giocatore, passo)`:

- `TipoPasso.Capobranco` → qualsiasi Lupo vivo. Il passo non viene più generato (il
  capobranco è scelto a caso dal motore), ma resta gestito per i salvataggi vecchi.
- `Potere.Lupo` → qualsiasi Lupo vivo.
- altrimenti → solo `passo.AttoreId`.

In più `StanzeService` verifica sempre: token valido, fase `Notte`, giocatore vivo, passo
esistente, autorizzazione, e `Partita.PassoCorrente?.Id == passoId`.

### Voto e pareggi

- `Stanza._voti` è `votanteId → bersaglioId`; un giocatore vota una sola volta.
- Il voto è segreto finché il narratore non chiude: `StatoVoto.Conteggi` è vuoto durante il
  voto aperto, tranne per il narratore che vede il tally live.
- `ChiudiVotoAsync` calcola i gruppi. Se il massimo è condiviso chiama
  `MotoreLupus.ApriBallottaggio` con i più votati (più gli eventuali gufati vivi): il
  **secondo voto** avviene solo tra loro. Se anche questo finisce in parità si chiama
  `MotoreLupus.SaltaRogo` e **nessuno va al rogo** per quel turno.
- Il ballottaggio del Gufo segue lo stesso schema con `_votiBallottaggio`; `Stanza.BallottaggioDaPareggio`
  distingue il secondo voto dal ballottaggio del Gufo nella UI.
- `sceltaManuale` resta come override del narratore ("Forza il rogo"); `SaltaRogoAsync`
  permette di saltare il rogo sia dalla fase `Voto` sia dal `Ballottaggio`.
- `ApplicaVoto`/`ApplicaBallottaggio` aggiornano gli esiti mostrati ai telefoni.

### Notifiche

- `StanzeService.Sottoscrivi(codice, callback)` e `AnnullaSottoscrizione`.
- Ogni pagina che mostra una stanza si iscrive in `OnAfterRenderAsync` e si disiscrive in
  `Dispose`.
- Le notifiche partono **fuori dal lock** e ogni callback è protetta da try/catch: un
  telefono disconnesso non deve bloccare gli altri.
- Dopo ogni mutazione chiamare `NotificaAsync`.

## 6. Flusso di gioco e macchina a stati

`FasePartita`:

```
NotteIntro → Notte → Alba → Giorno → Voto → Ballottaggio → Esecuzione → NotteIntro ...
                                                              ↘ Finita
```

- `Avvia` imposta turno 1, `NotteIntro`, prepara la notte e assegna a caso il capobranco iniziale se manca.
- `IniziaNotte` genera i `PassiNotte` in ordine di priorità. Se il capobranco è caduto o ha cambiato ruolo, ne viene scelto subito un altro a caso tra i Lupi vivi (non esiste più un passo di elezione manuale). I passi senza bersagli validi vengono creati già completati con un risultato informativo. Anche gli attori morti ricevono un passo segnaposto già chiuso (`AttoreMorto = true`): il numero e l'ordine delle chiamate non devono rivelare quali ruoli sono caduti.
- `EseguiPasso(partita, passoId, bersaglioId)` registra l'azione e calcola il risultato. Se `RichiedeConferma`, il passo resta non completato e la UI mostra il risultato con un pulsante di conferma; altrimenti si completa subito.
- `ConfermaRisultato` completa un passo informativo che richiede conferma.
- `RisolviNotte` applica morti, trappole, Kamikaze, Baby Lupo, Diavolo, Copioni e calcola il risultato dell'alba. Imposta `Fase = Alba` oppure `Finita` se c'è una vittoria.
- `VaiAlGiorno` passa a `Giorno` (o `Finita` se l'esito è già deciso).
- `ApriVoto` apre il voto. `Vota` può chiudere subito il rogo oppure aprire il ballottaggio se il Gufo ha aggiunto candidati.
- `ApriBallottaggio` apre il secondo voto tra i candidati indicati (pareggio al rogo).
- `ScegliBallottaggio` esegue il rogo tra i candidati.
- `SaltaRogo` chiude il turno senza condannato (pareggio anche al secondo voto o scelta del narratore).
- `ProssimaNotte` incrementa il turno e torna a `NotteIntro`.

### API pubblica di `MotoreLupus`

`Avvia`, `IniziaNotte`, `EseguiPasso`, `ConfermaRisultato`, `RisolviNotte`, `VaiAlGiorno`, `ApriVoto`, `Vota`, `ApriBallottaggio`, `ScegliBallottaggio`, `SaltaRogo`, `ProssimaNotte`, `ControllaVittoria`, `ImpostaCapobranco`, `AssegnaCapobrancoCasuale`, `OpzioniPerPotere`, `DescrizionePotere`, `EtichettaPotere`.

### Invarianti utili

- `Partita.PassoCorrente` è il primo passo non completato.
- `Partita.NotteCompleta` è `PassiNotte.All(p => p.Completato)`.
- I passi informativi già completati hanno `Saltato = true` e `Risultato` valorizzato. Il loro `Potere` deve essere quello reale del ruolo o `Potere.Nessuno`, mai il valore predefinito `Lupo`: altrimenti il diario del narratore li attribuisce al branco.
- I passi segnaposto degli attori morti hanno `AttoreMorto = true`, `Completato = true` e `Saltato = true`; restano nel diario del narratore ma non vengono mostrati ai giocatori.
- I passi che richiedono conferma hanno `RichiedeConferma = true`; dopo `EseguiPasso` hanno `Risultato` ma `Completato = false`.
- `Partita.UltimoRogoId` viene impostato da `EseguiRogo` e serve alla schermata di fine giornata. `PreparaNuovaNotte` lo azzera.
- `Partita.BallottaggioIds` contiene i candidati del Gufo durante l'alba/giorno/voto; durante il ballottaggio contiene i candidati finali (votato + gufati, oppure i più votati in caso di pareggio + gufati).
- `Partita.UltimoRogoId` resta `null` quando il rogo viene saltato o quando anche il secondo voto è in parità.
- `Partita.Esito` non nullo significa partita decisa; la fase può essere `Alba` (vittoria notturna) o `Finita`.
- Il capobranco è sempre esattamente uno tra i Lupi vivi (se ce ne sono) e viene scelto a caso all'avvio e dopo ogni morte o trasformazione del capo. `AssegnaCapobrancoCasuale` azzera i vecchi flag prima di scegliere.

## 7. Regole e correzioni rispetto alla versione console

Le regole sono state portate dalla console, correggendo i bug evidenti. Le modifiche principali:

1. **I lupi scelgono solo non-lupi**: `OpzioniPerPotere(Potere.Lupo)` esclude `Ruolo.Lupo` e l'attore stesso. La regola vale per il branco: il potere copiato dell'Imputato (`Potere.ImputatoLupo`) è un attacco solitario e può puntare anche un Lupo.
2. **Tutti i giocatori con lo stesso ruolo agiscono**: i passi notturni vengono generati per ogni attore vivo (Becchino, Cacciatore, Copione, Copione2, ecc.).
3. **Trappola del Cacciatore**: se il branco attacca il bersaglio trappolato, la vittima si salva e muore il capobranco. Se l'attacco arriva dal potere copiato dell'Imputato, muore l'Imputato.
4. **Kamikaze**: se il branco lo attacca, il Kamikaze sopravvive e muore il capobranco. Se è protetto dal Guardiano, la protezione ha la precedenza e non esplode.
5. **Baby Lupo**: se attaccato dal branco non muore, si trasforma in Lupo all'alba (`RuoloIniziale` conservato, `Trasformato = true`).
6. **Diavolo**: se sbranato sopravvive con `DiavoloSbranato = true`. Se viene anche votato nello stesso turno, vince da solo. Il flag viene azzerato solo dopo il rogo, non all'inizio del giorno.
7. **Copione / Copione 2**: copiano il ruolo di un giocatore e la trasformazione avviene all'alba. Possono puntarsi a vicenda: se il bersaglio è ancora un Copione non copiano nulla e restano Copioni, potendo riprovare la notte successiva. La copia si risolve sui ruoli di inizio alba, così l'ordine dei passi non cambia l'esito.
8. **Gufo**: il suo bersaglio finisce al ballottaggio del giorno successivo. Il ballottaggio avviene tra il giocatore votato e i giocatori segnati dal Gufo, non solo tra i gufati.
9. **Cacciatore**: dopo aver piazzato una trappola, la notte successiva non può piazzarne un'altra (`TrappolaInCooldown`).
10. **Sfigato**: appare CATTIVO alle indagini (Veggente/Becchino), ma non è un Lupo e non conta per la vittoria del branco.
11. **Annullatore**: blocca il potere del bersaglio per la notte. I Lupi sono immuni se ci sono più lupi vivi (branco).
12. **Imputato**: copia i poteri di chi lo ha puntato quella notte e li usa subito. I passi dell'Imputato vengono generati solo dopo i passi con priorità inferiore a 10. Se copia il Lupo, il suo attacco può colpire anche un Lupo vero (il branco invece non può).
13. **Vittoria**: Buoni se non restano lupi; Lupi se `lupi >= altri vivi`; Pareggio se non resta nessuno; Diavolo se sbranato e votato nello stesso turno.
14. **Ruoli morti**: ogni ruolo notturno viene chiamato anche se il suo attore è morto. Il passo è un segnaposto già completato che non produce azioni, così i vivi non possono dedurre quali personaggi sono caduti dal numero o dall'ordine delle chiamate. Il branco resta una chiamata unica: i lupi morti non alterano la chiamata del branco.
15. **Pareggio al rogo**: se due o più giocatori hanno lo stesso numero massimo di voti si apre un secondo voto solo tra loro (più gli eventuali gufati vivi). Se anche il secondo voto è in parità nessuno va al rogo e si passa all'esecuzione vuota. Il narratore può in ogni caso forzare o saltare il rogo.

Le priorità notturne sono in `Ruoli.Tutti` e guidano l'ordine dei passi.

## 8. UI e stato delle pagine

### `PartitaGioco.razor` (narratore)

- **Header**: codice stanza, fase, conteggi, rivela/nascondi ruoli, esporta, chiudi.
- **Timeline** delle fasi con stato `done/current/todo`.
- **Lobby**: link per i telefoni, posti occupati, "Libera" per un posto, "Avvia la partita".
- **Notte**: passo corrente, picker con ruoli, risultato con conferma, log dei passi,
  "Risolvi la notte" quando `NotteCompleta`.
- **Alba**: eventi pubblici, caduti, gufati, eventi riservati, interruttore "Rivela i ruoli dei caduti".
- **Giorno**: vivi, candidati del Gufo, "Apri il voto".
- **Voto**: tally live, "Chiudi il voto", "Forza il rogo", "Salta il rogo".
- **Ballottaggio**: candidati, tally live, "Forza il rogo", "Salta il rogo"; il titolo è
  "Secondo voto" quando nasce da un pareggio (`BallottaggioDaPareggio`).
- **Esecuzione**: ultimo rogo (o "Nessuno va al rogo"), interruttore "Rivela il ruolo del
  condannato" solo se c'è un condannato, "Cala la notte".
- **Finita**: vincitore e riepilogo ruoli.
- **Sidebar**: timer, elenco giocatori con stato connessione (`📱`) e reveal, stanza/link,
  diario filtrabile.
- **Modale ruoli**: reveal uno alla volta (`ruoliRivelati`, non persistito).

### `Gioca.razor` (giocatore)

- **Header sticky**: stanza, nome, fase/turno, chip del proprio ruolo, stato vivo/morto.
- **Lobby**: posti e attesa, senza ruolo.
- **Ruolo**: carta con icona, fazione, descrizione, capobranco, compagni di branco.
- **Notte**: una card per ogni azione autorizzata, picker senza ruoli, risultato, conferma.
- **Alba**: eventi pubblici, caduti (ruolo solo se rivelato), gufati.
- **Giorno**: vivi e candidati.
- **Voto/Ballottaggio**: picker, voto segreto, avanzamento, esito e tally a chiusura.
  Il secondo voto (pareggio al rogo) è etichettato come tale.
- **Esecuzione**: condannato e ruolo se rivelato.
- **Finita**: vincitore, eventi, ruoli di tutti.
- **Quaderno**: non esiste. I risultati dei propri passi si vedono nella card dell'azione
  mentre accade; ricordare le informazioni raccolte è compito del giocatore. Il diario
  completo resta solo al narratore.

### Dettagli tecnici importanti

- `App.razor` usa `prerender: false`: JS/localStorage sono disponibili in `OnAfterRenderAsync`,
  non in `OnInitialized`.
- `Program.cs` chiama `StaticWebAssetsLoader.UseStaticWebAssets` fuori da Development. È la
  rete di sicurezza per l'exe lanciato da `bin/`: senza, `MapStaticAssets` non serve
  `app.css` né `_framework/blazor.web.js` e la UI resta senza stile e senza interattività.
- Le pagine risolvono il token da query string e, se manca, da `ArchivioSessioni`.
- `Stanze.Ottieni` va richiamato a ogni aggiornamento: la stanza può essere stata chiusa o
  sostituita.
- `Stanze.StatoDi`/`PostiPubblici`/`GiocatoreDi` prendono il lock del servizio: non leggere
  `Stanza.Partita` direttamente da un circuito mentre un altro scrive.
- `TimerCountdown` viene ricreato con `@key="TimerKey"` quando cambiano stanza/turno/fase o
  dopo un cambio manuale.
- Il timer **non** persiste i secondi rimanenti: dopo un reload riparte dalla durata
  configurata. È una scelta consapevole.
- Il diario mostra le ultime 250 voci e fa auto-scroll tramite `lupusStorage.scrollLog`.
- Su mobile il layout del giocatore è a colonna singola; il narratore resta a due colonne
  sopra i 1240px.

## 9. Persistenza

- Sessioni: chiave `lupus.sessioni.v1` (`ArchivioSessioni`), elenco di
  `SessioneBrowser { Codice, Tipo, Token, GiocatoreId, Nome, SalvataIl }`.
- Un browser può ospitare e giocare la stessa stanza (tipi diversi), ma ricorda **un solo
  giocatore per stanza**: l'ultimo posto reclamato sovrascrive il precedente.
- Export: `ArchivioPartita.ScaricaAsync` → `lupus-yyyyMMdd-HHmm.json`.
- Import: `Serializzazione.Deserializza` → `Stanze.CreaDaPartita(partita, avviata)`; se la
  partita non è in `NotteIntro` la stanza nasce già avviata e i posti sono liberi.
- `Partita.Normalizza()` ripara liste null dopo un import.
- `Partita.Versione` è pensato per migrazioni future.
- **Le stanze non sono persistite**: un riavvio del server le perde. `Pulizia()` rimuove le
  stanze inattive da più di 24 ore.

## 10. Test

`Lupus.Core.Tests/Program.cs` è un harness console senza framework. Copre 25 scenari:

1. Protezione del Guardiano.
2. Morte senza protezione.
3. Trappola del Cacciatore.
4. Kamikaze.
5. Baby Lupo.
6. Puttana con Lupo.
7. Annullatore sulla Veggente.
8. Diavolo.
9. Copione che copia un ruolo.
10. Copione che punta un altro Copione.
11. Copione che punta un Copione già trasformato.
12. Ruoli morti chiamati come segnaposto.
13. Gufo e ballottaggio.
14. Pareggio al rogo: secondo voto tra i più votati.
15. Secondo voto in parità: nessuno al rogo.
16. Narratore che salta il rogo dal voto.
17. Imputato.
18. Imputato che, copiato il Lupo, sbrana un Lupo.
19. Capobranco scelto a caso tra i Lupi.
20. Capobranco rimpiazzato a caso dopo la morte.
21. Spia che non rivela il potere di chi ha puntato.
22. Serializzazione.
23. Validazione composizioni.
24. Creazione con ruoli espliciti scelti dal narratore.
25. Condizioni di vittoria.

Quando cambi una regola, aggiungi o aggiorna uno scenario qui e lancia il comando di test.

La UI multiplayer non ha test automatici: la verifica è manuale con narratore + più schede
giocatore (vedi checklist).

## 11. Convenzioni

- UI in **italiano**, tono da narratore di paese.
- Tema scuro "notte", accento viola/blu, responsive per tablet e telefono.
- Nessuna dipendenza da Bootstrap o framework CSS.
- Preferire classi CSS semantiche già presenti in `app.css`; aggiungerne di nuove solo se necessario.
- Il motore non deve conoscere Blazor, JS, localStorage o HTTP.
- Le transizioni di stato passano sempre da `MotoreLupus`; la UI non modifica mai direttamente
  `Vivi`, `Ruolo`, `Fase`, ecc. (unica eccezione: flag di presentazione e `TimerSecondi`).
- Ogni nuova informazione mostrata a un giocatore passa da `StatoGiocatoreFactory`, mai da
  `Partita` o `Giocatore` grezzi.
- Non eliminare il progetto console `Lupus/`: è il riferimento storico.

## 12. Limiti attuali

- Le stanze vivono in memoria: un riavvio del server le cancella.
- Nessuna autenticazione oltre al token: chi ha il link può reclamare un posto libero.
- Un browser ricorda un solo giocatore per stanza.
- Il timer non conserva i secondi rimanenti tra reload.
- Lo stato "ruolo già rivelato" nella modale non è persistito.
- Nessun test UI automatizzato.
- Il rilevamento dell'IP LAN preferisce Wi-Fi/Ethernet reali e penalizza gli adattatori
  virtuali, ma su macchine con VPN o più reti potrebbe comunque scegliere l'indirizzo
  sbagliato: in quel caso mostra il link corretto a mano.

## 13. Roadmap

1. **Persistenza server** delle stanze (in-memory con scadenza già presente, poi database)
   usando lo stesso JSON di `Serializzazione`.
2. **Reconnect robusto**: heartbeat dei telefoni, stato "connesso" reale, ripresa dopo
   sospensione del browser.
3. **QR code** nella lobby per far entrare i telefoni senza digitare il link.
4. **Test di integrazione** per autorizzazione, privacy e viste filtrate.
5. **Notifiche** (vibrazione/beep) quando il telefono deve agire o quando si apre il voto.
6. **Eliminare** `PartitaService` e i riferimenti al vecchio flusso narratore-only.

Il motore è già serializzabile e senza stato globale: queste aggiunte non richiedono di
riscrivere le regole.

## 14. Checklist rapida prima di consegnare

```powershell
dotnet build Lupus.slnx
dotnet run --project Lupus.Core.Tests/Lupus.Core.Tests.csproj
dotnet run --project Lupus.Web --launch-profile http
```

Poi nel browser (narratore + almeno due schede giocatore):

- Home → imposta numero giocatori, ruoli e nomi → "Crea la stanza" → codice e link visibili.
- Apri il link in due schede → reclama due posti → i telefoni sono in lobby senza ruolo.
- "Avvia la partita" → ogni telefono vede il proprio ruolo.
- "Inizia la notte" → il giocatore di turno agisce → il passo avanza per tutti.
- Conferma un risultato informativo (Veggente) → il risultato compare nella card del telefono e poi sparisce: nessun quaderno.
- "Risolvi la notte" → alba → "Vai alla discussione" → "Apri il voto".
- Vota da due telefoni → il narratore vede il tally → "Chiudi il voto".
- Pareggio al rogo → secondo voto tra i più votati; se resta il pareggio nessuno va al rogo.
- "Salta il rogo" dal voto o dal ballottaggio → esecuzione vuota e "Cala la notte".
- Ballottaggio del Gufo → candidati e rogo.
- "Cala la notte" → secondo turno.
- "Rivela i ruoli dei caduti" e "Rivela il ruolo del condannato" cambiano la vista dei telefoni.
- Fine partita → tutti i ruoli visibili su tutti i dispositivi.
- Esporta, ricarica, riprendi dalla home; chiudi la stanza e verifica il messaggio sui telefoni.
- Controlla la console del browser senza errori e il layout a 390px di larghezza.
