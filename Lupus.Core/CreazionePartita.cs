namespace Lupus.Core;

public static class CreazionePartita
{
    /// <summary>
    /// Controlla che la configurazione sia giocabile. Restituisce una lista
    /// vuota se va tutto bene, altrimenti i messaggi da mostrare al narratore.
    /// </summary>
    public static List<string> Valida(
        int numeroGiocatori,
        IReadOnlyDictionary<Ruolo, int> composizione,
        IReadOnlyList<string> nomi)
    {
        var errori = new List<string>();

        if (numeroGiocatori < 4)
            errori.Add("Servono almeno 4 giocatori.");

        var totaleRuoli = composizione.Values.Sum();
        if (totaleRuoli != numeroGiocatori)
            errori.Add($"La composizione ha {totaleRuoli} ruoli, ma i giocatori sono {numeroGiocatori}.");

        var lupi = composizione.GetValueOrDefault(Ruolo.Lupo);
        var altri = totaleRuoli - lupi;
        if (lupi == 0)
            errori.Add("Serve almeno un Lupo.");
        else if (lupi >= altri)
            errori.Add("I Lupi devono essere meno degli altri giocatori, altrimenti vincerebbero subito.");

        if (nomi.Count != numeroGiocatori)
            errori.Add($"Inserisci {numeroGiocatori} nomi (ne hai {nomi.Count}).");

        if (nomi.Any(string.IsNullOrWhiteSpace))
            errori.Add("Tutti i nomi devono essere compilati.");

        var duplicati = nomi
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .GroupBy(n => n.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicati.Count > 0)
            errori.Add($"Nomi duplicati: {string.Join(", ", duplicati)}.");

        return errori;
    }

    public static Partita Crea(
        IReadOnlyList<string> nomi,
        IReadOnlyDictionary<Ruolo, int> composizione,
        bool assegnaCasualmente = true)
    {
        var errori = Valida(nomi.Count, composizione, nomi);
        if (errori.Count > 0)
            throw new ArgumentException(string.Join(" ", errori));

        var ruoli = new List<Ruolo>();
        foreach (var info in Ruoli.Tutti)
        {
            var quantita = composizione.GetValueOrDefault(info.Ruolo);
            for (int i = 0; i < quantita; i++)
                ruoli.Add(info.Ruolo);
        }

        if (assegnaCasualmente)
            Mescola(ruoli);

        var partita = new Partita();
        for (int i = 0; i < nomi.Count; i++)
        {
            partita.Giocatori.Add(new Giocatore
            {
                Nome = nomi[i].Trim(),
                Ruolo = ruoli[i]
            });
        }

        // Il capobranco viene scelto a caso da Avvia, così non è sempre il
        // primo Lupo della lista (che dipende dall'ordine dei nomi).
        MotoreLupus.Avvia(partita);
        return partita;
    }

    private static void Mescola<T>(IList<T> lista)
    {
        for (int i = lista.Count - 1; i > 0; i--)
        {
            int j = Random.Shared.Next(i + 1);
            (lista[i], lista[j]) = (lista[j], lista[i]);
        }
    }
}
