using System;
using System.Collections.Generic;
using System.Text;

namespace Lupus
{
    internal class Becchino : Personaggio
    {
        public Becchino()
        {
            priorita = 6;
            buono = true;
        }
        public override void Agisci(List<Personaggio> listaPersonaggi)
        {
            bool ciSonoMorti = false;
            foreach (Personaggio personaggio in listaPersonaggi)
            {
                if (personaggio.morto)
                {
                    ciSonoMorti = true;
                }
            }
            if (!ciSonoMorti)
            {
                Console.WriteLine("Non ci sono ancora morti di cui puoi sapere...");
            }
            else
            {
                Console.WriteLine("Di quale morto vuoi sapere stanotte?");
                string personaBecchino = Console.ReadLine();
                personaBecchino = this.ControlloInput(personaBecchino, listaPersonaggi);
                foreach (Personaggio personaggio in listaPersonaggi)
                {
                    if (personaggio.nomePersona == personaBecchino && personaggio.morto)
                    {
                        personaggio.puntatoDalBecchino = true;
                        if (personaggio.buono)
                        {
                            Console.WriteLine("La persona di cui volevi sapere era BUONA!");
                        }
                        else
                        {
                            Console.WriteLine("La persona di cui volevi sapere era CATTIVA...");
                        }
                    }
                }
            }
        }
    }
}
