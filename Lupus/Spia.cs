using System;
using System.Collections.Generic;
using System.Text;

namespace Lupus
{
    internal class Spia: Personaggio
    {
        public Spia()
        {
            priorita = 9;
            buono = true;
        }

            public override void Agisci(List<Personaggio> listaPersonaggi)
        {
            Console.WriteLine("Chi spierai stanotte?");
            string personaSpiata = Console.ReadLine();
            personaSpiata = this.ControlloInput(personaSpiata, listaPersonaggi);
            foreach (Personaggio personaggio in listaPersonaggi)
            {
                if (personaggio.nomePersona == personaSpiata)
                {
                    personaggio.puntatoDallaSpia = true;
                }
            }
            foreach (Personaggio personaggio in listaPersonaggi)
            {
                bool puntatoDaQualcuno = false;
                if (personaggio.puntatoDallaSpia)
                {
                    if (personaggio.puntatoDallAnnullatore)
                    {
                        foreach (Personaggio personaggio2 in listaPersonaggi)
                        {
                            if (personaggio2 is Annullatore)
                            {
                                Console.WriteLine($"La persona che hai spiato è stata puntata da {personaggio2.nomePersona} stanotte");
                                Thread.Sleep(2000);
                                puntatoDaQualcuno = true;
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
                                puntatoDaQualcuno = true;
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
                                puntatoDaQualcuno = true;
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
                                puntatoDaQualcuno = true;
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
                                puntatoDaQualcuno = true;
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
                                puntatoDaQualcuno = true;
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
                                puntatoDaQualcuno = true;
                            }
                        }
                    }
                    if (personaggio.puntatoDalCopione2)
                    {
                        foreach (Personaggio personaggio2 in listaPersonaggi)
                        {
                            if (personaggio2 is Copione2)
                            {
                                Console.WriteLine($"La persona che hai spiato è stata puntata da {personaggio2.nomePersona} stanotte");
                                Thread.Sleep(2000);
                                puntatoDaQualcuno = true;
                            }
                        }
                    }
                    if (personaggio.puntatoDalCacciatore)
                    {
                        foreach (Personaggio personaggio2 in listaPersonaggi)
                        {
                            if (personaggio2 is Cacciatore)
                            {
                                Console.WriteLine($"La persona che hai spiato è stata puntata da {personaggio2.nomePersona} stanotte");
                                Thread.Sleep(2000);
                                puntatoDaQualcuno = true;
                            }
                        }
                    }
                    if (personaggio.puntatoDalGufo)
                    {
                        foreach (Personaggio personaggio2 in listaPersonaggi)
                        {
                            if (personaggio2 is Gufo)
                            {
                                Console.WriteLine($"La persona che hai spiato è stata puntata da {personaggio2.nomePersona} stanotte");
                                Thread.Sleep(2000);
                                puntatoDaQualcuno = true;
                            }
                        }
                    }
                    if (personaggio.puntatoDallImputatoComeLupo || personaggio.puntatoDallImputatoComeGuardiano || personaggio.puntatoDallImputatoComePuttana || personaggio.puntatoDallImputatoComeVeggente || personaggio.puntatoDallImputatoComeSpia || personaggio.puntatoDallImputatoComeCacciatore || personaggio.puntatoDallImputatoComeGufo)
                    {
                        foreach (Personaggio personaggio2 in listaPersonaggi)
                        {
                            if (personaggio2 is Imputato)
                            {
                                Console.WriteLine($"La persona che hai spiato è stata puntata da {personaggio2.nomePersona} stanotte");
                                Thread.Sleep(2000);
                                puntatoDaQualcuno = true;
                            }
                        }
                    }
                    if (!puntatoDaQualcuno)
                    {
                        Console.WriteLine("La persona che hai spiato non è stata puntata da nessuno stanotte...");
                    }
                }
            }
        }
    }
}
