using DatosOptiaqua;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace OptiAqua.Api.Infraestructura {
    /// <summary>
    /// Comprobación de estado publicada en /health.
    ///
    /// La aplicación arranca aunque la base de datos no responda; este punto permite que el
    /// sistema de monitorización se entere, en lugar de descubrirlo por las quejas de los usuarios.
    /// </summary>
    public class ComprobacionBaseDatos : IHealthCheck {
        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) {
            try {
                using (var db = Conexion.Nueva()) {
                    db.Execute("SELECT 1");
                }
                return Task.FromResult(HealthCheckResult.Healthy("La base de datos responde"));
            } catch (Exception ex) {
                return Task.FromResult(HealthCheckResult.Unhealthy("La base de datos no responde", ex));
            }
        }
    }
}
