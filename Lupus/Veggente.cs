using System;
using System.Collections.Generic;
using System.Text;

namespace Lupus
{
    internal class Veggente: Personaggio
    {
        public Veggente()
        {
            priorita = 8;
            buono = true;
        }
        public override void Agisci(List<Personaggio> listaPersonaggi)
        {
            Console.WriteLine("Di chi vuoi sapere stanotte?");
            string personaVeggente = Console.ReadLine();
            personaVeggente = this.ControlloInput(personaVeggente, listaPersonaggi);
            foreach (Personaggio personaggio in listaPersonaggi)
            {
                if (personaggio.nomePersona == personaVeggente && !personaggio.morto)
                {
                    personaggio.puntatoDallaVeggente = true;
                    if (personaggio.buono)
                    {
                        Console.WriteLine("La persona di cui volevi sapere è BUONA!");
                    }
                    else
                    {
                        Console.WriteLine("La persona di cui volevi sapere è CATTIVA...");
                    }
                }
            }
        }
    }
}
