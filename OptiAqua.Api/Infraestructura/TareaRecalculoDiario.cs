using DatosOptiaqua;
using Quartz;
using System.Diagnostics;

namespace OptiAqua.Api.Infraestructura {
    /// <summary>
    /// La pasada diaria: clima del SIAR, suelos y estado hídrico de todas las unidades de
    /// cultivo, en ese orden y en un único momento del día (las 9:00 por defecto).
    ///
    /// El orden importa, y es la razón de que sea UNA tarea y no tres: el balance lee el suelo
    /// de SueloUnidadCultivoTemporada y el clima de DatoClimatico, así que rehacerlo antes que
    /// ellos deja el resultado de ayer con fecha de hoy. Antes esta tarea solo rehacía los
    /// balances y los suelos había que lanzarlos a mano desde el panel.
    ///
    /// LA TAREA NO PUEDE FIARSE DE ESTAR VIVA A LAS 9:00. El planificador es en memoria, así
    /// que muere con el proceso, y el proceso puede no estar levantado a esa hora:
    ///
    ///  - En IIS, el módulo de ASP.NET Core hereda el grupo de aplicaciones: reciclado cada 29
    ///    horas y, sobre todo, apagado por inactividad a los 20 minutos sin visitas. De
    ///    madrugada no entra nadie, así que a las 9:00 puede no haber proceso.
    ///  - Como servicio (systemd o servicio de Windows) esto no pasa, pero sí un reinicio por
    ///    despliegue o por caída.
    ///
    /// Al arrancar, un segundo disparador comprueba si se ha perdido alguna ejecución
    /// —comparando la última hora programada por el cron con la marca que deja la propia
    /// pasada— y la recupera. Con eso la hora deja de depender del alojamiento.
    ///
    /// [DisallowConcurrentExecution] evita que dos ejecuciones se solapen; el cerrojo de
    /// CacheDatosHidricos protege además de los recálculos lanzados desde el panel.
    /// </summary>
    [DisallowConcurrentExecution]
    public class TareaRecalculoDiario : IJob {
        /// <summary>Nombre del disparador que se ejecuta una sola vez al arrancar.</summary>
        public const string DisparadorArranque = "RecalculoDiario-arranque";

        /// <summary>Cron por defecto: todos los días a las 9:00.</summary>
        public const string CronPorDefecto = "0 0 9 * * ?";

        private readonly ILogger<TareaRecalculoDiario> log;
        private readonly IConfiguration config;

        public TareaRecalculoDiario(ILogger<TareaRecalculoDiario> log, IConfiguration config) {
            this.log = log;
            this.config = config;
        }

        public Task Execute(IJobExecutionContext context) {
            bool esArranque = context.Trigger.Key.Name == DisparadorArranque;
            if (esArranque) {
                DateTimeOffset? perdida = EjecucionPerdida();
                if (perdida == null) {
                    log.LogInformation("Arranque: la pasada diaria está al día, no hay nada que recuperar");
                    return Task.CompletedTask;
                }
                log.LogWarning("Arranque: no se ejecutó la pasada de las {Prevista:dd/MM/yyyy HH:mm}; se recupera ahora",
                               perdida.Value.LocalDateTime);
            }

            var cronometro = Stopwatch.StartNew();
            log.LogInformation("Comienza la pasada diaria: clima, suelos y estado hídrico");
            try {
                string resultado = CacheDatosHidricos.PasadaDiaria();
                log.LogInformation("Pasada diaria terminada en {Segundos:N0} s. {Resultado}",
                                   cronometro.Elapsed.TotalSeconds, resultado);
            } catch (Exception ex) {
                // Se registra y se termina: que falle la pasada no debe tumbar el planificador,
                // que volverá a intentarlo mañana. Y la aplicación sigue sirviendo: lo que no se
                // haya rehecho se recalcula solo al consultarlo.
                log.LogError(ex, "La pasada diaria falló tras {Segundos:N0} s", cronometro.Elapsed.TotalSeconds);
                try { DB.InsertaEvento("La pasada diaria falló: " + ex.Message); } catch { }
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// La última hora a la que el cron debería haber disparado, si esa ejecución no llegó a
        /// hacerse. null si está todo al día.
        ///
        /// Se calcula avanzando con el cron desde hace tres días en vez de con GetTimeBefore,
        /// que en Quartz.NET no está implementado y devuelve null.
        /// </summary>
        private DateTimeOffset? EjecucionPerdida() {
            try {
                return EjecucionPerdida(config["Tareas:CronRecalculo"] ?? CronPorDefecto,
                                        CacheDatosHidricos.UltimaPasadaDiaria(),
                                        DateTimeOffset.Now);
            } catch (Exception ex) {
                // Si no se puede averiguar, NO se recupera: más vale saltarse una pasada que
                // lanzar un proceso largo en cada arranque.
                log.LogWarning(ex, "No se pudo comprobar si la pasada diaria estaba al día");
                return null;
            }
        }

        /// <summary>
        /// Función pura, para poder comprobarla sin reloj ni base de datos.
        /// </summary>
        /// <param name="cronExpresion">El cron de la tarea.</param>
        /// <param name="ultimaHecha">Cuándo terminó la última pasada, o null si no consta.</param>
        /// <param name="ahora">Momento actual.</param>
        /// <returns>La hora prevista que se perdió, o null si no falta ninguna.</returns>
        public static DateTimeOffset? EjecucionPerdida(string cronExpresion, DateTime? ultimaHecha, DateTimeOffset ahora) {
            var cron = new CronExpression(cronExpresion);
            DateTimeOffset? ultimaPrevista = null;
            DateTimeOffset cursor = ahora.AddDays(-3);
            for (int i = 0; i < 500; i++) {
                DateTimeOffset? siguiente = cron.GetTimeAfter(cursor);
                if (siguiente == null || siguiente > ahora)
                    break;
                ultimaPrevista = siguiente;
                cursor = siguiente.Value;
            }
            if (ultimaPrevista == null)
                return null;
            if (ultimaHecha != null && ultimaHecha.Value >= ultimaPrevista.Value.LocalDateTime)
                return null;
            return ultimaPrevista;
        }
    }
}
