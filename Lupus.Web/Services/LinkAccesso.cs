using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Components;

namespace Lupus.Web.Services;

/// <summary>
/// Costruisce i link da mostrare al narratore. Il link primario segue l'origine
/// della richiesta (<see cref="NavigationManager.BaseUri"/>): in LAN è
/// <c>http://192.168.x.x:5092</c>, dietro Cloudflare Tunnel è
/// <c>https://dominio</c>. <see cref="ReteHelper"/> resta come scorciatoia per
/// la sola rete locale, utile se internet di casa cade.
/// </summary>
public sealed class LinkAccesso
{
    private readonly NavigationManager _nav;
    private readonly ReteHelper _rete;

    public LinkAccesso(NavigationManager nav, ReteHelper rete)
    {
        _nav = nav;
        _rete = rete;
    }

    private Uri Origine
    {
        get
        {
            if (Uri.TryCreate(_nav.BaseUri, UriKind.Absolute, out var uri))
                return uri;

            return new Uri($"http://localhost:{_rete.Porta}");
        }
    }

    /// <summary>Origine della richiesta senza barra finale (schema + host + porta).</summary>
    public string BaseRichiesta => Origine.GetLeftPart(UriPartial.Authority);

    /// <summary>La richiesta arriva da localhost: non è raggiungibile dai telefoni.</summary>
    public bool DaLocalhost => Origine.IsLoopback;

    /// <summary>L'host della richiesta è un dominio pubblico, non un indirizzo locale.</summary>
    public bool Pubblico => !DaLocalhost && !IpPrivato(Origine.Host);

    /// <summary>
    /// Base da consegnare ai telefoni: l'origine della richiesta, oppure la LAN
    /// quando il narratore sta usando localhost sul PC.
    /// </summary>
    public string BaseTelefoni => DaLocalhost ? _rete.BaseUrl : BaseRichiesta;

    /// <summary>Base di sola rete locale, valorizzata solo se diversa da quella primaria.</summary>
    public string? BaseLocale =>
        string.Equals(_rete.BaseUrl, BaseTelefoni, StringComparison.OrdinalIgnoreCase)
            ? null
            : _rete.BaseUrl;

    public string Entra(string codice) => $"{BaseTelefoni}/entra/{codice}";

    /// <summary>Link di sola rete locale, null quando coincide con quello primario.</summary>
    public string? EntraLocale(string codice) =>
        BaseLocale is null ? null : _rete.UrlEntra(codice);

    /// <summary>Etichetta del link primario, coerente con l'host da cui scrive il narratore.</summary>
    public string EtichettaPrincipale => Pubblico ? "Link pubblico" : "Link per i telefoni (rete locale)";

    private static bool IpPrivato(string host)
    {
        if (host.StartsWith('[') && host.EndsWith(']'))
            host = host[1..^1];

        if (!IPAddress.TryParse(host, out var ip))
            return false;

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var ottetti = ip.GetAddressBytes();
            return ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || (ottetti[0] & 0xFE) == 0xFC;
        }

        var b = ip.GetAddressBytes();
        return b[0] == 10
            || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
            || (b[0] == 192 && b[1] == 168)
            || (b[0] == 169 && b[1] == 254);
    }
}
