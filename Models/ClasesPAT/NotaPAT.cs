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
    public class NotaPAT
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        [BsonElement("patId")]
        public int PatId { get; set; }

        [BsonElement("usuarioId")]
        public string UsuarioId { get; set; }

        [BsonElement("usuario")]
        public string Usuario { get; set; }

        [BsonElement("comentario")]
        public string Comentario { get; set; }

        [BsonElement("fechaCreacion")]
        public DateTime FechaCreacion { get; set; }

        [BsonElement("estado")]
        public string Estado { get; set; } = "activo";

        [BsonElement("metadata")]
        public ComentarioMetadata Metadata { get; set; }
    }

    public class ComentarioMetadata
    {
        [BsonElement("periodo")]
        public string Periodo { get; set; }

        [BsonElement("ano")]
        public string Ano { get; set; }

        [BsonElement("grupo")]
        public string Grupo { get; set; }

        [BsonElement("rol")]
        public string Rol { get; set; }

        [BsonElement("ipAddress")]
        public string IpAddress { get; set; }
    }
}
