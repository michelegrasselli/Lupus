using System;
using System.Collections.Generic;
using System.Text;

namespace Lupus
{
    internal class Gufo: Personaggio
    {
        public Gufo()
        {
            priorita = 5;
            buono = true;
        }
        public override void Agisci(List<Personaggio> listaPersonaggi)
        {
            Console.WriteLine("Chi manderai al ballottaggio stanotte?");
            string personaGufo = Console.ReadLine();
            personaGufo = this.ControlloInput(personaGufo, listaPersonaggi);
            foreach (Personaggio personaggio in listaPersonaggi)
            {
                if (personaggio.nomePersona == personaGufo)
                {
                    personaggio.puntatoDalGufo = true;
                }
            }
        }
    }
}
