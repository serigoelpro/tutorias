using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace PlataformaWeb.BecasTransporte.Models
{
    public class Historial
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int IdCarrera { get; set; }

        [DataType(DataType.Date)]
        [DisplayFormat(ApplyFormatInEditMode = true, DataFormatString = "{0:dd/MM/yyyy}")]
        public DateTime Fecha { get; set; }

        [Required]
        public int CantidadBecados { get; set; }

        [Required]
        public int CantidadUsanTransporte { get; set; }

        [Required]
        public int CantidadTotalEstudiantes { get; set; }

        [Required]
        [DisplayName("Monto total")]
        public int MontoTotal { get; set; }


        [Required]
        public int CantidadTotalEstudiantesMatutino { get; set; }

        [Required]
        public int CantidadBecadosMatutino { get; set; }

        [Required]
        public int CantidadUsanTransporteMatutino { get; set; }


        [Required]
        [DisplayName("Monto total matutino")]
        public int MontoTotalMatutino { get; set; }

        [Required]
        public int CantidadTotalEstudiantesVespertino { get; set; }
        [Required]
        public int CantidadBecadosVespertino { get; set; }

        [Required]
        public int CantidadUsanTransporteVespertino { get; set; }

        [Required]
        [DisplayName("Monto total vespertino")]
        public int MontoTotalVespertino { get; set; }

        [Required]
        public int CantidadTotalEstudiantesDespresurizado { get; set; }

        [Required]
        public int CantidadBecadosDespresurizado { get; set; }

        [Required]
        public int CantidadUsanTransporteDespresurizado { get; set; }

        [Required]
        [DisplayName("Monto total despresurizado")]
        public int MontoTotalDespresurizado { get; set; }

        [Required]
        public int PeriodoActual { get; set; }

    }
}