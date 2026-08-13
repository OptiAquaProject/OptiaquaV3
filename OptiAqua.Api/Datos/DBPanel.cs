namespace DatosOptiaqua {
    using NPoco;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using webapi.Utiles;

    /// <summary>
    /// Métodos de apoyo al panel de administración del cuadro de mando.
    /// </summary>
    public static partial class DB {

        // ---- Temporadas ----------------------------------------------------------------

        public class TemporadaItem {
            public string IdTemporada { get; set; }
            public string Descripcion { get; set; }
            public DateTime FechaInicial { get; set; }
            public DateTime FechaFinal { get; set; }
            public bool Activa { get; set; }
            public string IdVersionMapa { get; set; }
            public int NUnidadesCultivo { get; set; }
        }

        public static List<TemporadaItem> TemporadasPanelList() {
            using (var db = Conexion.Nueva()) {
                var sql = @"
                    SELECT t.IdTemporada, t.Descripcion, t.FechaInicial, t.FechaFinal,
                           CAST(ISNULL(t.Activa,0) AS bit) AS Activa, t.IdVersionMapa,
                           (SELECT COUNT(DISTINCT ucc.IdUnidadCultivo) FROM UnidadCultivoCultivo ucc WHERE ucc.IdTemporada=t.IdTemporada) AS NUnidadesCultivo
                    FROM Temporada t
                    ORDER BY t.FechaInicial DESC";
                return db.Fetch<TemporadaItem>(sql);
            }
        }

        /// <summary>Marca una temporada como activa y desactiva las demás. Transaccional.</summary>
        public static void TemporadaSetActiva(string idTemporada) {
            using (var db = Conexion.Nueva()) {
                using (var tr = db.GetTransaction()) {
                    db.Execute("UPDATE Temporada SET Activa=0 WHERE ISNULL(Activa,0)=1");
                    int n = db.Execute("UPDATE Temporada SET Activa=1 WHERE IdTemporada=@0", idTemporada);
                    if (n == 0)
                        throw new Exception("No existe la temporada " + idTemporada);
                    tr.Complete();
                }
            }
            // El cambio de temporada activa invalida los balances memorizados.
            CacheDatosHidricos.ClearAll();
            Log.Info("Temporada activa cambiada a " + idTemporada);
        }

        // ---- Eventos -------------------------------------------------------------------

        public class EventoItem {
            public int IdEvento { get; set; }
            public string Evento { get; set; }
            public DateTime? Fecha { get; set; }
        }

        /// <summary>
        /// Lista de eventos, opcionalmente filtrada por rango de fechas y texto.
        ///
        /// La tabla Eventos puede no tener aún la columna Fecha (ver migración
        /// sql/2026-08-13-eventos-fecha.sql). Si no existe, se devuelve sin fecha y el filtro por
        /// fechas no se aplica; la aplicación sigue funcionando.
        /// </summary>
        public static List<EventoItem> EventosList(DateTime? desde, DateTime? hasta, string texto, int maximo = 500) {
            using (var db = Conexion.Nueva()) {
                bool hayFecha = (db.SingleOrDefault<int?>(
                    "SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.Eventos') AND name='Fecha'") ?? 0) > 0;

                var where = new List<string>();
                var args = new List<object>();
                if (!string.IsNullOrWhiteSpace(texto)) {
                    where.Add("Evento LIKE @" + args.Count);
                    args.Add("%" + texto + "%");
                }
                if (hayFecha) {
                    if (desde != null) { where.Add("Fecha >= @" + args.Count); args.Add(desde.Value.Date); }
                    if (hasta != null) { where.Add("Fecha < @" + args.Count); args.Add(hasta.Value.Date.AddDays(1)); }
                }
                string filtro = where.Count > 0 ? " WHERE " + string.Join(" AND ", where) : "";
                string cols = hayFecha ? "IdEvento, Evento, Fecha" : "IdEvento, Evento, CAST(NULL AS datetime) AS Fecha";
                string sql = $"SELECT TOP {maximo} {cols} FROM Eventos{filtro} ORDER BY IdEvento DESC";
                return db.Fetch<EventoItem>(sql, args.ToArray());
            }
        }

        public static bool EventosTieneColumnaFecha() {
            using (var db = Conexion.Nueva()) {
                return (db.SingleOrDefault<int?>(
                    "SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.Eventos') AND name='Fecha'") ?? 0) > 0;
            }
        }

        // ---- Ejecución de un script SQL (para el botón de la tabla ApiKey) --------------

        /// <summary>
        /// Ejecuta un script SQL por lotes (separados por líneas 'GO'). Devuelve un resumen.
        /// No lanza excepción: cualquier fallo se registra y se devuelve en el texto.
        /// </summary>
        public static string EjecutarScriptSql(string textoScript) {
            if (string.IsNullOrWhiteSpace(textoScript))
                return "El script está vacío.";
            // Separa por líneas que sean solo 'GO' (delimitador de lote de SQL Server).
            var lotes = Regex.Split(textoScript, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)
                             .Where(l => !string.IsNullOrWhiteSpace(l))
                             .ToList();
            int ok = 0;
            try {
                using (var db = Conexion.Nueva()) {
                    foreach (var lote in lotes) {
                        db.Execute(lote);
                        ok++;
                    }
                }
                Log.Info($"Script SQL ejecutado: {ok} lote(s).");
                return $"Script ejecutado correctamente ({ok} lote(s)).";
            } catch (Exception ex) {
                Log.Error("Error ejecutando script SQL desde el panel", ex);
                return $"Ejecutados {ok} lote(s) y falló el siguiente: {ex.Message}";
            }
        }

        // ---- Parcelas (editor) ---------------------------------------------------------

        /// <summary>Actualiza los campos descriptivos de una parcela (no toca geometría).</summary>
        public static void ParcelaGuardarDatos(int idParcelaInt, string descripcion, int? idRegante, double superficieM2) {
            using (var db = Conexion.Nueva()) {
                int n = db.Execute(
                    "UPDATE Parcela SET Descripcion=@0, IdRegante=@1, SuperficieM2=@2 WHERE IdParcelaInt=@3",
                    descripcion, idRegante, superficieM2, idParcelaInt);
                if (n == 0)
                    throw new Exception("No existe la parcela " + idParcelaInt);
            }
            Log.Info("Parcela " + idParcelaInt + " actualizada desde el panel");
        }
    }
}
