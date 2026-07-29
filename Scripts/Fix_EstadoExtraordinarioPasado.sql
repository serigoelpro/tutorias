/* =========================================================================
   Fix_EstadoExtraordinarioPasado.sql
   -------------------------------------------------------------------------
   Problema:
     Algunos alumnos tienen Estado = 'Extraordinario' en MateriasAlumno
     pero la Calificacion guardada ya es >= a la CalificacionMinima de su
     plan de estudio (ej: Plan 2020 requiere 8.0).
     Esto ocurre cuando la calificación se actualizó directamente en BD
     (o vía ruta que no invoca ValidarCalificacionSegunPlan) sin actualizar
     el campo Estado.

     Consecuencia visible: la vista MateriasAlumno/Index muestra banner
     REPROBADO y castiga el promedio aunque el alumno ya acreditó el examen.

   Solución:
     Actualizar Estado → 'Acreditada' en los registros donde:
       - Estado = 'Extraordinario'
       - Calificacion >= CalificacionMinima del PlanEstudio de la materia
         (ISNULL → usa 7.0 como mínimo por defecto)

   Caso verificado:
     ✅ Plascencia Rodriguez Joceline Gisell (IdPersona = 22029)
        Inglés VII, Plan 2020, CalificacionMinima = 8.0
        Calificacion = 8 → debe ser Acreditada

   Uso:
     1. Ejecutar primero el SELECT de diagnóstico para revisar los casos.
     2. Una vez validados, ejecutar el UPDATE.
     3. Repetir en producción.
   ========================================================================= */

USE Tutorias;
GO

-- =========================================================================
-- PASO 1: DIAGNÓSTICO — ver qué registros serán afectados
-- =========================================================================
SELECT
    ma.IdPersona,
    dp.Nombre              AS NombreAlumno,
    dp.Matricula,
    m.Nombre               AS Materia,
    m.IdGrado              AS Cuatrimestre,
    pe.Nombre              AS PlanEstudio,
    ISNULL(pe.CalificacionMinima, 7.0) AS CalificacionMinima,
    ma.Calificacion,
    ma.Estado              AS EstadoActual,
    'Acreditada'           AS EstadoNuevo
FROM MateriasAlumno ma
INNER JOIN Materias m        ON ma.IdMateria  = m.IdMateria
INNER JOIN DatosPersonales dp ON ma.IdPersona = dp.IdPersona
LEFT  JOIN PlanesEstudio pe  ON m.IdPlanEstudio = pe.IdPlanEstudio
WHERE ma.Estado = 'Extraordinario'
  AND ma.Calificacion IS NOT NULL
  AND ma.Calificacion >= ISNULL(pe.CalificacionMinima, 7.0)
ORDER BY dp.Nombre, m.Nombre;
GO

-- =========================================================================
-- PASO 2: CORRECCIÓN — actualizar Estado a 'Acreditada'
-- =========================================================================
UPDATE ma
SET
    ma.Estado              = 'Acreditada',
    ma.FechaActualizacion  = GETDATE(),
    ma.IntentosExtraordinarios = 0       -- acreditado → no quedan intentos pendientes
FROM MateriasAlumno ma
INNER JOIN Materias m ON ma.IdMateria = m.IdMateria
LEFT  JOIN PlanesEstudio pe ON m.IdPlanEstudio = pe.IdPlanEstudio
WHERE ma.Estado = 'Extraordinario'
  AND ma.Calificacion IS NOT NULL
  AND ma.Calificacion >= ISNULL(pe.CalificacionMinima, 7.0);

PRINT CONCAT('Registros corregidos: ', @@ROWCOUNT);
GO

-- =========================================================================
-- PASO 3: VERIFICACIÓN PUNTUAL (Gisell, id = 22029)
-- =========================================================================
SELECT
    ma.IdPersona,
    dp.Nombre     AS NombreAlumno,
    m.Nombre      AS Materia,
    ma.Calificacion,
    ma.Estado,
    ma.FechaActualizacion
FROM MateriasAlumno ma
INNER JOIN Materias m         ON ma.IdMateria  = m.IdMateria
INNER JOIN DatosPersonales dp ON ma.IdPersona  = dp.IdPersona
WHERE ma.IdPersona = 22029;
GO
