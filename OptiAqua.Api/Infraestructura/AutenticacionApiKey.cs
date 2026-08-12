using DatosOptiaqua;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace OptiAqua.Api.Infraestructura {

    /// <summary>
    /// Autenticación por clave de API, para sistemas que se integran con OptiAqua y no pueden
    /// mantener una sesión con usuario y contraseña.
    ///
    /// La clave viaja en la cabecera <c>X-Api-Key</c>. Se admite también
    /// <c>Authorization: ApiKey &lt;clave&gt;</c> para clientes que sólo saben usar esa cabecera.
    ///
    /// Punto importante: la identidad que se construye lleva EXACTAMENTE los mismos claims que
    /// un token JWT (IdRegante, NifRegante y role). Así todo el control de acceso que ya existe
    /// —EstaAutorizado, User.EsAdmin, [Authorize(Policy="Administrador")]— se aplica igual a las
    /// peticiones con clave de API. No es una vía para saltarse los permisos: una clave tiene
    /// exactamente los del regante al que está asociada.
    /// </summary>
    public class AutenticacionApiKey : AuthenticationHandler<AuthenticationSchemeOptions> {
        public const string Esquema = "ApiKey";
        public const string Cabecera = "X-Api-Key";

        public AutenticacionApiKey(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder) {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync() {
            string clave = LeeClave();
            if (string.IsNullOrWhiteSpace(clave)) {
                // Sin clave no se falla: puede que la petición traiga un token JWT y sea ese
                // esquema el que deba resolverla.
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            ApiKeyValidada validada = ApiKeys.Valida(clave);
            if (validada == null) {
                Logger.LogWarning("Clave de API no válida desde {IP}",
                    Context.Connection.RemoteIpAddress);
                return Task.FromResult(AuthenticateResult.Fail("Clave de API no válida"));
            }

            var identidad = new ClaimsIdentity(Esquema);
            identidad.AddClaim(new Claim("IdRegante", validada.IdRegante.ToString()));
            identidad.AddClaim(new Claim("NifRegante", validada.Nif ?? ""));
            identidad.AddClaim(new Claim(ClaimTypes.Role, validada.Role ?? ""));
            // Deja constancia de por dónde entró, para poder distinguirlo en los registros.
            identidad.AddClaim(new Claim("IdApiKey", validada.IdApiKey.ToString()));

            var principal = new ClaimsPrincipal(identidad);
            var entrada = new AuthenticationTicket(principal, Esquema);
            return Task.FromResult(AuthenticateResult.Success(entrada));
        }

        private string LeeClave() {
            if (Request.Headers.TryGetValue(Cabecera, out var valores)) {
                string clave = valores.ToString();
                if (!string.IsNullOrWhiteSpace(clave))
                    return clave.Trim();
            }
            // Alternativa: Authorization: ApiKey <clave>
            if (Request.Headers.TryGetValue("Authorization", out var autorizacion)) {
                string cabecera = autorizacion.ToString();
                const string prefijo = "ApiKey ";
                if (cabecera != null && cabecera.StartsWith(prefijo, StringComparison.OrdinalIgnoreCase))
                    return cabecera.Substring(prefijo.Length).Trim();
            }
            return null;
        }
    }
}
