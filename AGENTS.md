# AGENTS.md · Lupus in Tabula

File di contesto per chi lavora su questo repository. Leggilo prima di modificare il motore o la UI.

## 1. Obiettivo del progetto

Trasformare il vecchio narratore da terminale di **Lupus in Tabula** in un'applicazione web usabile da tablet o telefono, mantenendo il motore di gioco separato dalla UI.

Stato attuale:

- **Narratore-only**: una sola persona (il narratore) usa l'app e passa il dispositivo ai giocatori per rivelare i ruoli.
- **Nessun account, nessun database**: la partita vive nel browser e può essere esportata/importata come JSON.
- **Pronto per il multi-dispositivo**: il motore è una libreria pura, serializzabile, senza dipendenze da ASP.NET o dal browser. In futuro si potrà aggiungere un'API/SignalR per i telefoni dei giocatori senza riscrivere le regole.

## 2. Stack e prerequisiti

- .NET SDK **10.0.200** (target `net10.0`).
- ASP.NET Core + **Blazor Server** (interactive server, prerender disabilitato).
- CSS puro in `Lupus.Web/wwwroot/app.css`.
- JavaScript minimo in `Lupus.Web/wwwroot/js/lupus.js` per `localStorage`, download JSON e beep.
- Nessun package NuGet esterno oltre a quelli del framework.

## 3. Comandi

Esegui dalla root `C:\Users\miche\Desktop\PROGETTI_AI\Lupus`.

```powershell
# Build dell'intera soluzione
dotnet build Lupus.slnx

# Test del motore (harness console, 14 scenari)
dotnet run --project Lupus.Core.Tests/Lupus.Core.Tests.csproj

# Avvio web (profilo HTTP, senza certificati)
dotnet run --project Lupus.Web --launch-profile http
# http://localhost:5092

# Build del solo web
dotnet build Lupus.Web/Lupus.Web.csproj

# Eseguibile distribuibile (include wwwroot e _framework)
dotnet publish Lupus.Web/Lupus.Web.csproj -c Release
# → Lupus.Web/bin/Release/net10.0/publish/Lupus.Web.exe
```

> La porta 5092 è impostata in `appsettings.json` (`"Urls"`), quindi vale anche per
> l'exe pubblicato. `launchSettings.json` viene usato solo da `dotnet run`/IDE: l'exe
> dentro `bin\Debug\net10.0` non è distribuibile (manca `wwwroot`) e senza `Urls`
> ripiegherebbe sulla porta 5000.

Il vecchio progetto console `Lupus/` resta compilabile e **non va modificato** salvo richiesta esplicita: serve come riferimento storico delle regole.

## 4. Struttura della soluzione

