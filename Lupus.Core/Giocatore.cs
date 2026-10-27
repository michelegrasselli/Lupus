namespace Lupus.Core;

public sealed class Giocatore
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Nome { get; set; } = "";
    public Ruolo Ruolo { get; set; }
    public Ruolo? RuoloIniziale { get; set; }
    public bool Vivo { get; set; } = true;
    public bool Capobranco { get; set; }
    public bool MortoInNotte { get; set; }
    public bool MortoAlRogo { get; set; }
    public bool Trasformato { get; set; }
    public bool DiavoloSbranato { get; set; }
    public bool TrappolaInCooldown { get; set; }

    public string Iniziali
    {
        get
        {
            var parti = Nome.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parti.Length == 0) return "?";
            if (parti.Length == 1) return parti[0][..1].ToUpperInvariant();
            return (parti[0][..1] + parti[1][..1]).ToUpperInvariant();
        }
    }

    public string RuoloAttualeNome => Ruoli.Nome(Ruolo);
}
