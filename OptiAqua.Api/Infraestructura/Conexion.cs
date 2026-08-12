using Microsoft.Data.SqlClient;
using NPoco;
using System;

namespace DatosOptiaqua {
    /// <summary>
    /// Origen único de las conexiones a la base de datos.
    ///
    /// Antes, la cadena se leía del Web.config en cada acceso mediante ConfigurationManager,
    /// con una constante distinta según se compilase en Debug o en Release. Ahora la fija el
    /// arranque a partir de la configuración (appsettings, variables de entorno o secretos de
    /// usuario), que es también lo que permite sacar las credenciales del control de versiones.
    /// </summary>
    public static class Conexion {
        private static string cadena;

        public static void Configura(string cadenaConexion) {
            if (string.IsNullOrWhiteSpace(cadenaConexion))
                throw new ArgumentException("La cadena de conexión no puede estar vacía", nameof(cadenaConexion));
            cadena = cadenaConexion;
        }

        /// <summary>
        /// Crea una conexión nueva. El llamador es responsable de liberarla (using).
        /// </summary>
        public static Database Nueva() {
            if (cadena == null)
                throw new InvalidOperationException("La conexión no está configurada: falta llamar a Conexion.Configura durante el arranque.");
            return new Database(cadena, DatabaseType.SqlServer2012, SqlClientFactory.Instance);
        }
    }
}
