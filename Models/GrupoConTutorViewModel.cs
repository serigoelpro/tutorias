using System;

namespace Plataforma_Web.Models
{
    public class GrupoConTutorViewModel
    {
        public int IdTutoriaGrupal { get; set; }
        public int IdGrado { get; set; }
        public int IdGrupo { get; set; }
        public int IdCarrera { get; set; }
        public int IdTurno { get; set; }
        public int IdPeriodo { get; set; }
        public int Año { get; set; }
        public int IdUsuario { get; set; }
        public string NombreTutor { get; set; }
        public string UserNameTutor { get; set; }
        public bool EstadoTutor { get; set; }
    }
}