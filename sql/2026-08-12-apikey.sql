-- =============================================================================
-- OptiAquaV2 — Tabla de claves de API
--
-- ENTREGADO, NO EJECUTADO. Revísalo y ejecútalo tú en el servidor.
--
-- Sostiene la autenticación por cabecera X-Api-Key. Cada clave está asociada a un
-- regante, del que hereda role y permisos, de modo que el control de acceso que ya
-- existe se aplica igual venga la petición con token JWT o con clave de API.
--
-- De la clave sólo se guarda el SHA-256 en hexadecimal: si se pierde, se emite otra.
-- Así una copia de la base de datos no entrega las claves de nadie.
-- =============================================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ApiKey')
BEGIN
    CREATE TABLE dbo.ApiKey (
        IdApiKey        INT IDENTITY(1,1) NOT NULL,
        Descripcion     NVARCHAR(200)     NOT NULL,
        ClaveHash       CHAR(64)          NOT NULL,   -- SHA-256 en hexadecimal
        IdRegante       INT               NOT NULL,
        Activa          BIT               NOT NULL CONSTRAINT DF_ApiKey_Activa DEFAULT (1),
        FechaAlta       DATETIME          NOT NULL CONSTRAINT DF_ApiKey_FechaAlta DEFAULT (GETDATE()),
        FechaCaducidad  DATETIME          NULL,
        UltimoUso       DATETIME          NULL,
        CONSTRAINT PK_ApiKey PRIMARY KEY CLUSTERED (IdApiKey),
        CONSTRAINT FK_ApiKey_Regante FOREIGN KEY (IdRegante) REFERENCES dbo.Regante (IdRegante)
    );

    -- La comprobación de cada petición entra por el hash: sin este índice sería un
    -- recorrido completo de la tabla en cada llamada.
    CREATE UNIQUE INDEX UX_ApiKey_ClaveHash ON dbo.ApiKey (ClaveHash);

    -- Para localizar rápido las claves de un regante al darlo de baja o revisarlo.
    CREATE INDEX IX_ApiKey_IdRegante ON dbo.ApiKey (IdRegante);

    PRINT 'Tabla ApiKey creada.';
END
ELSE
    PRINT 'La tabla ApiKey ya existe: no se ha modificado.';
GO

-- =============================================================================
-- Uso
--
-- Las claves NO se dan de alta a mano: se emiten desde la API, que es quien genera
-- el valor aleatorio y guarda sólo su hash.
--
--   POST /api/apikeys        (con token de administrador)
--   { "Descripcion": "Pasarela de riego Nebula", "IdRegante": 1, "DiasValidez": 365 }
--
-- La respuesta trae la clave en claro UNA sola vez.
--
-- El sistema externo la envía después en cada petición:
--   X-Api-Key: oaq_xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
--
-- Para revocarla:  DELETE /api/apikeys/{idApiKey}
-- Para listarlas:  GET    /api/apikeys
-- =============================================================================
