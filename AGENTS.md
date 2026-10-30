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
- **Giocatori dal telefono**: ruolo privato, azioni notturne, quaderno delle indagini, voto
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
> `dotnet run`/IDE: l'exe dentro `bin\Debug\net10.0` non è distribuibile (manca `wwwroot`)
> e senza `Urls` ripiegherebbe sulla porta 5000.

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
| `CreazionePartita.cs` | Validazione configurazione, creazione giocatori, assegnazione casuale dei ruoli. |
| `Preset.cs` | 5 preset: Rapida (6), Classica (8), Villaggio (10), Caos (14), Completa (18). |
| `Serializzazione.cs` | JSON con enum come stringhe, wrapper `SalvataggioPartita`, deserializzazione tollerante. |

### Lupus.Web

| File | Responsabilità |
| --- | --- |
| `Program.cs` | Registra Razor Components, `StanzeService` (singleton), `ReteHelper` (singleton), `ArchivioPartita`/`ArchivioSessioni` (scoped); static assets; interactive server. |
| `Components/App.razor` | HTML base italiano, `prerender: false`, CSS, script `lupus.js`. |
| `Components/Pages/Home.razor` | Crea una stanza (preset, ruoli, nomi), entra con codice, riprende le sessioni, importa un JSON. |
| `Components/Pages/Entra.razor` | `/entra` e `/entra/{Codice}`: inserimento codice, elenco posti, reclamo del posto, rientro, "Dimentica". |
| `Components/Pages/Gioca.razor` | `/gioca/{Codice}?t=...`: lobby, ruolo privato, azioni notturne, alba, discussione, voto, ballottaggio, esecuzione, finale, quaderno. |
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

1. `Home` → `StanzeService.Crea(...)` → `CreazionePartita.Crea(...)` (ruoli già assegnati) →
   stanza in `NotteIntro` con `Avviata = false`.
2. I giocatori aprono `/entra/<codice>`, scelgono un posto libero → `UnisciAsync` genera il
   token del posto.
3. Il narratore apre `/partita/<codice>?h=<hostToken>` e preme **Avvia la partita** →
   `AvviaAsync` imposta `Avviata = true`; i telefoni mostrano il ruolo.
4. `IniziaNotteAsync` chiama `MotoreLupus.IniziaNotte` e azzera voti e flag di rivelazione.
5. I giocatori eseguono i passi dal telefono (`EseguiPassoGiocatoreAsync`); il narratore può
   sempre eseguirli al posto loro (`EseguiPassoNarratoreAsync`).
6. `RisolviNotteAsync` → alba; `VaiAlGiornoAsync` → giorno; `ApriVotoAsync` → voto.
7. I telefoni votano (`RegistraVotoAsync`), il narratore chiude (`ChiudiVotoAsync`).
8. Se il Gufo ha aggiunto candidati si passa al ballottaggio (`ChiudiBallottaggioAsync`).
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
  né che morirà. I dettagli extra vanno in `MotoreLupus.NotaNarratore`, che finisce solo nel
  diario del narratore. Vale per ogni nuovo potere: prima di scrivere `passo.Risultato`,
  chiedersi se quella frase è lecita per il ruolo che la legge.
- Il diario (`Partita.Log`) è **solo del narratore** e non viene mai serializzato verso il
  telefono.
- `PlayerPicker` espone ruoli e accenti: usalo solo nel pannello narratore. Per i giocatori
  usa `PlayerTargetPicker`.

### Autorizzazione delle azioni

`StatoGiocatoreFactory.Autorizzato(giocatore, passo)`:

- `TipoPasso.Capobranco` → qualsiasi Lupo vivo.
- `Potere.Lupo` → qualsiasi Lupo vivo.
- altrimenti → solo `passo.AttoreId`.

In più `StanzeService` verifica sempre: token valido, fase `Notte`, giocatore vivo, passo
esistente, autorizzazione, e `Partita.PassoCorrente?.Id == passoId`.

### Voto e pareggi

