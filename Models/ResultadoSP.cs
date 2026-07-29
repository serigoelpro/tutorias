using System;

namespace PlataformaWeb.Models
{
    public class ResultadoSP
    {
        public int IdBaja { get; set; }       // Ya es int gracias al CAST
        public int IdPersona { get; set; }    // Cambiado a int también

        public string Mensaje { get; set; }
        public string Folio { get; set; }
        public string Matricula { get; set; }
        public string NombreAlumno { get; set; }
        public string Vulnerable { get; set; }       // <- ya igual que en el SP
        public string Vulnerabilidad { get; set; }
        public string CampoOtra { get; set; }
    }

}
