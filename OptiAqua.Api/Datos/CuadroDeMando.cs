using NPoco;
using System;
using System.Collections.Generic;
using webapi.Utiles;

namespace DatosOptiaqua {

    /// <summary>Una cifra del panel, con su estado para el semáforo de color.</summary>
    public class Indicador {
        public string Titulo { get; set; }
        public string Valor { get; set; }
        public string Detalle { get; set; }
        /// <summary>ok | aviso | error</summary>
        public string Estado { get; set; } = "ok";
    }

    /// <summary>
    /// Datos que alimentan el cuadro de mando de la página de inicio.
    ///
    /// Cada cifra se obtiene de forma independiente y con su propio control de errores: si una
    /// consulta falla, ese recuadro sale marcado en rojo con el motivo y el resto del panel se
    /// sigue mostrando. La página nunca deja de responder porque la base de datos no esté.
    /// </summary>
    public class DatosCuadroDeMando {
        public bool BaseDatosOperativa { get; set; }
        public string ErrorBaseDatos { get; set; }
        public string TemporadaActiva { get; set; }
        /// <summary>Si la tabla ApiKey ya está instalada (para ofrecer o no la acción de crearla).</summary>
        public bool TablaApiKeyExiste { get; set; }
        /// <summary>Progreso del recálculo, que puede durar mucho.</summary>
        public EstadoRecalculo Recalculo { get; set; }
        public List<Indicador> Indicadores { get; } = new List<Indicador>();
        public List<string> UltimosEventos { get; } = new List<string>();
        public DateTime Generado { get; set; } = DateTime.Now;

