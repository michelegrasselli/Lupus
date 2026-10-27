using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace Lupus
{
    internal class Copione: Personaggio
    {
        public Copione()
        {
            priorita = 11;
            buono = true;
        }
        public override void Agisci(List<Personaggio> listaPersonaggi)
        {
            Console.WriteLine("Chi copierai stanotte?");
            string personaCopione = Console.ReadLine();
            personaCopione = this.ControlloInput(personaCopione, listaPersonaggi);
            string ruoloCopione = "";
            bool trasformato = false;
            foreach (Personaggio personaggio in listaPersonaggi)
            {
                if (personaggio.nomePersona == personaCopione)
                {
                    personaggio.puntatoDalCopione = true;
                    if (personaggio is not Copione)
                    {
                        Console.WriteLine($"Sei diventato un personaggio {personaggio.GetType().Name}");
                        ruoloCopione = personaggio.GetType().Name;
                        trasformato = true;
                    }
                }
            }
            int indiceCopione = TrovaIndice(listaPersonaggi);
            switch (ruoloCopione)
            {
                case "Lupo":
                    listaPersonaggi.Insert(indiceCopione, new Lupo());
                    break;
                case "Puttana":
                    listaPersonaggi.Insert(indiceCopione, new Puttana());
                    break;
                case "Veggente":
                    listaPersonaggi.Insert(indiceCopione, new Veggente());
                    break;
                case "Guardiano":
                    listaPersonaggi.Insert(indiceCopione, new Guardiano());
                    break;
                case "Becchino":
                    listaPersonaggi.Insert(indiceCopione, new Becchino());
                    break;
                case "Annullatore":
                    listaPersonaggi.Insert(indiceCopione, new Annullatore());
                    break;
                case "BabyLupo":
                    listaPersonaggi.Insert(indiceCopione, new BabyLupo());
                    break;
                case "Copione":
                    listaPersonaggi.Insert(indiceCopione, new Copione());
                    break;
                case "Spia":
                    listaPersonaggi.Insert(indiceCopione, new Spia());
                    break;
                case "Imputato":
                    listaPersonaggi.Insert(indiceCopione, new Imputato());
                    break;
                case "Diavolo":
                    listaPersonaggi.Insert(indiceCopione, new Diavolo());
                    break;
                case "Contadino":
                    listaPersonaggi.Insert(indiceCopione, new Contadino());
                    break;
                case "Vvelenatu":
                    listaPersonaggi.Insert(indiceCopione, new Vvelenatu());
                    break;
                case "Cacciatore":
                    listaPersonaggi.Insert(indiceCopione, new Cacciatore());
                    break;
                case "Kamikaze":
                    listaPersonaggi.Insert(indiceCopione, new Kamikaze());
                    break;
                case "Gufo":
                    listaPersonaggi.Insert(indiceCopione, new Gufo());
                    break;
                case "Sfigato":
                    listaPersonaggi.Insert(indiceCopione, new Sfigato());
                    break;
                default:
                    Console.WriteLine("Hai puntato un copione, non ti trasformerai stanotte...");
                    break;
            }
            if (trasformato)
            {
                listaPersonaggi[indiceCopione].nomePersona = this.nomePersona;
                listaPersonaggi[indiceCopione].puntatoDalLupo = this.puntatoDalLupo;
                listaPersonaggi[indiceCopione].mortoQuestoTurno = this.mortoQuestoTurno;
                listaPersonaggi[indiceCopione].puntatoDalGuardiano = this.puntatoDalGuardiano;
                listaPersonaggi[indiceCopione].puntatoDallAnnullatore = this.puntatoDallAnnullatore;
                listaPersonaggi[indiceCopione].puntatoDalBecchino = this.puntatoDalBecchino;
                listaPersonaggi[indiceCopione].puntatoDallaVeggente = this.puntatoDallaVeggente;
                listaPersonaggi[indiceCopione].puntatoDallaPuttana = this.puntatoDallaPuttana;
                listaPersonaggi[indiceCopione].puttanaAndataConUnLupo = this.puttanaAndataConUnLupo;
                listaPersonaggi[indiceCopione].puntatoDalCopione = this.puntatoDalCopione;
                listaPersonaggi[indiceCopione].puntatoDallaSpia = this.puntatoDallaSpia;
                listaPersonaggi[indiceCopione].puntatoDallImputatoComeLupo = this.puntatoDallImputatoComeLupo;
                listaPersonaggi[indiceCopione].puntatoDallImputatoComeGuardiano = this.puntatoDallImputatoComeGuardiano;
                listaPersonaggi[indiceCopione].puntatoDallImputatoComePuttana = this.puntatoDallImputatoComePuttana;
                listaPersonaggi[indiceCopione].puntatoDallImputatoComeVeggente = this.puntatoDallImputatoComeVeggente;
                listaPersonaggi[indiceCopione].puntatoDallImputatoComeSpia = this.puntatoDallImputatoComeSpia;
                listaPersonaggi[indiceCopione].puntatoDallImputatoComeCacciatore = this.puntatoDallImputatoComeCacciatore;
                listaPersonaggi[indiceCopione].puntatoDallImputatoComeGufo = this.puntatoDallImputatoComeGufo;
                listaPersonaggi[indiceCopione].diavoloSbranato = this.diavoloSbranato;
                listaPersonaggi[indiceCopione].puntatoDalCacciatore = this.puntatoDalCacciatore;
                listaPersonaggi[indiceCopione].puntatoDalGufo = this.puntatoDalGufo;



                listaPersonaggi.RemoveAt(indiceCopione + 1);
            }
        }
    }
}
