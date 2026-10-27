using System;
using System.Collections.Generic;
using System.Text;

namespace Lupus
{
    internal class BabyLupo: Personaggio
    {
        public BabyLupo()
        {
            priorita = 1000;
            buono = true;
        }
        public override void Agisci(List<Personaggio> listaPersonaggi)
        {
            Console.WriteLine("Non c'è molto che tu possa fare stanotte...");
        }

        public override void MuoriBaby(List<Personaggio>listaPersonaggi)
        {
            if ((this.puntatoDalLupo || this.puntatoDallImputatoComeLupo) && !this.puntatoDalGuardiano && !this.puntatoDallImputatoComeGuardiano)
            {
                int indiceBabyLupo = TrovaIndice(listaPersonaggi);
                listaPersonaggi.Insert(indiceBabyLupo, new Lupo());
                listaPersonaggi[indiceBabyLupo].nomePersona = this.nomePersona;
                listaPersonaggi.RemoveAt(indiceBabyLupo + 1);
                Console.WriteLine("Stanotte è nato un nuovo Lupo...");
            }
        }
    }
}
