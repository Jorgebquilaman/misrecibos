-- ============================================================================
-- ZkSync: control de checkpoint y (opcional) índice antiduplicados
-- Base: la MISMA SQL Server que usa el software oficial del proveedor (ZKBio).
-- Ejecutar una vez como admin de esa base.
-- ============================================================================

-- 1) Tabla de checkpoint del worker (guarda el último timestamp procesado por dispositivo).
IF OBJECT_ID(N'dbo.ZkSync_Control_Checkpoint', N'U') IS NULL
CREATE TABLE dbo.ZkSync_Control_Checkpoint (
    Dispositivo      NVARCHAR(20)  NOT NULL PRIMARY KEY,   -- ej. 'SALA-3'
    UltimoCheckpoint DATETIME      NOT NULL,               -- último CHECKTIME procesado
    ActualizadoEn    DATETIME      NOT NULL DEFAULT GETDATE()
);
GO

-- 2) OPCIONAL: índice único antiduplicados sobre la tabla de marcas del proveedor.
--    NO HABILITAR de entrada: si el checkinout ya trae duplicados históricos el CREATE fallará;
--    limpiar duplicados primero (script de detección más abajo) y recién ahí crear el índice.
--    Con el índice, el INSERT fallará de vuelta ante duplicados y el worker igual chequea
--    existencia antes de insertar, así que el índice es defensa en profundidad.
--
-- CREATE UNIQUE INDEX UX_checkinout_usuario_fecha_tipo
--     ON dbo.checkinout (USERID, CHECKTIME, CHECKTYPE)
--     WHERE sn = 'PORTAL-IUPA';   -- filtrado: solo filas escritas por este worker
-- GO

-- 2b) Detección de duplicados históricos (antes de querer crear el índice único).
--     SELECT USERID, CONVERT(varchar(19), CHECKTIME, 120), CHECKTYPE, COUNT(*)
--     FROM dbo.checkinout
--     GROUP BY USERID, CHECKTIME, CHECKTYPE
--     HAVING COUNT(*) > 1;
GO
