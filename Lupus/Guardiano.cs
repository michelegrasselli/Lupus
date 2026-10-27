using System;
using System.Collections.Generic;
using System.Text;

namespace Lupus
{
    internal class Guardiano : Personaggio
    {
        public Guardiano()
        {
            priorita = 4;
            buono = true;
        }
        public override void Agisci(List<Personaggio> listaPersonaggi)
        {
            Console.WriteLine("Chi proteggerai stanotte?");
            string personaGuardiano = Console.ReadLine();
            personaGuardiano = this.ControlloInput(personaGuardiano, listaPersonaggi);
            foreach (Personaggio personaggio in listaPersonaggi)
            {
                if (personaggio.nomePersona == personaGuardiano)
                {
                    personaggio.puntatoDalGuardiano = true;
                }
            }
        }
    }
}
