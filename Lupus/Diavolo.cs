using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Lupus
{
    internal class Diavolo : Personaggio
    {
        public Diavolo()
        {
            priorita = 1000;
            buono = false;
        }

        public override void Muori()
        {
            if ((this.puntatoDalLupo || this.puntatoDallImputatoComeLupo) && !this.puntatoDalGuardiano && !this.puntatoDallImputatoComeGuardiano)
            {
                this.diavoloSbranato = true;
            }
        }
    }
}
