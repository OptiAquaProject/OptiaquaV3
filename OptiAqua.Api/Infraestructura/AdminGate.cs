using DatosOptiaqua;

namespace OptiAqua.Api.Infraestructura {
    /// <summary>
    /// Comprobación de credenciales de administrador para las acciones del panel.
    ///
    /// El cuadro de mando es público (solo lectura), pero las acciones que modifican datos
    /// (activar temporada, editar regante/parcela, ejecutar SQL, lanzar recálculo/SIAR, importar
    /// o eliminar mapas) exigen usuario y contraseña de administrador, igual que ya hacía la
    /// importación. Las credenciales se validan en el servidor en cada acción.
    /// </summary>
    public static class AdminGate {
        public static bool EsAdmin(string nif, string pass, out string error) {
            error = null;
            Models.Regante regante;
            if (!DB.IsCorrectPassword(new Models.LoginRequest { NifRegante = nif, Password = pass }, out regante)) {
                error = "Usuario o contraseña no válidos";
                return false;
            }
            if (regante == null || regante.Role != "admin") {
                error = "Esta operación requiere permisos de administrador";
                return false;
            }
            return true;
        }
    }
}
