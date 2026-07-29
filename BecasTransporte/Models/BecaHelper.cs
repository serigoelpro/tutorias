using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace PlataformaWeb.BecasTransporte.Models
{
    public class BecaHelper
    {
        public string Beca { get; set; }
        public int CantidadAlumnos { get; set; }

        public string MontoTotalPorBeca { get; set; }

        public string ListaAlumnos { get; set; }
    }
}