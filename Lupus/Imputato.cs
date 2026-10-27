using System;
using System.Collections.Generic;
using System.Text;

namespace Lupus
{
    internal class Imputato: Personaggio
    {
        public Imputato()
        {
            priorita = 10;
            buono = true;
        }
        public override void Agisci(List<Personaggio> listaPersonaggi)
        {
            bool puntatoDaQualcuno = false;
            if (this.puntatoDalLupo)
            {
                puntatoDaQualcuno = true;
                Console.WriteLine("Puoi utilizzare il potere dello sbrano stanotte...");
                Console.WriteLine("Chi verrà mangiato stanotte?");
                string personaPuntata = Console.ReadLine();
                personaPuntata = this.ControlloInput(personaPuntata, listaPersonaggi);
                foreach (Personaggio personaggio in listaPersonaggi)
                {
                    if (personaggio.nomePersona == personaPuntata)
                    {
                        personaggio.puntatoDallImputatoComeLupo = true;
                    }
                }
                Thread.Sleep(2000);
            }
            if (this.puntatoDalGuardiano)
            {
                puntatoDaQualcuno = true;
                Console.WriteLine("Puoi utilizzare il potere del Guardiano stanotte...");
                Console.WriteLine("Chi proteggerai stanotte?");
                string personaGuardiano = Console.ReadLine();
                personaGuardiano = this.ControlloInput(personaGuardiano, listaPersonaggi);
                foreach (Personaggio personaggio in listaPersonaggi)
                {
                    if (personaggio.nomePersona == personaGuardiano)
                    {
                        personaggio.puntatoDallImputatoComeGuardiano = true;
                    }
                }
                Thread.Sleep(2000);
            }
            if (this.puntatoDallaPuttana)
            {
                puntatoDaQualcuno = true;
                Console.WriteLine("Puoi utilizzare il potere della Puttana stanotte...");
                Console.WriteLine("Con chi vuoi spassartela stanotte?");
                string personaPuttana = Console.ReadLine();
                personaPuttana = this.ControlloInput(personaPuttana, listaPersonaggi);
                foreach (Personaggio personaggio in listaPersonaggi)
                {
                    if (personaggio.nomePersona == personaPuttana)
                    {
                        personaggio.puntatoDallImputatoComePuttana = true;

                        if (personaggio is Lupo)
                        {
                            this.puttanaAndataConUnLupo = true;
                        }
                    }
                }
                Thread.Sleep(2000);
            }
            if (this.puntatoDallaVeggente)
            {
                puntatoDaQualcuno = true;
                Console.WriteLine("Puoi utilizzare il potere della Veggente stanotte...");
                Console.WriteLine("Di chi vuoi sapere stanotte?");
                string personaVeggente = Console.ReadLine();
                personaVeggente = this.ControlloInput(personaVeggente, listaPersonaggi);
                foreach (Personaggio personaggio in listaPersonaggi)
                {
                    if (personaggio.nomePersona == personaVeggente && !personaggio.morto)
                    {
                        personaggio.puntatoDallImputatoComeVeggente = true;
                        if (personaggio.buono)
                        {
                            Console.WriteLine("La persona di cui volevi sapere è BUONA!");
                        }
                        else
                        {
                            Console.WriteLine("La persona di cui volevi sapere è CATTIVA...");
                        }
                    }
                }
                Thread.Sleep(2000);
            }
            if(this.puntatoDallaSpia)
            {
                puntatoDaQualcuno = true;
                Console.WriteLine("Puoi utilizzare il potere della Spia stanotte...");
                Console.WriteLine("Chi spierai stanotte?");
                string personaSpiata = Console.ReadLine();
                personaSpiata = this.ControlloInput(personaSpiata, listaPersonaggi);
                foreach (Personaggio personaggio in listaPersonaggi)
                {
                    if (personaggio.nomePersona == personaSpiata)
                    {
                        personaggio.puntatoDallImputatoComeSpia = true;
                    }
                }
                foreach (Personaggio personaggio in listaPersonaggi)
                {
                    bool puntatoDaQualcuno2 = false;
                    if (personaggio.puntatoDallImputatoComeSpia)
                    {
                        if (personaggio.puntatoDallAnnullatore)
                        {
                            foreach (Personaggio personaggio2 in listaPersonaggi)
                            {
                                if (personaggio2 is Annullatore)
                                {
                                    Console.WriteLine($"La persona che hai spiato è stata puntata da {personaggio2.nomePersona} stanotte");
                                    Thread.Sleep(2000);
                                    puntatoDaQualcuno2 = true;
                                }
                            }
                        }
                        if (personaggio.puntatoDalLupo)
                        {
                            foreach (Personaggio personaggio2 in listaPersonaggi)
                            {
                                if (personaggio2.capobranco)
                                {
                                    Console.WriteLine($"La persona che hai spiato è stata puntata da {personaggio2.nomePersona} stanotte");
                                    Thread.Sleep(2000);
                                    puntatoDaQualcuno2 = true;
                                }
                            }
                        }
                        if (personaggio.puntatoDalGuardiano)
                        {
                            foreach (Personaggio personaggio2 in listaPersonaggi)
                            {
                                if (personaggio2 is Guardiano)
                                {
                                    Console.WriteLine($"La persona che hai spiato è stata puntata da {personaggio2.nomePersona} stanotte");
                                    Thread.Sleep(2000);
                                    puntatoDaQualcuno2 = true;
                                }
                            }
                        }
                        if (personaggio.puntatoDalBecchino)
                        {
                            foreach (Personaggio personaggio2 in listaPersonaggi)
                            {
                                if (personaggio2 is Becchino)
                                {
                                    Console.WriteLine($"La persona che hai spiato è stata puntata da {personaggio2.nomePersona} stanotte");
                                    Thread.Sleep(2000);
                                    puntatoDaQualcuno2 = true;
                                }
                            }
                        }
                        if (personaggio.puntatoDallaPuttana)
                        {
                            foreach (Personaggio personaggio2 in listaPersonaggi)
                            {
                                if (personaggio2 is Puttana)
                                {
                                    Console.WriteLine($"La persona che hai spiato è stata puntata da {personaggio2.nomePersona} stanotte");
                                    Thread.Sleep(2000);
                                    puntatoDaQualcuno2 = true;
                                }
                            }
                        }
                        if (personaggio.puntatoDallaVeggente)
                        {
                            foreach (Personaggio personaggio2 in listaPersonaggi)
                            {
                                if (personaggio2 is Veggente)
                                {
                                    Console.WriteLine($"La persona che hai spiato è stata puntata da {personaggio2.nomePersona} stanotte");
                                    Thread.Sleep(2000);
                                    puntatoDaQualcuno2 = true;
                                }
                            }
                        }
                        if (personaggio.puntatoDalCopione)
                        {
                            foreach (Personaggio personaggio2 in listaPersonaggi)
                            {
                                if (personaggio2 is Copione)
                                {
                                    Console.WriteLine($"La persona che hai spiato è stata puntata da {personaggio2.nomePersona} stanotte");
                                    Thread.Sleep(2000);
                                    puntatoDaQualcuno2 = true;
                                }
                            }
                        }
                        if (!puntatoDaQualcuno2)
                        {
                            Console.WriteLine("La persona che hai spiato non è stata puntata da nessuno stanotte...");
                        }
                    }
                }

                Thread.Sleep(2000);
            }
            if (this.puntatoDalCacciatore)
            {
                puntatoDaQualcuno = true;
                Console.WriteLine("Puoi utilizzare il potere del cacciatore stanotte...");
                Console.WriteLine("Su chi piazzerai una trappola stanotte?");
                string personaCacciatore = Console.ReadLine();
                personaCacciatore = this.ControlloInput(personaCacciatore, listaPersonaggi);
                foreach (Personaggio personaggio in listaPersonaggi)
                {
                    if (personaggio.nomePersona == personaCacciatore)
                    {
                        personaggio.puntatoDallImputatoComeCacciatore = true;
                    }
                }
            }
            if (this.puntatoDalGufo)
            {
                puntatoDaQualcuno = true;
                Console.WriteLine("Puoi utilizzare il potere del gufo stanotte...");
                Console.WriteLine("Chi manderai al ballottaggio stanotte?");
                string personaGufo = Console.ReadLine();
                personaGufo = this.ControlloInput(personaGufo, listaPersonaggi);
                foreach (Personaggio personaggio in listaPersonaggi)
                {
                    if (personaggio.nomePersona == personaGufo)
                    {
                        personaggio.puntatoDallImputatoComeGufo = true;
                    }
                }
            }
            if (!puntatoDaQualcuno)
            {
                Console.WriteLine("Non hai poteri da poter usare stanotte...");
            }
           
        }
    }
}
