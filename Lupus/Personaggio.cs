using System;
using System.Collections.Generic;
using System.Text;

namespace Lupus
{
    internal abstract class Personaggio
    {
        #region
        public string nomePersona;
        public bool morto = false;
        public int priorita;
        public bool buono;
        public bool capobranco = false;
        public bool trappolaAlTurnoScorso = false;
        #endregion

        //da resettare

        #region
        public bool puntatoDalLupo = false;
        public bool mortoQuestoTurno = false;
        public bool puntatoDalGuardiano = false;
        public bool puntatoDallAnnullatore = false;
        public bool puntatoDalBecchino = false;
        public bool puntatoDallaVeggente = false;
        public bool puntatoDallaPuttana = false;
        public bool puttanaAndataConUnLupo = false;
        public bool puntatoDalCopione = false;
        public bool puntatoDallaSpia = false;
        public bool puntatoDallImputatoComeLupo = false;
        public bool puntatoDallImputatoComeGuardiano = false;
        public bool puntatoDallImputatoComePuttana = false;
        public bool puntatoDallImputatoComeVeggente = false;
        public bool puntatoDallImputatoComeSpia = false;
        public bool puntatoDallImputatoComeCacciatore = false;
        public bool puntatoDalCopione2 = false;
        public bool diavoloSbranato = false;
        public bool puntatoDalCacciatore = false;
        public bool puntatoDalGufo = false;
        public bool puntatoDallImputatoComeGufo = false;
        #endregion

        public virtual void Agisci(List<Personaggio> listaPersonaggi)
        {
        }
        public void AgisciDaMorto()
        {
            Console.WriteLine("Sei morto...");
        }

        public virtual void MuoriBaby(List <Personaggio> listaPersonaggi)
        {
        }


        public virtual void Muori()
        {
            if (!this.morto)
            {
                if ((this.puntatoDalLupo || this.puttanaAndataConUnLupo || this.puntatoDallImputatoComeLupo) && !this.puntatoDalGuardiano && !this.puntatoDallImputatoComeGuardiano)
                {
                    this.morto = true;
                    this.mortoQuestoTurno = true;
                    if (this is Lupo)
                    {
                        this.capobranco = false;
                    }
                    Console.WriteLine($"Stanotte ci ha lasciato {this.nomePersona}...");
                }
            }
        }

        public int TrovaIndice(List<Personaggio> lista)
        {
            for (int i = 0; i < lista.Count; i++)
            {
                if (lista[i] == this)
                {
                    return i;
                }
            }
            return -1;
        }

        public string ControlloInput(string input, List<Personaggio> lista)
        {
            input = input.ToLower();
            bool inputValido = false;
            do
            {
                foreach (Personaggio personaggio in lista)
                {
                    if (personaggio.nomePersona == input)
                    {
                        inputValido = true;
                    }
                }
                if (!inputValido)
                {
                    Console.WriteLine("Il nome che hai scritto non è tra quello delle persone che stanno giocando, riprova...");
                    input = Console.ReadLine();
                }
            } while (!inputValido);
            return input;
        }
    }
}