- `Stanza._voti` è `votanteId → bersaglioId`; un giocatore vota una sola volta.
- Il voto è segreto finché il narratore non chiude: `StatoVoto.Conteggi` è vuoto durante il
  voto aperto, tranne per il narratore che vede il tally live.
- `ChiudiVotoAsync` calcola i gruppi; se il massimo è condiviso imposta `VotoPareggio`,
  `CandidatiPareggio` e restituisce `RisultatoChiusura.Pareggio = true`.
- Il narratore risolve con `sceltaManuale`; solo dopo si chiama `MotoreLupus.Vota`.
- Il ballottaggio segue lo stesso schema con `_votiBallottaggio`.
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

- `Avvia` imposta turno 1, `NotteIntro`, prepara la notte e assegna il capobranco iniziale se manca.
- `IniziaNotte` genera i `PassiNotte` in ordine di priorità. I passi senza bersagli validi vengono creati già completati con un risultato informativo.
- `EseguiPasso(partita, passoId, bersaglioId)` registra l'azione e calcola il risultato. Se `RichiedeConferma`, il passo resta non completato e la UI mostra il risultato con un pulsante di conferma; altrimenti si completa subito.
- `ConfermaRisultato` completa un passo informativo che richiede conferma.
- `RisolviNotte` applica morti, trappole, Kamikaze, Baby Lupo, Diavolo, Copioni e calcola il risultato dell'alba. Imposta `Fase = Alba` oppure `Finita` se c'è una vittoria.
- `VaiAlGiorno` passa a `Giorno` (o `Finita` se l'esito è già deciso).
- `ApriVoto` apre il voto. `Vota` può chiudere subito il rogo oppure aprire il ballottaggio se il Gufo ha aggiunto candidati.
- `ScegliBallottaggio` esegue il rogo tra i candidati.
- `ProssimaNotte` incrementa il turno e torna a `NotteIntro`.

### API pubblica di `MotoreLupus`

`Avvia`, `IniziaNotte`, `EseguiPasso`, `ConfermaRisultato`, `RisolviNotte`, `VaiAlGiorno`, `ApriVoto`, `Vota`, `ScegliBallottaggio`, `ProssimaNotte`, `ControllaVittoria`, `ImpostaCapobranco`, `OpzioniPerPotere`, `DescrizionePotere`, `EtichettaPotere`.

### Invarianti utili

- `Partita.PassoCorrente` è il primo passo non completato.
- `Partita.NotteCompleta` è `PassiNotte.All(p => p.Completato)`.
- I passi informativi già completati hanno `Saltato = true` e `Risultato` valorizzato.
- I passi che richiedono conferma hanno `RichiedeConferma = true`; dopo `EseguiPasso` hanno `Risultato` ma `Completato = false`.
- `Partita.UltimoRogoId` viene impostato da `EseguiRogo` e serve alla schermata di fine giornata. `PreparaNuovaNotte` lo azzera.
- `Partita.BallottaggioIds` contiene i candidati del Gufo durante l'alba/giorno/voto; durante il ballottaggio contiene i candidati finali (votato + gufati).
- `Partita.Esito` non nullo significa partita decisa; la fase può essere `Alba` (vittoria notturna) o `Finita`.

## 7. Regole e correzioni rispetto alla versione console

Le regole sono state portate dalla console, correggendo i bug evidenti. Le modifiche principali:

1. **I lupi scelgono solo non-lupi**: `OpzioniPerPotere(Potere.Lupo)` esclude `Ruolo.Lupo` e l'attore stesso.
2. **Tutti i giocatori con lo stesso ruolo agiscono**: i passi notturni vengono generati per ogni attore vivo (Becchino, Cacciatore, Copione, Copione2, ecc.).
3. **Trappola del Cacciatore**: se il branco attacca il bersaglio trappolato, la vittima si salva e muore il capobranco. Se l'attacco arriva dal potere copiato dell'Imputato, muore l'Imputato.
4. **Kamikaze**: se il branco lo attacca, il Kamikaze sopravvive e muore il capobranco. Se è protetto dal Guardiano, la protezione ha la precedenza e non esplode.
5. **Baby Lupo**: se attaccato dal branco non muore, si trasforma in Lupo all'alba (`RuoloIniziale` conservato, `Trasformato = true`).
6. **Diavolo**: se sbranato sopravvive con `DiavoloSbranato = true`. Se viene anche votato nello stesso turno, vince da solo. Il flag viene azzerato solo dopo il rogo, non all'inizio del giorno.
7. **Copione / Copione 2**: copiano il ruolo di un giocatore e la trasformazione avviene all'alba. Non possono copiare un altro Copione.
8. **Gufo**: il suo bersaglio finisce al ballottaggio del giorno successivo. Il ballottaggio avviene tra il giocatore votato e i giocatori segnati dal Gufo, non solo tra i gufati.
9. **Cacciatore**: dopo aver piazzato una trappola, la notte successiva non può piazzarne un'altra (`TrappolaInCooldown`).
10. **Sfigato**: appare CATTIVO alle indagini (Veggente/Becchino), ma non è un Lupo e non conta per la vittoria del branco.
11. **Annullatore**: blocca il potere del bersaglio per la notte. I Lupi sono immuni se ci sono più lupi vivi (branco).
12. **Imputato**: copia i poteri di chi lo ha puntato quella notte e li usa subito. I passi dell'Imputato vengono generati solo dopo i passi con priorità inferiore a 10.
13. **Vittoria**: Buoni se non restano lupi; Lupi se `lupi >= altri vivi`; Pareggio se non resta nessuno; Diavolo se sbranato e votato nello stesso turno.

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
- **Voto**: tally live, "Chiudi il voto", pannello pareggio, "Forza il rogo".
- **Ballottaggio**: candidati, tally live, pareggio, forza.
- **Esecuzione**: ultimo rogo, interruttore "Rivela il ruolo del condannato", "Cala la notte".
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
- **Esecuzione**: condannato e ruolo se rivelato.
- **Finita**: vincitore, eventi, ruoli di tutti.
- **Quaderno**: risultati privati raggruppati per turno.

### Dettagli tecnici importanti

- `App.razor` usa `prerender: false`: JS/localStorage sono disponibili in `OnAfterRenderAsync`,
  non in `OnInitialized`.
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

`Lupus.Core.Tests/Program.cs` è un harness console senza framework. Copre 14 scenari:

1. Protezione del Guardiano.
2. Morte senza protezione.
3. Trappola del Cacciatore.
4. Kamikaze.
5. Baby Lupo.
6. Puttana con Lupo.
7. Annullatore sulla Veggente.
8. Diavolo.
9. Copione.
10. Gufo e ballottaggio.
11. Imputato.
12. Serializzazione.
13. Validazione composizioni.
14. Condizioni di vittoria.

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

- Home → preset → nomi → "Crea la stanza" → codice e link visibili.
- Apri il link in due schede → reclama due posti → i telefoni sono in lobby senza ruolo.
- "Avvia la partita" → ogni telefono vede il proprio ruolo.
- "Inizia la notte" → il giocatore di turno agisce → il passo avanza per tutti.
- Conferma un risultato informativo (Veggente) → il quaderno si popola.
- "Risolvi la notte" → alba → "Vai alla discussione" → "Apri il voto".
- Vota da due telefoni → il narratore vede il tally → "Chiudi il voto".
- Pareggio → scegli il candidato → rogo; oppure ballottaggio del Gufo.
- "Cala la notte" → secondo turno.
- "Rivela i ruoli dei caduti" e "Rivela il ruolo del condannato" cambiano la vista dei telefoni.
- Fine partita → tutti i ruoli visibili su tutti i dispositivi.
- Esporta, ricarica, riprendi dalla home; chiudi la stanza e verifica il messaggio sui telefoni.
- Controlla la console del browser senza errori e il layout a 390px di larghezza.
