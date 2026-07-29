using MongoDB.Driver;
using Plataforma_Web.Models.MongoDB;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Threading.Tasks;

namespace PlataformaWeb.Services
{
    public class MongoDBService
    {
        private readonly IMongoDatabase _database;
        private readonly IMongoCollection<EvidenciaPAT> _evidenciasCollection;
        private readonly IMongoCollection<NotaPAT> _notasCollection;

        public MongoDBService()
        {
            var connectionString = ConfigurationManager.ConnectionStrings["MongoDB"].ConnectionString;
            var client = new MongoClient(connectionString);
            _database = client.GetDatabase("TutoriasDB");
            _evidenciasCollection = _database.GetCollection<EvidenciaPAT>("evidencias_pat");
            _notasCollection = _database.GetCollection<NotaPAT>("notas_pat");
        }

        // Crear evidencia
        public async Task<string> CrearEvidenciaAsync(EvidenciaPAT evidencia)
        {
            evidencia.FechaCreacion = DateTime.UtcNow;
            evidencia.FechaSubida = DateTime.UtcNow;
            await _evidenciasCollection.InsertOneAsync(evidencia);
            return evidencia.Id;
        }

        // Obtener todas las evidencias activas por PAT y semana
        public async Task<List<EvidenciaPAT>> ObtenerEvidenciasPorPATySemanaAsync(int patId, string semana)
        {
            var builder = Builders<EvidenciaPAT>.Filter;
            var filter = builder.And(
                builder.Eq(e => e.PatId, patId),
                builder.Eq(e => e.Estado, "active"),
                builder.Eq("metadata.semana", semana)
            );
            return await _evidenciasCollection.Find(filter).ToListAsync();
        }

        // Obtener evidencias por actividad
        public async Task<List<EvidenciaPAT>> ObtenerEvidenciasPorActividadAsync(int actividadId)
        {
            var filter = Builders<EvidenciaPAT>.Filter.And(
                Builders<EvidenciaPAT>.Filter.Eq(e => e.ActividadId, actividadId),
                Builders<EvidenciaPAT>.Filter.Eq(e => e.Estado, "active")
            );
            return await _evidenciasCollection.Find(filter).ToListAsync();
        }

        // Obtener evidencias por PAT (todas las evidencias activas de un PAT)
        public async Task<List<EvidenciaPAT>> ObtenerEvidenciasPorPATAsync(int patId)
        {
            var filter = Builders<EvidenciaPAT>.Filter.And(
                Builders<EvidenciaPAT>.Filter.Eq(e => e.PatId, patId),
                Builders<EvidenciaPAT>.Filter.Eq(e => e.Estado, "active")
            );
            return await _evidenciasCollection.Find(filter).ToListAsync();
        }

        // Obtener evidencia por PAT y semana (usando metadata.semana)
        public async Task<EvidenciaPAT> ObtenerEvidenciaPorPATySemanaAsync(int patId, string semana)
        {
            var builder = Builders<EvidenciaPAT>.Filter;
            var filter = builder.And(
                builder.Eq(e => e.PatId, patId),
                builder.Eq(e => e.Estado, "active"),
                builder.Eq("metadata.semana", semana)
            );
            return await _evidenciasCollection.Find(filter).FirstOrDefaultAsync();
        }

        // Eliminar evidencia (soft delete)
        public async Task<bool> EliminarEvidenciaAsync(string evidenciaId)
        {
            var filter = Builders<EvidenciaPAT>.Filter.Eq(e => e.Id, evidenciaId);
            var update = Builders<EvidenciaPAT>.Update.Set(e => e.Estado, "deleted");
            var result = await _evidenciasCollection.UpdateOneAsync(filter, update);
            return result.ModifiedCount > 0;
        }

        // Obtener evidencia por ID
        public async Task<EvidenciaPAT> ObtenerEvidenciaPorIdAsync(string evidenciaId)
        {
            var filter = Builders<EvidenciaPAT>.Filter.Eq(e => e.Id, evidenciaId);
            return await _evidenciasCollection.Find(filter).FirstOrDefaultAsync();
        }

        // ========== MÉTODOS PARA COMENTARIOS PAT ==========

        // Crear nota
        public async Task<string> CrearNotaAsync(NotaPAT nota)
        {
            nota.FechaCreacion = DateTime.UtcNow;
            nota.Estado = "activo";
            await _notasCollection.InsertOneAsync(nota);
            return nota.Id;
        }

        // Obtener notas por PAT ID
        public async Task<List<NotaPAT>> ObtenerNotasPorPATAsync(int patId)
        {
            var filter = Builders<NotaPAT>.Filter.And(
                Builders<NotaPAT>.Filter.Eq(c => c.PatId, patId),
                Builders<NotaPAT>.Filter.Eq(c => c.Estado, "activo")
            );

            var sort = Builders<NotaPAT>.Sort.Descending(c => c.FechaCreacion);
            return await _notasCollection.Find(filter).Sort(sort).ToListAsync();
        }

        // Obtener nota por ID
        public async Task<NotaPAT> ObtenerNotaPorIdAsync(string notaId)
        {
            var filter = Builders<NotaPAT>.Filter.Eq(c => c.Id, notaId);
            return await _notasCollection.Find(filter).FirstOrDefaultAsync();
        }

        // Eliminar nota (soft delete)
        public async Task<bool> EliminarNotaAsync(string notaId)
        {
            var filter = Builders<NotaPAT>.Filter.Eq(c => c.Id, notaId);
            var update = Builders<NotaPAT>.Update.Set(c => c.Estado, "eliminado");
            var result = await _notasCollection.UpdateOneAsync(filter, update);
            return result.ModifiedCount > 0;
        }

        // Actualizar nota
        public async Task<bool> ActualizarNotaAsync(string notaId, string nuevaNota)
        {
            var filter = Builders<NotaPAT>.Filter.Eq(c => c.Id, notaId);
            var update = Builders<NotaPAT>.Update.Set(c => c.Comentario, nuevaNota);
            var result = await _notasCollection.UpdateOneAsync(filter, update);
            return result.ModifiedCount > 0;
        }

        // Obtener todas las notas activas (para reportes)
        public List<NotaPAT> ObtenerTodasNotasActivas()
        {
            var filter = Builders<NotaPAT>.Filter.Eq(n => n.Estado, "activo");
            return _notasCollection.Find(filter).ToList();
        }

        public async Task<bool> ActualizarEstadoAprobacionAsync(string evidenciaId, int nuevoEstado)
        {
            try
            {
                // 1. Crea el filtro para encontrar la evidencia por su ID
                var filter = Builders<EvidenciaPAT>.Filter.Eq(e => e.Id, evidenciaId);

                // 2. Crea la definición de actualización para cambiar solo el campo 'estadoAprobacion'
                var update = Builders<EvidenciaPAT>.Update.Set(e => e.EstadoAprobacion, nuevoEstado);

                // 3. Ejecuta la actualización en la base de datos
                // _evidenciasCollection es tu colección de MongoDB (deberías tenerla definida en esta clase)
                var result = await _evidenciasCollection.UpdateOneAsync(filter, update);

                // 4. Retorna 'true' si se modificó al menos 1 documento
                return result.IsAcknowledged && result.ModifiedCount > 0;
            }
            catch (Exception ex)
            {
                // Opcional: Registrar el error
                System.Diagnostics.Debug.WriteLine($"Error al actualizar estado de evidencia: {ex.Message}");
                return false;
            }
        }
    }
}

