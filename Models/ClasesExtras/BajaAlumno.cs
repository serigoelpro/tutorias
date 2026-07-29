using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace Plataforma_Web.Models
{
    public class BajaAlumno
    {
        //Datos del alumno 
        [Key]
        public int IdBajaAlumno { get; set; }

        //numero de folio checar si puede ser automatico con el id o simplemente que sea manual y que reinicie cada cuatri
        [Required]
        [DisplayName("Numero de folio")]
        public string NumeroFolio { get; set; }

        [Required(ErrorMessage = "*")]
        [DisplayName("Fecha de Registro")]
        [DataType(DataType.Date)]
        [DisplayFormat(ApplyFormatInEditMode = true, DataFormatString = "{0:yyyy-MM-dd}")]
        public DateTime Fecha { get; set; }

        [Required]
        [DisplayName("Matricula")]
        public int IdMatricula { get; set; }
        [NotMapped]
        public string Matricula { get; set; }

        [NotMapped]
        public string Grupo { get; set; }

        [NotMapped]
        public string Carrera { get; set; }

        [Required]
        [DisplayName("Area")]
        public string Area { get; set; }

        [Required]
        [DisplayName("Cuatrimestre")]
        public int Cuatrimestre { get; set; }

        [Required]
        [DisplayName("Turno")]
        public string Turno { get; set; }

        [Required]
        [DisplayName("¿El Alumno pertenece a algun grupo altamente vunerable?")]
        public bool GrupoVulnerable { get; set; }
        
        [DisplayName("Tipo de vulnerabilidad")]
        public int IdVunerabilidad { get; set; }
        [NotMapped]
        public string Vulnerabilidad { get; set; }

        [Required]
        [DisplayName("Ubique la causa de la baja del alumno")]
        public string CausaBaja{ get; set; }

        [DisplayName("En caso de otras causas, especifique cuales")]
        public string OtraCausaBaja { get; set; }

        [Required]
        [DisplayName("Tipo de baja")]
        public string TipoBaja { get; set; }

        [Required]
        [DisplayName("Observaciones")]
        public string Observacion { get; set; }

    }
}