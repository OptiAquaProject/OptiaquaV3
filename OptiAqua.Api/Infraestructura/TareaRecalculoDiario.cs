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
    /// Antes se arrancaba a mano desde Application_Start con GetAwaiter().GetResult(); ahora lo
    /// gestiona el servicio alojado de Quartz, que respeta el ciclo de vida de la aplicación.
    /// [DisallowConcurrentExecution] evita que dos ejecuciones se solapen.
    /// </summary>
    [DisallowConcurrentExecution]
    public class TareaRecalculoDiario : IJob {
        private readonly ILogger<TareaRecalculoDiario> log;

        public TareaRecalculoDiario(ILogger<TareaRecalculoDiario> log) {
            this.log = log;
        }

        public Task Execute(IJobExecutionContext context) {
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
    }
}
