using DatosOptiaqua;
using Quartz;
using System.Diagnostics;

namespace OptiAqua.Api.Infraestructura {
    /// <summary>
    /// Recálculo diario de la caché de balances hídricos.
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
            log.LogInformation("Comienza el recálculo diario de la caché de balances");
            try {
                DB.InsertaEvento("Execute at " + DateTime.Now.ToString());
            } catch (Exception ex) {
                // No poder anotar el evento no debe impedir el recálculo.
                log.LogWarning(ex, "No se pudo registrar el inicio del recálculo en la tabla de eventos");
            }

            try {
                CacheDatosHidricos.RecreateAll();
                log.LogInformation("Recálculo diario terminado en {Segundos:N0} s", cronometro.Elapsed.TotalSeconds);
            } catch (Exception ex) {
                // Se registra y se termina: que falle el recálculo no debe tumbar el planificador.
                log.LogError(ex, "El recálculo diario falló tras {Segundos:N0} s", cronometro.Elapsed.TotalSeconds);
            }
            return Task.CompletedTask;
        }
    }
}
