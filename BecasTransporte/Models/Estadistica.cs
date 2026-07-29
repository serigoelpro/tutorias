using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace PlataformaWeb.BecasTransporte.Models
{
    public class Estadistica
    {
        public List<String> Rutas { get; set; }
        public int CantidadAlumnosUsanTransporte { get; set; }
        public int CantidadAlumnosReynosa { get; set; }
        public int CantidadAlumnosRB { get; set; }
        public List<String> ListaAlumnosReynosa { get; set; }
        public List<String> ListaAlumnosRB { get; set; }

    }
}