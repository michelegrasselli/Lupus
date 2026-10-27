# 🐺 Lupus in Tabula · Assistente narratore

Applicazione web per gestire una partita di **Lupus in Tabula** dal lato del narratore.
Niente carta, niente appunti: ruoli, notti, voti, ballottaggi e timer in un'unica schermata
pensata per tablet e telefono.

> Modalità attuale: **solo narratore**. Il dispositivo passa di mano in mano per rivelare i ruoli.
> L'architettura è però pronta per aggiungere in futuro i telefoni dei giocatori (vedi `AGENTS.md`).

## Requisiti

- [.NET SDK 10.0.200](https://dotnet.microsoft.com/) o superiore (target `net10.0`).
- Un browser moderno.

Nessun database, nessun account, nessun package NuGet esterno.

## Avvio rapido

Dalla root del repository:

```powershell
# Avvia l'app web (profilo HTTP, senza certificati)
dotnet run --project Lupus.Web --launch-profile http
```

Apri <http://localhost:5092>.

### Eseguibile (publish)

Per ottenere un `.exe` avviabile con doppio clic (con CSS, JavaScript e Blazor inclusi):

```powershell
dotnet publish Lupus.Web/Lupus.Web.csproj -c Release
```

Poi avvia:

```powershell
.\Lupus.Web\bin\Release\net10.0\publish\Lupus.Web.exe
```

L'app ascolta su <http://localhost:5092> (configurato in `appsettings.json`).

> **Attenzione:** non avviare l'exe dentro `bin\Debug\net10.0`. Quella cartella è
> l'output intermedio di build: non contiene `wwwroot` né `_framework`, quindi l'app
> parte sulla porta 5000 e serve CSS/JS vuoti o in errore. Usa `dotnet run` per lo
> sviluppo oppure `dotnet publish` per l'eseguibile.

### Build e test

```powershell
# Build dell'intera soluzione
dotnet build Lupus.slnx

# Test del motore di gioco (14 scenari)
dotnet run --project Lupus.Core.Tests/Lupus.Core.Tests.csproj
```

## Come si gioca

1. **Setup** (`/`): scegli un preset o imposta numero e ruoli, inserisci i nomi, avvia la partita.
2. **Notte**: il narratore segue i passi generati in ordine di priorità, seleziona i bersagli
   e conferma i risultati informativi.
3. **Alba**: vengono mostrati gli eventi pubblici, i caduti e le informazioni riservate al narratore.
4. **Giorno / Voto / Ballottaggio**: discussione con timer, voto e eventuale ballottaggio del Gufo.
5. **Esecuzione**: rogo, ruolo rivelato, poi di nuovo notte.
6. **Vittoria**: pannello finale con il riepilogo dei ruoli.

La partita viene salvata automaticamente nel browser (`localStorage`, chiave `lupus.partita.v1`)
e può essere esportata/importata come file JSON per cambiare dispositivo.

## Funzionalità

- **18 ruoli** con preset Rapida (6), Classica (8), Villaggio (10), Caos (14), Completa (18).
- **Macchina a stati** esplicita: `NotteIntro → Notte → Alba → Giorno → Voto → Ballottaggio → Esecuzione → …`
- **Diario di gioco** filtrabile, con eventi riservati al narratore.
- **Timer** configurabile per discussione e voto, con beep a fine tempo.
- **Modale ruoli** con rivelazione uno alla volta.
- **Salvataggio locale**, export/import JSON e ripresa automatica.
- **Tema scuro** responsive per tablet e telefono.

## Struttura

```
Lupus.slnx
├── Lupus/              # vecchia app console (riferimento storico, invariata)
├── Lupus.Core/         # motore di gioco puro e serializzabile
├── Lupus.Core.Tests/   # harness console con 14 scenari di regole
└── Lupus.Web/          # Blazor Server, UI italiana
```

Il motore (`Lupus.Core`) non conosce Blazor, JavaScript, localStorage o HTTP: può essere
riutilizzato da una futura API per i telefoni dei giocatori.

## Documentazione

- `AGENTS.md` — contesto completo del progetto: architettura, regole, correzioni rispetto
  alla versione console, convenzioni e roadmap multi-dispositivo.
- `Lupus.Core.Tests/Program.cs` — scenari di regole verificabili.

## Note

- Il log contiene informazioni da narratore (ruoli, poteri): non mostrarlo ai giocatori.
- Il timer non conserva i secondi rimanenti tra un reload: riparte dalla durata configurata.
- I salvataggi sono per browser/dispositivo; usa export/import per spostarli.