```
Lupus.slnx
├── Lupus/                  # vecchia app console, invariata
├── Lupus.Core/             # motore di gioco puro, riutilizzabile
├── Lupus.Core.Tests/       # harness console con scenari di regole
└── Lupus.Web/              # Blazor Server, UI italiana
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
| `Program.cs` | Registra Razor Components, `PartitaService`, `ArchivioPartita`; static assets; interactive server. |
| `Components/App.razor` | HTML base italiano, `prerender: false`, CSS, script `lupus.js`. |
| `Components/Pages/Home.razor` | Setup partita: preset, conteggi ruoli, nomi, validazione, ripresa salvataggio. |
| `Components/Pages/PartitaGioco.razor` | Dashboard di gioco (`/partita`): timeline fasi, passi notte, alba, discussione, voto, ballottaggio, rogo, vittoria, sidebar, log, timer, salvataggi, modale ruoli. |
| `Components/Shared/PlayerPicker.razor` | Griglia riutilizzabile di selezione giocatore. |
| `Components/Shared/TimerCountdown.razor` | Timer con preset, pausa/reset, beep a fine tempo, callback durata. |
| `Services/PartitaService.cs` | Partita corrente per circuito Blazor (una scheda = una partita). |
| `Services/ArchivioPartita.cs` | localStorage, export/import JSON, beep, conferma. |
| `Services/Testi.cs` | Etichette italiane di fasi, vittorie e classi CSS. |
| `wwwroot/app.css` | Tema scuro "notte", layout responsive, tutti i componenti. |
| `wwwroot/js/lupus.js` | `lupusStorage.save/load/remove/download/beep/scrollLog`. |

> **Nota sul nome della pagina**: il componente si chiama `PartitaGioco`, non `Partita`, per evitare che la classe generata `Lupus.Web.Components.Pages.Partita` oscuri `Lupus.Core.Partita` nel codice generato. La route resta `/partita`.

## 5. Flusso di gioco e macchina a stati

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
- `VaiAlGiorno` passa ad `Giorno` (o `Finita` se l'esito è già deciso).
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

## 6. Regole e correzioni rispetto alla versione console

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

## 7. UI e stato della pagina

`PartitaGioco.razor` gestisce:

- **Timeline** delle fasi con stato `done/current/todo`.
- **NotteIntro**: composizione, capobranco, pulsanti "Rivela i ruoli" e "Inizia la notte".
- **Notte**: passo corrente, player picker filtrato su `OpzioniIds`, risultato con conferma se necessario, log delle azioni della notte, pulsante "Risolvi la notte" quando `NotteCompleta`.
- **Alba**: eventi pubblici, caduti con ruolo, eventuale ballottaggio del Gufo, eventi riservati al narratore.
- **Giorno**: vivi, eventuale candidato del Gufo, apertura voto.
- **Voto**: player picker sui vivi, conferma voto.
- **Ballottaggio**: candidati e scelta finale.
- **Esecuzione**: ultimo rogo, ruolo, prossima notte.
- **Finita**: pannello vincitore e riepilogo ruoli.
- **Sidebar**: timer, elenco giocatori con reveal, diario filtrabile, salvataggio/export/import/abbandono.
- **Modale ruoli**: reveal uno alla volta, con tracciamento dei ruoli già visti (`ruoliRivelati`, non persistito).
- **Autosave** dopo ogni transizione e dopo il cambio durata timer.

### Dettagli tecnici importanti

- `App.razor` usa `prerender: false`: JS/localStorage sono disponibili in `OnAfterRenderAsync`, non in `OnInitialized`.
- La pagina carica la partita da `PartitaService.Corrente`; se manca, prova `Archivio.CaricaAsync()` al primo render.
- `TimerCountdown` viene ricreato con `@key="TimerKey"` quando cambiano partita/turno/fase o dopo un import (`timerReset`).
- `TimerCountdown.OnDurataCambiata` aggiorna `Partita.TimerSecondi` e salva.
- Il timer **non** persiste i secondi rimanenti: dopo un reload riparte dalla durata configurata. È una scelta consapevole, non un bug.
- Il diario mostra le ultime 250 voci e fa auto-scroll tramite `lupusStorage.scrollLog`.
- Il log contiene informazioni da narratore (ruoli, poteri). Non mostrarlo ai giocatori.

## 8. Persistenza

- Chiave localStorage: `lupus.partita.v1` (vedi `ArchivioPartita.Chiave`).
- Formato: `SalvataggioPartita { Versione, SalvataIl, Partita }`, JSON con enum stringa.
- Export: download di `lupus-yyyyMMdd-HHmm.json`.
- Import: lettura file, `Serializzazione.Deserializza`, controllo giocatori, sostituzione partita corrente e salvataggio.
- `Partita.Normalizza()` ripara liste null dopo un import.
- `Partita.Versione` è pensato per migrazioni future.

## 9. Test

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

## 10. Convenzioni

- UI in **italiano**, tono da narratore di paese.
- Tema scuro "notte", accento viola/blu, responsive per tablet e telefono.
- Nessuna dipendenza da Bootstrap o framework CSS.
- Preferire classi CSS semantiche già presenti in `app.css`; aggiungerne di nuove solo se necessario.
- Il motore non deve conoscere Blazor, JS, localStorage o HTTP.
- Le transizioni di stato passano sempre da `MotoreLupus`; la UI non modifica mai direttamente `Vivi`, `Ruolo`, `Fase`, ecc. (unica eccezione: flag di presentazione e `TimerSecondi`).
- Non eliminare il progetto console `Lupus/`: è il riferimento storico.

## 11. Limiti attuali

- Solo narratore: i giocatori non hanno un'interfaccia propria.
- Nessuna autenticazione: chiunque abbia accesso al browser vede tutti i ruoli.
- Salvataggio locale per browser/dispositivo; per cambiare dispositivo serve export/import.
- Il timer non conserva i secondi rimanenti tra reload.
- Lo stato "ruolo già rivelato" nella modale non è persistito.
- Nessun test UI automatizzato; la verifica è manuale nel browser.
- La partita console e quella web sono separate: non condividono lo stato.

## 12. Roadmap multi-dispositivo

Obiettivo: permettere ai giocatori di usare il telefono, mantenendo il narratore come regista.

1. **Estrarre un servizio applicativo** attorno a `MotoreLupus` (comandi: `EseguiPasso`, `Vota`, ecc.) con DTO e validazione.
2. **Aggiungere `Lupus.Api`** (minimal API o SignalR) con:
   - creazione partita e codice stanza;
   - join con nome e token giocatore;
   - endpoint che restituiscono solo le informazioni consentite (ruolo proprio, azioni disponibili, stato pubblico);
   - broadcast degli eventi pubblici e dei risultati.
3. **Sostituire `PartitaService`** con un client che parla con l'API; mantenere `ArchivioPartita` per la modalità offline del narratore.
4. **Persistenza server** (in-memory con scadenza, poi database) usando lo stesso JSON di `Serializzazione`.
5. **Privacy**: il narratore vede tutto; il giocatore vede solo il proprio ruolo e le azioni che può compiere. Filtrare log ed eventi riservati.
6. **Reconnect**: token di sessione, riconnessione SignalR, stato idempotente.
7. **Test**: aggiungere test di integrazione per autorizzazione e per le viste filtrate.

Il motore è già serializzabile e senza stato globale, quindi il passaggio a un'API non richiede di riscrivere le regole.

## 13. Checklist rapida prima di consegnare

```powershell
dotnet build Lupus.slnx
dotnet run --project Lupus.Core.Tests/Lupus.Core.Tests.csproj
dotnet run --project Lupus.Web --launch-profile http
```

Poi nel browser:

- Setup → preset → nomi → "Inizia la partita".
- "Rivela ruoli" → reveal di un giocatore → chiusura modale.
- "Inizia la notte" → completa i passi → conferma risultati → "Risolvi la notte".
- Alba → "Vai alla discussione" → "Apri il voto" → voto → eventuale ballottaggio.
- Rogo → "Cala la notte".
- Salva, esporta, ricarica la pagina, riprendi il salvataggio.
- Controlla console browser senza errori.
