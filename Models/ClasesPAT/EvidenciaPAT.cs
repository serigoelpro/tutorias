using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;
using Plataforma_Web.Models.MongoDB;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Plataforma_Web.Models.MongoDB
{
    public class EvidenciaPAT
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        [BsonElement("patId")]
        public int PatId { get; set; }

        [BsonElement("actividadId")]
        public int ActividadId { get; set; }

        [BsonElement("tutorId")]
        public int TutorId { get; set; }

        [BsonElement("nombreArchivo")]
        public string NombreArchivo { get; set; }

        [BsonElement("rutaArchivo")]
        public string RutaArchivo { get; set; }

        [BsonElement("fechaSubida")]
        public DateTime FechaSubida { get; set; }

        [BsonElement("tamanoArchivo")]
        public long TamanoArchivo { get; set; }

        [BsonElement("estado")]
        public string Estado { get; set; } = "active";

        [BsonElement("tipoTutoria")]
        public string TipoTutoria { get; set; } // individual o grupal

        [BsonElement("metadata")]
        public EvidenciaMetadata Metadata { get; set; }

        [BsonElement("fechaCreacion")]
        public DateTime FechaCreacion { get; set; }

        // --- INICIO: SOLICITUD 2 (AÑADIR CAMPO DE ESTADO) ---
        [BsonElement("estadoAprobacion")]
        [DefaultValue(0)]
        public int EstadoAprobacion { get; set; } = 0; // 0: Pendiente, 1: Aprobado, 2: Rechazado
        // --- FIN: SOLICITUD 2 ---
    }

    public class EvidenciaMetadata
    {
        [BsonElement("periodo")]
        public string Periodo { get; set; }

        [BsonElement("ano")]
        public string Ano { get; set; }

        [BsonElement("grupo")]
        public string Grupo { get; set; }

        [BsonElement("semana")]
        public string Semana { get; set; }

        [BsonElement("tipoTutoria")]
        public string TipoTutoria { get; set; } // individual o grupal
    }
}