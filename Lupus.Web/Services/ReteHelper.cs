using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Lupus.Web.Services;

/// <summary>
/// Calcola l'indirizzo con cui i telefoni sulla stessa Wi-Fi possono
/// raggiungere il server. Non conosce Blazor: legge solo configurazione e
/// interfacce di rete.
/// </summary>
public sealed class ReteHelper
{
    private readonly IConfiguration _configurazione;

    public ReteHelper(IConfiguration configurazione)
    {
        _configurazione = configurazione;
    }

    /// <summary>Porta HTTP usata da Kestrel (5092 di default).</summary>
    public int Porta
    {
        get
        {
            var urls = _configurazione["Urls"];
            if (string.IsNullOrWhiteSpace(urls)) return 5092;

            foreach (var voce in urls.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                if (Uri.TryCreate(voce.Trim(), UriKind.Absolute, out var uri) && uri.Port > 0)
                    return uri.Port;
            }

            return 5092;
        }
    }

    /// <summary>Primo IPv4 privato dell'host, oppure null se non disponibile.</summary>
    public string? IpLocale
    {
        get
        {
            var candidati = new List<(int Priorita, string Ip)>();

            foreach (var scheda in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (scheda.OperationalStatus != OperationalStatus.Up) continue;
                if (scheda.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
                    continue;

                var prioritaScheda = PrioritaScheda(scheda);

                foreach (var indirizzo in scheda.GetIPProperties().UnicastAddresses)
                {
                    if (indirizzo.Address.AddressFamily != AddressFamily.InterNetwork) continue;

                    var ip = indirizzo.Address.ToString();
                    if (IPAddress.IsLoopback(indirizzo.Address)) continue;
                    if (ip.StartsWith("169.254.", StringComparison.Ordinal)) continue;

                    // La scheda pesa più dell'intervallo: così un Wi-Fi reale
                    // batte sempre gli adattatori virtuali di VMware/Hyper-V.
                    candidati.Add((prioritaScheda * 10 + PrioritaIp(ip), ip));
                }
            }

            if (candidati.Count == 0)
            {
                try
                {
                    foreach (var indirizzo in Dns.GetHostAddresses(Dns.GetHostName()))
                    {
                        if (indirizzo.AddressFamily != AddressFamily.InterNetwork) continue;
                        if (IPAddress.IsLoopback(indirizzo)) continue;

                        var ip = indirizzo.ToString();
                        if (ip.StartsWith("169.254.", StringComparison.Ordinal)) continue;
                        candidati.Add((PrioritaIp(ip), ip));
                    }
                }
                catch
                {
                    // Se la risoluzione del nome fallisce, resta il fallback.
                }
            }

            return candidati
                .OrderBy(c => c.Priorita)
                .Select(c => c.Ip)
                .FirstOrDefault();
        }
    }

    public string BaseUrl =>
        IpLocale is { } ip ? $"http://{ip}:{Porta}" : $"http://localhost:{Porta}";

    public string UrlEntra(string codice) => $"{BaseUrl}/entra/{codice}";

    public string UrlGioca(string codice) => $"{BaseUrl}/gioca/{codice}";

    private static int PrioritaScheda(NetworkInterface scheda)
    {
        var nome = $"{scheda.Name} {scheda.Description}";

        // Gli adattatori virtuali hanno spesso un 192.168.x ma non sono
        // raggiungibili dai telefoni: vanno sempre in fondo.
        string[] virtuali =
        {
            "VMware", "VirtualBox", "Hyper-V", "vEthernet", "WSL",
            "Docker", "TAP-", "Tailscale", "ZeroTier", "Bluetooth",
            "Loopback", "VPN", "WireGuard"
        };

        if (virtuali.Any(v => nome.Contains(v, StringComparison.OrdinalIgnoreCase)))
            return 50;

        var tipo = scheda.NetworkInterfaceType switch
        {
            NetworkInterfaceType.Wireless80211 => 0,
            NetworkInterfaceType.Ethernet
                or NetworkInterfaceType.GigabitEthernet
                or NetworkInterfaceType.FastEthernetT
                or NetworkInterfaceType.FastEthernetFx => 1,
            _ => 4
        };

        var senzaGateway = scheda.GetIPProperties().GatewayAddresses.Any(g =>
            g.Address.AddressFamily == AddressFamily.InterNetwork &&
            !g.Address.Equals(IPAddress.Any))
            ? 0
            : 2;

        return tipo + senzaGateway;
    }

    private static int PrioritaIp(string ip)
    {
        // Preferisce gli intervalli privati tipici delle reti domestiche.
        if (ip.StartsWith("192.168.", StringComparison.Ordinal)) return 0;
        if (ip.StartsWith("10.", StringComparison.Ordinal)) return 1;

        if (ip.StartsWith("172.", StringComparison.Ordinal))
        {
            var parti = ip.Split('.');
            if (parti.Length > 1 && int.TryParse(parti[1], out var secondo) && secondo is >= 16 and <= 31)
                return 2;
        }

        return 3;
    }
}
