using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Plataforma_Web.Models.PrimeraEntrevista;

namespace PlataformaWeb.Models
{
    public class AlumnoGrupoBajaVM
    {
        public DatosPersonales Alumno { get; set; }
        public bool TieneBaja { get; set; }
    }
}
