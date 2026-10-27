using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Lupus
{
    internal class Puttana: Personaggio
    {
        public Puttana()
        {
            priorita = 7;
            buono = true;
        }
        public override void Agisci(List<Personaggio> listaPersonaggi)
        {
            Console.WriteLine("Con chi vuoi spassartela stanotte?");
            string personaPuttana = Console.ReadLine();
            personaPuttana = this.ControlloInput(personaPuttana, listaPersonaggi);
            foreach (Personaggio personaggio in listaPersonaggi)
            {
                if (personaggio.nomePersona == personaPuttana)
                {
                    personaggio.puntatoDallaPuttana = true;
                    if (personaggio is Lupo)
                    {
                        this.puttanaAndataConUnLupo = true;
                    }
                }
            }
        }
    }
}
