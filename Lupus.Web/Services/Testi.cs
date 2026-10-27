using Lupus.Core;

namespace Lupus.Web.Services;

public static class Testi
{
    public static string FaseBreve(FasePartita fase) => fase switch
    {
        FasePartita.NotteIntro => "Preparazione notte",
        FasePartita.Notte => "Notte",
        FasePartita.Alba => "Alba",
        FasePartita.Giorno => "Giorno",
        FasePartita.Voto => "Voto",
        FasePartita.Ballottaggio => "Ballottaggio",
        FasePartita.Esecuzione => "Fine giornata",
        FasePartita.Finita => "Partita conclusa",
        _ => fase.ToString()
    };

    public static string TitoloVittoria(EsitoPartita? esito) => esito switch
    {
        EsitoPartita.Buoni => "I Buoni vincono!",
        EsitoPartita.Lupi => "I Lupi vincono!",
        EsitoPartita.Diavolo => "Il Diavolo vince!",
        EsitoPartita.Pareggio => "Pareggio",
        _ => "Partita conclusa"
    };

    public static string SottotitoloVittoria(EsitoPartita? esito) => esito switch
    {
        EsitoPartita.Buoni => "Il villaggio ha eliminato tutti i Lupi. La notte è finita.",
        EsitoPartita.Lupi => "Il branco ha raggiunto o superato i superstiti. Il villaggio è caduto.",
        EsitoPartita.Diavolo => "Sbranato e votato nello stesso giorno: il Diavolo ha ottenuto la sua vendetta.",
        EsitoPartita.Pareggio => "Non è rimasto nessuno a raccontarlo.",
        _ => "La partita è terminata."
    };

    public static string IconaVittoria(EsitoPartita? esito) => esito switch
    {
        EsitoPartita.Buoni => "🌅",
        EsitoPartita.Lupi => "🐺",
        EsitoPartita.Diavolo => "😈",
        EsitoPartita.Pareggio => "⚖️",
        _ => "🏁"
    };

    public static string ClasseVittoria(EsitoPartita? esito) => esito switch
    {
        EsitoPartita.Buoni => "win-good",
        EsitoPartita.Lupi => "win-evil",
        EsitoPartita.Diavolo => "win-devil",
        EsitoPartita.Pareggio => "win-draw",
        _ => "win-draw"
    };
}
