using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace PlataformaWeb.Models
{
    public class SeguimientoPlanoVM
    {
        public int IdSeguimiento { get; set; }
        public int IdIndividual { get; set; }
        public System.DateTime Fecha { get; set; }
        public string Vulnerabilidad { get; set; }
        public string Problematica { get; set; }
        public string Accion { get; set; }
        public string Grupo { get; set; }
        public string Cuatrimestre { get; set; }
        public int Anio { get; set; }
    }
}