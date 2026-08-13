using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace webapi.Utiles {
    /// <summary>
    /// Utiles de cadenas
    /// </summary>
    public static class Extensiones {        
        /// <summary>
        /// Añadir ' al comienzo y al final de del string (sino los tuviera ya)
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string Quoted(this string str) {
            if (str.Length == 0)
                return "''";
            if (str[0] != '\'')
                str = '\'' + str;
            if (str[str.Length - 1] != '\'')
                str = str + '\'';
            return str;
        }

        public static string Unquoted(this string str) {
            return $"{str.Replace("'", "")}";            
        }





        public static string Truncate(this string @this, int maxLength) {
            const string suffix = "...";

            if (@this == null || @this.Length <= maxLength) {
                return @this;
            }

            int strLength = maxLength - suffix.Length;
            return @this.Substring(0, strLength) + suffix;
        }

    }
}