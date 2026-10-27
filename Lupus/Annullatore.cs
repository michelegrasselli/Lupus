using System;
using System.Collections.Generic;
using System.Text;

namespace Lupus
{
    internal class Annullatore: Personaggio
    {
        public Annullatore()
        {
            priorita = 1;
            buono = false;
        }
        public override void Agisci(List<Personaggio> listaPersonaggi)
        {
            Console.WriteLine("A chi annullerai il potere stanotte?");
            string personaAnnullata = Console.ReadLine();
            personaAnnullata = this.ControlloInput(personaAnnullata, listaPersonaggi);
            foreach (Personaggio personaggio in listaPersonaggi)
            {
                if (personaggio.nomePersona == personaAnnullata)
                {
                    personaggio.puntatoDallAnnullatore = true;
                }
            } 
        }
    }
}
