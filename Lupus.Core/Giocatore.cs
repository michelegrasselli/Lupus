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

    /// <summary>
    /// Se sono un Copione che ha copiato: il giocatore di cui ho preso il
    /// ruolo. Lo so sempre, dal momento della copia.
    /// </summary>
    public string? CopiaDaId { get; set; }

    /// <summary>Turno in cui è avvenuta la copia (la notte in cui ho puntato).</summary>
    public int CopiaTurno { get; set; }

    /// <summary>
    /// Chi ha copiato il mio ruolo. Il copiato non lo scopre il primo giorno:
    /// vede la lista solo dalla notte successiva, quando si sveglia insieme
    /// al suo doppione.</summary>
    public List<CopiaRuolo> CopieSubite { get; set; } = new();

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

/// <summary>
/// Registro di una copia subita: chi ha copiato il mio ruolo e in che turno.
/// </summary>
public sealed class CopiaRuolo
{
    public string CopioneId { get; set; } = "";
    public int Turno { get; set; }
}
