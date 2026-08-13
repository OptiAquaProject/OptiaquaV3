using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace OptiAqua.Api.Infraestructura {
    /// <summary>
    /// Captura cualquier excepción no controlada, la registra completa y devuelve al cliente
    /// una respuesta neutra con el identificador de la petición.
    ///
    /// Sustituye a los "catch (Exception ex) { return BadRequest(ex.Message); }" repetidos en
    /// todos los controladores, que hacían justo lo contrario: mandaban el detalle interno
    /// (tablas, SQL, rutas) al cliente y no dejaban ningún rastro en el servidor.
    /// </summary>
    public class ManejadorDeErrores {
        private readonly RequestDelegate siguiente;
        private readonly ILogger<ManejadorDeErrores> log;

        public ManejadorDeErrores(RequestDelegate siguiente, ILogger<ManejadorDeErrores> log) {
            this.siguiente = siguiente;
            this.log = log;
        }

        public async Task Invoke(HttpContext contexto) {
            try {
                await siguiente(contexto);
            } catch (Exception ex) {
                string idPeticion = contexto.TraceIdentifier;
                log.LogError(ex, "Fallo no controlado atendiendo {Metodo} {Ruta} (petición {IdPeticion})",
                    contexto.Request.Method, contexto.Request.Path, idPeticion);

                if (contexto.Response.HasStarted) {
                    // La respuesta ya iba camino del cliente: no se puede sustituir.
                    log.LogWarning("La respuesta de la petición {IdPeticion} ya había empezado a enviarse", idPeticion);
                    return;
                }

                contexto.Response.Clear();
                contexto.Response.StatusCode = StatusCodes.Status500InternalServerError;
                contexto.Response.ContentType = "application/problem+json";
                var problema = new ProblemDetails {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "Error interno",
                    Detail = "No se pudo completar la operación. Indique la referencia al comunicar la incidencia.",
                    Instance = contexto.Request.Path
                };
                problema.Extensions["referencia"] = idPeticion;
                await contexto.Response.WriteAsJsonAsync(problema);
            }
        }
    }
}
