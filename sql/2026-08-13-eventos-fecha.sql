-- =============================================================================
-- OptiAquaV2 — Columna Fecha en la tabla Eventos
--
-- ENTREGADO, NO EJECUTADO. Revísalo y ejecútalo tú.
--
-- La tabla Eventos solo tenía IdEvento y Evento; la fecha iba dentro del texto, así
-- que no se podía filtrar por fechas. Esta migración añade una columna Fecha con
-- DEFAULT GETDATE(): los eventos NUEVOS la rellenan solos (el código no cambia, se
-- apoya en el DEFAULT). Los eventos antiguos se rellenan con un intento de leer la
-- fecha del texto; lo que no se pueda interpretar queda a NULL.
-- =============================================================================

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.Eventos') AND name='Fecha')
BEGIN
    ALTER TABLE dbo.Eventos ADD Fecha datetime NULL
        CONSTRAINT DF_Eventos_Fecha DEFAULT (GETDATE());
    PRINT 'Columna Fecha añadida a Eventos (los eventos nuevos la rellenan con GETDATE()).';
END
ELSE
    PRINT 'La columna Fecha ya existe en Eventos.';
GO

-- Backfill de mejor esfuerzo para los eventos antiguos: la mayoría terminan en
-- "... at dd/MM/yyyy HH:mm:ss". Se intenta interpretar; lo que falle queda a NULL.
UPDATE dbo.Eventos
SET Fecha = TRY_CONVERT(datetime, LTRIM(RIGHT(Evento, CHARINDEX(' ta ', REVERSE(Evento) + ' ta ') - 1)), 103)
WHERE Fecha IS NULL
  AND Evento LIKE '%at [0-9][0-9]/[0-9][0-9]/%';
GO

PRINT 'Backfill de fechas antiguas terminado (mejor esfuerzo).';
GO
