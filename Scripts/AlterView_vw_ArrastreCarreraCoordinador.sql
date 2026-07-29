USE Tutorias;
GO

ALTER VIEW [dbo].[vw_ArrastreCarreraCoordinador] AS
SELECT
    dp.IdPersona,
    ma.IdMateria,
    dp.IdCarrera,
    m.IdEspecialidad,
    dp.IdGrado,
    dp.IdGrupo,
    dp.IdTurno,
    dp.IdPeriodo,
    dp.Matricula,
    dp.Nombre as NombreAlumno,
    c.Nombre as NombreCarrera,
    ISNULL(e.Nombre, 'Sin especialidad') as NombreEspecialidad,
    CONCAT(gr.Nombre, ' - ', g.Nombre) as GradoGrupo,
    m.Nombre as MateriaArrastre,
    m.IdGrado as CuatrimestreMateria,
    CASE m.IdGrado
        WHEN 1 THEN '1'
        WHEN 2 THEN '2'
        WHEN 3 THEN '3'
        WHEN 4 THEN '4'
        WHEN 5 THEN '5'
        WHEN 6 THEN '6'
        WHEN 7 THEN '7'
        WHEN 8 THEN '8'
        WHEN 9 THEN '9'
        ELSE CONCAT(m.IdGrado, '')
    END as CuatrimestreTexto,
    ISNULL(ma.IntentosExtraordinarios, 0) as IntentosExtraordinarios,
    ma.FechaInicioArrastre,
    ISNULL(ma.Observaciones, '') as Observaciones,
    ISNULL(t.Nombre, 'Sin turno') as NombreTurno,
    ISNULL(p.Nombre, 'Sin periodo') as NombrePeriodo,
    dp.Año,
    ISNULL(u.NombreCompleto, 'Sin tutor') as TutorAsignado,
    CASE
        WHEN m.IdGrado <= 2 THEN 1
        WHEN m.IdGrado <= 4 THEN 2
        ELSE 3
    END as NivelCriticidad,
    CASE
        WHEN m.IdGrado <= 2 THEN 'danger'
        WHEN m.IdGrado <= 4 THEN 'warning'
        ELSE 'info'
    END as ClasificacionVisual,
    CASE
        WHEN m.IdGrado <= 2 THEN 'CRITICO'
        WHEN m.IdGrado <= 4 THEN 'MEDIO'
        ELSE 'RECIENTE'
    END as DescripcionCriticidad,
    CASE
        WHEN ma.FechaInicioArrastre IS NOT NULL
        THEN DATEDIFF(DAY, ma.FechaInicioArrastre, GETDATE())
        ELSE 0
    END as DiasEnArrastre,
    CASE
        WHEN ma.FechaInicioArrastre IS NOT NULL
        THEN DATEADD(MONTH, 8, ma.FechaInicioArrastre)
        ELSE NULL
    END as FechaLimiteArrastre,
    CASE
        WHEN ma.FechaInicioArrastre IS NOT NULL
        THEN DATEDIFF(DAY, GETDATE(), DATEADD(MONTH, 8, ma.FechaInicioArrastre))
        ELSE 999
    END as DiasRestantes,
    CASE
        WHEN ma.FechaInicioArrastre IS NULL THEN 'Sin fecha'
        WHEN DATEDIFF(DAY, GETDATE(), DATEADD(MONTH, 8, ma.FechaInicioArrastre)) <= 0 THEN 'Fuera de Tiempo'
        WHEN DATEDIFF(DAY, GETDATE(), DATEADD(MONTH, 8, ma.FechaInicioArrastre)) <= 60 THEN 'Critico'
        WHEN DATEDIFF(DAY, GETDATE(), DATEADD(MONTH, 8, ma.FechaInicioArrastre)) <= 180 THEN 'Medio'
        ELSE 'En Tiempo'
    END as EstadoTiempo,
    ROW_NUMBER() OVER (
        ORDER BY
            CASE
                WHEN m.IdGrado <= 2 THEN 1
                WHEN m.IdGrado <= 4 THEN 2
                ELSE 3
            END,
            dp.Nombre
    ) as OrdenPrioridad,
    m.Activo as MateriaEstaActiva,
    CASE
        WHEN m.Activo = 1 THEN 'Activa'
        ELSE 'Desactivada'
    END as EstadoMateria,
    CASE WHEN EXISTS(
        SELECT 1 FROM BajasAlumnos ba
        WHERE ba.IdPersona = dp.IdPersona
        AND ba.Activo = 1
        AND ba.Reingreso = 0
    ) THEN 0 ELSE 1 END as EstadoAlumno,
    ma.Estado as EstadoMateriaAlumno
FROM DatosPersonales dp
INNER JOIN MateriasAlumno ma ON dp.IdPersona = ma.IdPersona
INNER JOIN Materias m ON ma.IdMateria = m.IdMateria
INNER JOIN Carreras c ON dp.IdCarrera = c.IdCarrera
LEFT JOIN Especialidads e ON m.IdEspecialidad = e.Id
LEFT JOIN Grupoes g ON dp.IdGrupo = g.IdGrupo
LEFT JOIN Gradoes gr ON dp.IdGrado = gr.IdGrado
LEFT JOIN Turnoes t ON dp.IdTurno = t.IdTurno
LEFT JOIN Periodoes p ON dp.IdPeriodo = p.IdPeriodo
LEFT JOIN TutoriaGrupals tg ON (
    dp.IdGrupo = tg.IdGrupo AND
    dp.IdCarrera = tg.IdCarrera AND
    dp.IdGrado = tg.IdGrado AND
    dp.IdTurno = tg.IdTurno AND
    dp.IdPeriodo = tg.IdPeriodo AND
    dp.Año = tg.Año
)
LEFT JOIN Usuarios u ON tg.IdUsuario = u.IdUsuario
WHERE ma.Estado IN ('Reprobada', 'Extraordinario')
  AND m.IdCarrera = dp.IdCarrera;
GO