using System.Linq;
using System.Security.Claims;

namespace webapi.Utiles {
    /// <summary>
    /// Lectura de la identidad del usuario a partir de los claims del token JWT.
    ///
    /// En ASP.NET Core el usuario autenticado viaja en HttpContext.User, no en
    /// Thread.CurrentPrincipal (que en Core no es fiable). Por eso son métodos de extensión de
    /// ClaimsPrincipal: en un controlador se usan directamente sobre "User".
    ///
    /// El patrón original —identity.Claims.SingleOrDefault(...).Value— lanzaba
    /// NullReferenceException cuando el claim no estaba presente. Aquí no se lanza: se devuelve
    /// false y el llamador responde 401.
    /// </summary>
    public static class Identidad {
        /// <summary>
        /// Obtiene el identificador y el role del usuario autenticado.
        /// </summary>
        /// <returns>false si no hay usuario autenticado o el token no trae los claims esperados.</returns>
        public static bool TryLeer(this ClaimsPrincipal usuario, out int idRegante, out string role) {
            idRegante = 0;
            role = null;
            if (usuario == null || usuario.Identity == null || !usuario.Identity.IsAuthenticated)
                return false;

            Claim claimId = usuario.Claims.FirstOrDefault(c => c.Type == "IdRegante");
            Claim claimRole = usuario.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role);
            if (claimId == null || claimRole == null)
                return false;

            if (!int.TryParse(claimId.Value, out idRegante))
                return false;

            role = claimRole.Value;
            return true;
        }

        /// <summary>
        /// Indica si el usuario autenticado tiene role de administrador.
        /// </summary>
        public static bool EsAdmin(this ClaimsPrincipal usuario) {
            int idRegante;
            string role;
            if (!usuario.TryLeer(out idRegante, out role))
                return false;
            return role == "admin";
        }

        /// <summary>
        /// NIF del regante autenticado, o null si no consta.
        /// </summary>
        public static string Nif(this ClaimsPrincipal usuario) {
            if (usuario == null)
                return null;
            Claim claim = usuario.Claims.FirstOrDefault(c => c.Type == "NifRegante");
            return claim == null ? null : claim.Value;
        }
    }
}
