-- =============================================================================
-- OptiAquaV2 — Estado hídrico materializado por unidad de cultivo
--
-- ENTREGADO. El panel lo ejecuta desde su casilla si la tabla no existe.
--
-- Guarda la RESPUESTA, no el cálculo: el DatosEstadoHidrico del último día
-- calculado de cada unidad de cultivo. Es lo que abren el regante en MiZona, el
-- panel de administración y /api/DatosHidricos.
--
-- Por qué la respuesta y no la serie del balance (medido, ver
-- PERSISTENCIA-BALANCES.md): montar un DatosEstadoHidrico cuesta 7,68 ms de
-- cargar UnidadCultivoDatosHidricos + 5,13 ms de calcular el balance + 2 ms de
-- componerlo. Guardar la serie no ahorra los 7,68 ms, porque DatosEstadoHidrico
-- lee del propio UnidadCultivoDatosHidricos —alias, regante, superficie,
-- estación, textura…—; guardar la respuesta los ahorra todos y deja la pantalla
-- en una lectura indexada.
--
-- La fila se rehace cuando: cambia el día pedido, sube VersionAlgoritmo, o algo
-- la invalida (riegos, clima, datos extra, o un cambio estructural). La
-- invalidación BORRA la fila; no hay estado intermedio que interpretar.
--
-- HashEntradas no se comprueba al leer —costaría lo mismo que recalcular—: lo
-- escribe el cálculo, que ya tiene los datos delante, y lo compara la pasada
-- nocturna para cazar lo que se haya modificado sin avisar por ningún camino.
-- =============================================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'EstadoHidricoUC')
BEGIN
    CREATE TABLE dbo.EstadoHidricoUC (
        IdTemporada      NVARCHAR(20)   NOT NULL,
        IdUnidadCultivo  NVARCHAR(20)   NOT NULL,
        Fecha            DATE           NOT NULL,   -- el día al que se refiere el estado
        Datos            NVARCHAR(MAX)  NOT NULL,   -- el DatosEstadoHidrico serializado en JSON
        HashEntradas     CHAR(64)       NULL,       -- SHA-256 en hexadecimal de las entradas
        VersionAlgoritmo INT            NOT NULL,
        FechaCalculo     DATETIME       NOT NULL CONSTRAINT DF_EstadoHidricoUC_FechaCalculo DEFAULT (GETDATE()),
        CONSTRAINT PK_EstadoHidricoUC PRIMARY KEY CLUSTERED (IdTemporada, IdUnidadCultivo)
    );

    -- La invalidación por clima llega por estación y la de riegos por unidad de
    -- cultivo: los dos caminos borran por IdUnidadCultivo sin temporada.
    CREATE INDEX IX_EstadoHidricoUC_UC ON dbo.EstadoHidricoUC (IdUnidadCultivo);

    PRINT 'Creada la tabla EstadoHidricoUC.';
END
ELSE
    PRINT 'La tabla EstadoHidricoUC ya existe, no se toca.';
GO
