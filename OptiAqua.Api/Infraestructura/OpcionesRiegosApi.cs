namespace OptiAqua.Api.Infraestructura {

    /// <summary>
    /// Dirección y clave del servicio externo del que se descargan los riegos, fijadas en el
    /// arranque a partir de la configuración.
    ///
    /// Estaban escritas dentro del código. Con el repositorio en abierto eso no se sostiene:
    /// una clave en el fuente es una clave publicada. Van por el mismo camino que la cadena de
    /// conexión y la clave de firma —appsettings.local.json, variables de entorno o secretos de
    /// usuario—, y aquí solo se guarda lo que el arranque haya encontrado.
    ///
    /// A diferencia de aquellas dos, si faltan la aplicación NO se niega a arrancar: la descarga
    /// de riegos externos es opcional y quien la llama ya sabe encajar un "no hay datos". Lo que
    /// no se hace es llamar a ciegas.
    /// </summary>
    public static class OpcionesRiegosApi {

        /// <summary>Raíz del servicio, sin barra final. Vacío si no está configurado.</summary>
        public static string Url { get; private set; }

        /// <summary>Clave que viaja en la cabecera API_KEY. Vacía si no está configurada.</summary>
        public static string Clave { get; private set; }

        /// <summary>Si hay con qué llamar al servicio.</summary>
        public static bool Configurado =>
            !string.IsNullOrWhiteSpace(Url) && !string.IsNullOrWhiteSpace(Clave);

        /// <summary>Fija los valores. La llama el arranque.</summary>
        /// <param name="url">Raíz del servicio.</param>
        /// <param name="clave">Clave de API.</param>
        public static void Configura(string url, string clave) {
            Url = url?.Trim();
            Clave = clave?.Trim();
        }
    }
}
