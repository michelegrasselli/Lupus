using Lupus.Core;

namespace Lupus.Web.Services;

/// <summary>
/// Conserva la partita corrente per il circuito Blazor (una scheda = una partita).
/// </summary>
public sealed class PartitaService
{
    public Partita? Corrente { get; set; }
}
