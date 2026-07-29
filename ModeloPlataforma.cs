namespace PlataformaWeb
{
    using Plataforma_Web.Models;
    using PlataformaWeb.Models;
    using PlataformaWeb.Models.Psicologia;
    using System;
    using System.Data.Entity;
    using System.Linq;

    public class ModeloPlataforma : DbContext
    {
        // El contexto se ha configurado para usar una cadena de conexión 'ModeloPlataforma' del archivo 
        // de configuración de la aplicación (App.config o Web.config). De forma predeterminada, 
        // esta cadena de conexión tiene como destino la base de datos 'PlataformaWeb.ModeloPlataforma' de la instancia LocalDb. 
        // 
        // Si desea tener como destino una base de datos y/o un proveedor de base de datos diferente, 
        // modifique la cadena de conexión 'ModeloPlataforma'  en el archivo de configuración de la aplicación.

        public ModeloPlataforma()
            : base("name=ModeloPlataforma")
        {
        }
        public System.Data.Entity.DbSet<Plataforma_Web.Models.Carrera> Carreras { get; set; }
        public System.Data.Entity.DbSet<Plataforma_Web.Models.Grupo> Grupoes { get; set; }
        public System.Data.Entity.DbSet<Plataforma_Web.Models.ClasesExtras.Grado> Gradoes { get; set; }
        public System.Data.Entity.DbSet<Plataforma_Web.Models.ClasesExtras.Turno> Turnoes { get; set; }
        public System.Data.Entity.DbSet<Plataforma_Web.Models.Usuario> Usuarios { get; set; }
        public System.Data.Entity.DbSet<Plataforma_Web.Models.Nivel> Nivels { get; set; }
        public System.Data.Entity.DbSet<Plataforma_Web.Models.TutoriaGrupal> TutoriaGrupals { get; set; }
        public System.Data.Entity.DbSet<Plataforma_Web.Models.PAT> PATs { get; set; }
        public System.Data.Entity.DbSet<Plataforma_Web.Models.PrimeraEntrevista.DatosPersonales> DatosPersonales { get; set; }
        public System.Data.Entity.DbSet<Plataforma_Web.Models.PrimeraEntrevista.AspectosEconomicos> AspectosEconomicos { get; set; }
        public System.Data.Entity.DbSet<Plataforma_Web.Models.PrimeraEntrevista.AspectosAcademicos> AspectosAcademicos { get; set; }
        public System.Data.Entity.DbSet<Plataforma_Web.Models.PrimeraEntrevista.AspectosPersonales> AspectosPersonales { get; set; }
        public System.Data.Entity.DbSet<Plataforma_Web.Models.PrimeraEntrevista.Secundarios.Respuesta1> Respuesta1 { get; set; }
        public System.Data.Entity.DbSet<Plataforma_Web.Models.PrimeraEntrevista.Secundarios.Respuesta2> Respuesta2 { get; set; }
        public System.Data.Entity.DbSet<Plataforma_Web.Models.PrimeraEntrevista.Secundarios.Respuesta3> Respuesta3 { get; set; }
        public System.Data.Entity.DbSet<Plataforma_Web.Models.PrimeraEntrevista.Secundarios.Respuesta4> Respuesta4 { get; set; }
        public System.Data.Entity.DbSet<Plataforma_Web.Models.PrimeraEntrevista.Secundarios.Respuesta5> Respuesta5 { get; set; }
        public System.Data.Entity.DbSet<Plataforma_Web.Models.PrimeraEntrevista.Secundarios.Respuesta6> Respuesta6 { get; set; }
        public System.Data.Entity.DbSet<Plataforma_Web.Models.PrimeraEntrevista.Secundarios.Respuesta7> Respuesta7 { get; set; }
        public System.Data.Entity.DbSet<Plataforma_Web.Models.PrimeraEntrevista.Secundarios.Respuesta8> Respuesta8 { get; set; }
        public System.Data.Entity.DbSet<Plataforma_Web.Models.PrimeraEntrevista.Secundarios.Respuesta9> Respuesta9 { get; set; }
        public System.Data.Entity.DbSet<Plataforma_Web.Models.PrimeraEntrevista.Secundarios.Respuesta0> Respuesta0 { get; set; }
        public System.Data.Entity.DbSet<Plataforma_Web.Models.PrimeraEntrevista.Secundarios.Respuesta10> Respuesta10 { get; set; }
        public System.Data.Entity.DbSet<Plataforma_Web.Models.PrimeraEntrevista.Secundarios.Respuesta11> Respuesta11 { get; set; }
        public System.Data.Entity.DbSet<Plataforma_Web.Models.PrimeraEntrevista.Secundarios.Respuesta12> Respuesta12 { get; set; }
        public System.Data.Entity.DbSet<Plataforma_Web.Models.PrimeraEntrevista.Secundarios.Respuesta13> Respuesta13 { get; set; }
        public System.Data.Entity.DbSet<Plataforma_Web.Models.PrimeraEntrevista.Secundarios.Respuesta14> Respuesta14 { get; set; }
        public System.Data.Entity.DbSet<Plataforma_Web.Models.PrimeraEntrevista.Secundarios.Respuesta15> Respuesta15 { get; set; }
        public System.Data.Entity.DbSet<Plataforma_Web.Models.PrimeraEntrevista.Secundarios.Respuesta16> Respuesta16 { get; set; }
        public System.Data.Entity.DbSet<Plataforma_Web.Models.PrimeraEntrevista.Secundarios.Respuesta17> Respuesta17 { get; set; }
        public System.Data.Entity.DbSet<Plataforma_Web.Models.PrimeraEntrevista.Secundarios.Respuesta18> Respuesta18 { get; set; }
        public System.Data.Entity.DbSet<Plataforma_Web.Models.PrimeraEntrevista.Secundarios.Vulnerable> Vulnerable { get; set; }

        public System.Data.Entity.DbSet<Plataforma_Web.Models.PrimeraEntrevista.Secundarios.TempGrupo> TempGrupos { get; set; }
        public System.Data.Entity.DbSet<Plataforma_Web.Models.ObservacioFamilia> ObservacioFamilias { get; set; }

        public System.Data.Entity.DbSet<Plataforma_Web.Models.Vulnerabilidad> Vulnerabilidads { get; set; }

        public System.Data.Entity.DbSet<Plataforma_Web.Models.EntrevistaInicial> EntrevistaInicials { get; set; }
        public System.Data.Entity.DbSet<Plataforma_Web.Models.ClasesPAT.Periodo> Periodos { get; set; }
        public System.Data.Entity.DbSet<Plataforma_Web.Models.ClasesPAT.Semana> Semanas { get; set; }
        public System.Data.Entity.DbSet<Plataforma_Web.Models.ClasesPAT.SemanasPeriodo> SemanasPeriodos { get; set; }
        public System.Data.Entity.DbSet<Plataforma_Web.Models.ClasesPAT.TipoTutoria> TipoTutorias { get; set; }
        public System.Data.Entity.DbSet<Plataforma_Web.Models.ClasesPAT.ActividadesSemanal> actividadesSemanals { get; set; }
        public System.Data.Entity.DbSet<PlataformaWeb.Models.ClasesPAT.AuxiliarPAT> AuxiliarPATs { get; set; }

        public System.Data.Entity.DbSet<PlataformaWeb.Models.Baja> Bajas { get; set; }

        public System.Data.Entity.DbSet<PlataformaWeb.Models.Individual> Individuals { get; set; }

        public System.Data.Entity.DbSet<PlataformaWeb.Models.Seguimiento> Seguimientoes { get; set; }

        public System.Data.Entity.DbSet<PlataformaWeb.Models.Materia> Materias { get; set; }


        public System.Data.Entity.DbSet<PlataformaWeb.Models.PlanEstudio> PlanesEstudio { get; set; }

        public System.Data.Entity.DbSet<PlataformaWeb.Models.Materias.MateriaAlumno> AlumnosMaterias { get; set; }
        public System.Data.Entity.DbSet<Plataforma_Web.Models.NivelDesempenoPerfil> NivelDesempenoPerfils { get; set; }
        public DbSet<Models.Canalizaciones> Canalizaciones { get; set; }
        public DbSet<TipoCanalizaciones> TipoCanalizaciones { get; set; }

        public DbSet<Notificaciones> Notificaciones { get; set; }
        public DbSet<TutoriasGrupalsByArea> TutoriasGrupalsByArea { get; set; }

        //Agregado por Joel Ruiz para administrar los modelos de becas y transportes de los alumnos.
        #region Becas y transporte
        public System.Data.Entity.DbSet<PlataformaWeb.BecasTransporte.Models.Beca> Becas { get; set; }
        public System.Data.Entity.DbSet<PlataformaWeb.BecasTransporte.Models.Estudiante> Estudiantes { get; set; }
        public System.Data.Entity.DbSet<PlataformaWeb.BecasTransporte.Models.Historial> Historials { get; set; }
        public System.Data.Entity.DbSet<PlataformaWeb.BecasTransporte.Models.Transporte> Transportes { get; set; }
        public System.Data.Entity.DbSet<PlataformaWeb.BecasTransporte.Models.Colonia> Colonias { get; set; }
        public System.Data.Entity.DbSet<PlataformaWeb.BecasTransporte.Models.Ciudad> Ciudads { get; set; }
        public System.Data.Entity.DbSet<PlataformaWeb.BecasTransporte.Models.Especialidad> Especialidads { get; set; }
        public virtual DbSet<Psicologos> Psicologos { get; set; }
        public virtual DbSet<PsicologoTurnos> PsicologoTurnos { get; set; }
        public virtual DbSet<PsiAreaAtencion> PsiAreasAtencion { get; set; }

        public virtual DbSet<PsiDetalleAtencion> PsiDetallesAtencion { get; set; }

        public virtual DbSet<Psicologo_PsiDetalle> Psicologo_PsiDetalles { get; set; }

        public virtual DbSet<PlataformaWeb.Models.Historico.EstadisticasHistoricoCorte> EstadisticasHistoricoCortes { get; set; }
        public virtual DbSet<PlataformaWeb.Models.Historico.EstadisticasHistoricoSeccion> EstadisticasHistoricoSecciones { get; set; }


        #endregion

        // Agregue un DbSet para cada tipo de entidad que desee incluir en el modelo. Para obtener más información 
        // sobre cómo configurar y usar un modelo Code First, vea http://go.microsoft.com/fwlink/?LinkId=390109.

        // public virtual DbSet<MyEntity> MyEntities { get; set; }
    }

    //public class MyEntity
    //{
    //    public int Id { get; set; }
    //    public string Name { get; set; }
    //}
}