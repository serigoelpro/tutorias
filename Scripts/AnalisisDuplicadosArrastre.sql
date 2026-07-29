/* =========================================================================
   ANÁLISIS DE DUPLICADOS Y HUÉRFANOS EN VISTA DE ARRASTRE POR CARRERA
   -------------------------------------------------------------------------
   Propósito:
     Verificar el impacto del filtro de especialidad agregado a la vista
     vw_ArrastreCarreraCoordinador:
        AND (e.Nombre = dp.Especialidad OR m.IdEspecialidad IS NULL OR m.IdEspecialidad = 0)

   Distingue:
     - DUPLICADOS reales: el alumno tiene la misma materia (Nombre+IdGrado)
       en dos IdMateria distintos (una por cada especialidad).
     - HUÉRFANOS: el alumno tiene la materia una sola vez, pero asignada
       a una especialidad que no es la suya.

   Base de datos: Tutorias
   Servidor local: MGAZ\SQLEXPRESS
   ========================================================================= */

USE Tutorias;
GO

/* -------------------------------------------------------------------------
   1. CONTEO GENERAL: cuántos registros oculta el filtro de especialidad
   ------------------------------------------------------------------------- */
WITH prod AS (
    SELECT  dp.IdPersona,
            ma.IdMateria,
            m.Nombre        AS MatNom,
            m.IdGrado,
            e.Nombre        AS MatEsp,
            dp.Especialidad AS AlumEsp,
            ma.FechaInicioArrastre,
            dp.[Año],
            dp.IdPeriodo,
            dp.IdCarrera
    FROM DatosPersonales      dp
    INNER JOIN MateriasAlumno ma ON dp.IdPersona   = ma.IdPersona
    INNER JOIN Materias       m  ON ma.IdMateria   = m.IdMateria
    LEFT  JOIN Especialidads  e  ON m.IdEspecialidad = e.Id
    WHERE ma.Estado IN ('Reprobada','Extraordinario')
      AND m.IdCarrera = dp.IdCarrera
      AND ma.FechaInicioArrastre IS NOT NULL
),
removed AS (
    SELECT  p.*,
            CASE WHEN EXISTS(
                SELECT 1 FROM prod p2
                WHERE p2.IdPersona = p.IdPersona
                  AND p2.MatNom    = p.MatNom
                  AND p2.IdGrado   = p.IdGrado
                  AND p2.IdMateria <> p.IdMateria
            ) THEN 1 ELSE 0 END AS HasDup
    FROM prod p
    WHERE NOT (p.MatEsp = p.AlumEsp OR p.MatEsp IS NULL)
)
SELECT
    COUNT(*)                   AS RegistrosOcultadosPorFix,
    SUM(HasDup)                AS Duplicados_Reales,
    SUM(1 - HasDup)            AS Huerfanos_EspecialidadIncorrecta,
    COUNT(DISTINCT IdPersona)  AS AlumnosAfectados
FROM removed;
GO


/* -------------------------------------------------------------------------
   2. ALUMNOS CON DUPLICADOS REALES (materia visible + copia oculta)
   ------------------------------------------------------------------------- */
SELECT DISTINCT
    v.Matricula,
    v.NombreAlumno,
    v.NombreEspecialidad,
    v.MateriaArrastre,
    v.CuatrimestreMateria
FROM vw_ArrastreCarreraCoordinador v
INNER JOIN Materias mv ON v.IdMateria = mv.IdMateria
WHERE v.FechaInicioArrastre IS NOT NULL
  AND EXISTS (
        SELECT 1
        FROM MateriasAlumno ma2
        INNER JOIN Materias m2 ON ma2.IdMateria = m2.IdMateria
        WHERE ma2.IdPersona = v.IdPersona
          AND m2.Nombre     = mv.Nombre
          AND m2.IdGrado    = mv.IdGrado
          AND ma2.IdMateria <> v.IdMateria
  )
ORDER BY v.NombreAlumno, v.MateriaArrastre;
GO


/* -------------------------------------------------------------------------
   3. DETALLE: registros HUÉRFANOS (materia mal asignada, sin par correcto)
   ------------------------------------------------------------------------- */
WITH prod AS (
    SELECT  dp.IdPersona, dp.Matricula, dp.Nombre AS NombreAlumno,
            dp.Especialidad AS AlumEsp,
            ma.IdMateria, m.Nombre AS MatNom, m.IdGrado,
            e.Nombre AS MatEspIncorrecta,
            ma.Estado, ma.Calificacion, ma.FechaInicioArrastre
    FROM DatosPersonales      dp
    INNER JOIN MateriasAlumno ma ON dp.IdPersona     = ma.IdPersona
    INNER JOIN Materias       m  ON ma.IdMateria     = m.IdMateria
    LEFT  JOIN Especialidads  e  ON m.IdEspecialidad = e.Id
    WHERE ma.Estado IN ('Reprobada','Extraordinario')
      AND m.IdCarrera = dp.IdCarrera
      AND ma.FechaInicioArrastre IS NOT NULL
)
SELECT  p.Matricula, p.NombreAlumno, p.AlumEsp,
        p.MatNom, p.IdGrado, p.MatEspIncorrecta,
        p.Estado, p.Calificacion, p.FechaInicioArrastre
FROM prod p
WHERE NOT (p.MatEspIncorrecta = p.AlumEsp OR p.MatEspIncorrecta IS NULL)
  AND NOT EXISTS (
        SELECT 1 FROM prod p2
        WHERE p2.IdPersona = p.IdPersona
          AND p2.MatNom    = p.MatNom
          AND p2.IdGrado   = p.IdGrado
          AND p2.IdMateria <> p.IdMateria
  )
ORDER BY p.NombreAlumno, p.MatNom;
GO


/* -------------------------------------------------------------------------
   4. CONTEO COMPARATIVO (local con fix vs simulación de producción)
   ------------------------------------------------------------------------- */
SELECT
    (SELECT COUNT(*) FROM vw_ArrastreCarreraCoordinador
       WHERE FechaInicioArrastre IS NOT NULL)                              AS LocalConFix,
    (SELECT COUNT(*)
       FROM DatosPersonales      dp
       INNER JOIN MateriasAlumno ma ON dp.IdPersona = ma.IdPersona
       INNER JOIN Materias       m  ON ma.IdMateria = m.IdMateria
       WHERE ma.Estado IN ('Reprobada','Extraordinario')
         AND m.IdCarrera = dp.IdCarrera
         AND ma.FechaInicioArrastre IS NOT NULL)                           AS SimulacionProduccion,
    (SELECT COUNT(*)
       FROM DatosPersonales      dp
       INNER JOIN MateriasAlumno ma ON dp.IdPersona = ma.IdPersona
       INNER JOIN Materias       m  ON ma.IdMateria = m.IdMateria
       WHERE ma.Estado IN ('Reprobada','Extraordinario')
         AND m.IdCarrera = dp.IdCarrera
         AND ma.FechaInicioArrastre IS NOT NULL)
    -
    (SELECT COUNT(*) FROM vw_ArrastreCarreraCoordinador
       WHERE FechaInicioArrastre IS NOT NULL)                              AS DiferenciaOcultadaPorFix;
GO
