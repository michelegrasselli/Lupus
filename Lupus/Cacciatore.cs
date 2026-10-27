using System;
using System.Collections.Generic;
using System.Text;

namespace Lupus
{
    internal class Cacciatore: Personaggio
    {
        public Cacciatore()
        {
            priorita = 2;
            buono = true;
        }

        public override void Agisci(List<Personaggio> listaPersonaggi)
        {
            if (!this.trappolaAlTurnoScorso)
            {
                Console.WriteLine("Su chi piazzerai una trappola stanotte?");
                string personaCacciatore = Console.ReadLine();
                personaCacciatore = this.ControlloInput(personaCacciatore, listaPersonaggi);
                foreach (Personaggio personaggio in listaPersonaggi)
                {
                    if (personaggio.nomePersona == personaCacciatore)
                    {
                        personaggio.puntatoDalCacciatore = true;
                    }
                    this.trappolaAlTurnoScorso = true;
                }
            }
            else
            {
                this.trappolaAlTurnoScorso = false;
                Console.WriteLine("Hai già piazzato una trappola la scorsa notte stanotte...");
                Thread.Sleep(3000);
            }
        }
    }
}
