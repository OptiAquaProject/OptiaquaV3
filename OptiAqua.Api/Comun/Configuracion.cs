
namespace DatosOptiaqua {
    /// <summary>
    /// Clase dedicada a optener y guardar parámetros de la aplicación almacenados en la tabla configuracion
    /// </summary>
    public static class Config {
        /// <summary>
        /// GetString - Retorna el parametro almacenado como un string
        /// </summary>
        /// <param name="parametro">The parametro<see cref="string"/></param>
        /// <returns>The <see cref="string"/></returns>
        public static string GetString(string parametro) => DB.ConfigLoad(parametro);

        /// <summary>
        /// GetDouble - Retorna el parametro almacenado como un double
        /// </summary>
        /// <param name="parametro">The parametro<see cref="string"/></param>
        /// <returns>The <see cref="double"/></returns>
        public static double GetDouble(string parametro) => double.Parse(DB.ConfigLoad(parametro));

        public static DateTime? GetDateTime(string parametro) {
            if (DateTime.TryParse(DB.ConfigLoad(parametro), out var fecha))
                return fecha;
            else
                return null;        
        }





        public static void SetDateTime(string parametro, DateTime valor) => DB.ConfigSave(parametro, valor.ToString());

    }
}
