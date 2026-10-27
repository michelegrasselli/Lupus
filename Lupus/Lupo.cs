using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Runtime.CompilerServices;
using System.Text;

namespace Lupus
{
    internal class Lupo: Personaggio
    {

        public Lupo()
        {
            priorita = 3;
            buono = false;
        }

        public override void Agisci(List<Personaggio> listaPersonaggi)
        {
            base.Agisci(listaPersonaggi);
            Console.WriteLine("Chi verrà mangiato stanotte?");
            string personaPuntata = Console.ReadLine();
            personaPuntata = this.ControlloInput(personaPuntata, listaPersonaggi);
            foreach (Personaggio personaggio in listaPersonaggi)
            {
                if (personaggio.nomePersona == personaPuntata)
                {
                    personaggio.puntatoDalLupo = true;
                }
            }
        }
    }
}