        public static DatosCuadroDeMando Recopila() {
            var panel = new DatosCuadroDeMando();
            // El progreso vive en memoria: se puede leer aunque la base de datos no responda.
            panel.Recalculo = ProgresoRecalculo.Foto();
            Database db = null;
            try {
                db = Conexion.Nueva();
                db.Execute("SELECT 1");
                panel.BaseDatosOperativa = true;
            } catch (Exception ex) {
                panel.BaseDatosOperativa = false;
                panel.ErrorBaseDatos = ex.Message;
                Log.Error("El cuadro de mando no pudo conectar con la base de datos", ex);
                if (db != null) db.Dispose();
                return panel;
            }

            using (db) {
                panel.TemporadaActiva = Texto(db, "SELECT TOP 1 IdTemporada FROM Temporada WHERE Activa=1");

                panel.Indicadores.Add(Cuenta(db, "Temporadas", "SELECT COUNT(*) FROM Temporada",
                    string.IsNullOrEmpty(panel.TemporadaActiva) ? "Ninguna marcada como activa" : "Activa: " + panel.TemporadaActiva));

                // Si no hay temporada activa el recuento de unidades de cultivo no tiene referencia.
                if (!string.IsNullOrEmpty(panel.TemporadaActiva)) {
                    panel.Indicadores.Add(Cuenta(db, "Unidades de cultivo",
                        "SELECT COUNT(DISTINCT IdUnidadCultivo) FROM UnidadCultivoCultivo WHERE IdTemporada=@0",
                        "En la temporada activa", panel.TemporadaActiva));
                } else {
                    panel.Indicadores.Add(new Indicador {
                        Titulo = "Unidades de cultivo",
                        Valor = "—",
                        Detalle = "No hay temporada activa",
                        Estado = "aviso"
                    });
                }

                panel.Indicadores.Add(Cuenta(db, "Regantes", "SELECT COUNT(*) FROM Regante", "Dados de alta"));
                panel.Indicadores.Add(Cuenta(db, "Parcelas", "SELECT COUNT(*) FROM Parcela", "Registradas"));

                // Superficie
                try {
                    double? m2 = db.SingleOrDefault<double?>("SELECT SUM(SuperficieM2) FROM Parcela");
                    panel.Indicadores.Add(new Indicador {
                        Titulo = "Superficie",
                        Valor = m2 == null ? "—" : (m2.Value / 10000.0).ToString("N0") + " ha",
                        Detalle = "Suma de todas las parcelas"
                    });
                } catch (Exception ex) {
                    panel.Indicadores.Add(Fallo("Superficie", ex));
                }

                // Riegos del último mes
                DateTime desde = DateTime.Today.AddDays(-30);
                panel.Indicadores.Add(Cuenta(db, "Riegos (30 días)",
                    "SELECT COUNT(*) FROM Riego WHERE Fecha>=@0", "Registros recibidos", desde));

                try {
                    double? m3 = db.SingleOrDefault<double?>("SELECT SUM(RiegoM3) FROM Riego WHERE Fecha>=@0", desde);
                    panel.Indicadores.Add(new Indicador {
                        Titulo = "Agua aplicada (30 días)",
                        Valor = m3 == null ? "0 m³" : m3.Value.ToString("N0") + " m³",
                        Detalle = "Suma de los riegos del último mes"
                    });
                } catch (Exception ex) {
                    panel.Indicadores.Add(Fallo("Agua aplicada (30 días)", ex));
                }

                // Frescura de los datos climáticos del SIAR: es lo que más suele fallar.
                try {
                    DateTime? ultima = db.SingleOrDefault<DateTime?>("SELECT MAX(Fecha) FROM DatoClimatico");
                    var indicador = new Indicador { Titulo = "Datos climáticos (SIAR)" };
                    if (ultima == null) {
                        indicador.Valor = "Sin datos";
                        indicador.Detalle = "No hay ningún registro climático";
                        indicador.Estado = "error";
                    } else {
                        int diasAtraso = (int)(DateTime.Today - ultima.Value.Date).TotalDays;
                        indicador.Valor = ultima.Value.ToShortDateString();
                        indicador.Detalle = diasAtraso <= 0
                            ? "Al día"
                            : "Último dato hace " + diasAtraso + (diasAtraso == 1 ? " día" : " días");
                        indicador.Estado = diasAtraso <= 1 ? "ok" : (diasAtraso <= 4 ? "aviso" : "error");
                    }
                    panel.Indicadores.Add(indicador);
                } catch (Exception ex) {
                    panel.Indicadores.Add(Fallo("Datos climáticos (SIAR)", ex));
                }

                // Estado del recálculo diario. El detalle del progreso va en su propio panel.
                panel.Indicadores.Add(new Indicador {
                    Titulo = "Recálculo",
                    Valor = panel.Recalculo.EnCurso ? panel.Recalculo.Porcentaje + " %" : "En reposo",
                    Detalle = panel.Recalculo.EnCurso
                        ? panel.Recalculo.Tarea + ", faltan " + panel.Recalculo.Restante
                        : (panel.Recalculo.UltimaEjecucion ?? "Programado cada día a las 8:00"),
                    Estado = panel.Recalculo.EnCurso ? "aviso" : "ok"
                });

                panel.Indicadores.Add(Cuenta(db, "Mapas de suelo", "SELECT COUNT(DISTINCT IdVersion) FROM MapaSuelo", "Versiones importadas"));

                // Claves de API. La tabla puede no existir todavía: el script de creación se
                // entrega aparte y se ejecuta a mano, así que aquí se avisa en vez de fallar.
                try {
                    bool existe = (db.SingleOrDefault<int?>(
                        "SELECT COUNT(*) FROM sys.tables WHERE name='ApiKey'") ?? 0) > 0;
                    panel.TablaApiKeyExiste = existe;
                    if (!existe) {
                        panel.Indicadores.Add(new Indicador {
                            Titulo = "Claves de API",
                            Valor = "Sin instalar",
                            Detalle = "Pulsa para crear la tabla",
                            Estado = "aviso"
                        });
                    } else {
                        int activas = db.SingleOrDefault<int?>("SELECT COUNT(*) FROM ApiKey WHERE Activa=1") ?? 0;
                        int caducadas = db.SingleOrDefault<int?>(
                            "SELECT COUNT(*) FROM ApiKey WHERE Activa=1 AND FechaCaducidad IS NOT NULL AND FechaCaducidad < GETDATE()") ?? 0;
                        panel.Indicadores.Add(new Indicador {
                            Titulo = "Claves de API",
                            Valor = activas.ToString("N0"),
                            Detalle = caducadas == 0
                                ? "Activas"
                                : activas + " activas, " + caducadas + " ya caducadas",
                            Estado = caducadas == 0 ? "ok" : "aviso"
                        });
                    }
                } catch (Exception ex) {
                    panel.Indicadores.Add(Fallo("Claves de API", ex));
                }

                // Últimos eventos registrados
                try {
                    panel.UltimosEventos.AddRange(
                        db.Fetch<string>("SELECT TOP 12 Evento FROM Eventos ORDER BY IdEvento DESC"));
                } catch (Exception ex) {
                    Log.Aviso("El cuadro de mando no pudo leer la tabla de eventos", ex);
                }
            }
            return panel;
        }

        private static Indicador Cuenta(Database db, string titulo, string sql, string detalle, params object[] args) {
            try {
                int n = db.SingleOrDefault<int?>(sql, args) ?? 0;
                return new Indicador { Titulo = titulo, Valor = n.ToString("N0"), Detalle = detalle };
            } catch (Exception ex) {
                return Fallo(titulo, ex);
            }
        }

        private static string Texto(Database db, string sql) {
            try {
                return db.SingleOrDefault<string>(sql);
            } catch (Exception ex) {
                Log.Aviso("El cuadro de mando no pudo ejecutar: " + sql, ex);
                return null;
            }
        }

        private static Indicador Fallo(string titulo, Exception ex) {
            Log.Aviso("El cuadro de mando no pudo calcular el indicador '" + titulo + "'", ex);
            return new Indicador {
                Titulo = titulo,
                Valor = "Error",
                Detalle = ex.Message,
                Estado = "error"
            };
        }
    }
}
