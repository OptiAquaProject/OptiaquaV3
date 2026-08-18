namespace OptiAqua.Api.Infraestructura {

    /// <summary>
    /// Si la documentación interactiva de la API está publicada en esta instancia.
    ///
    /// Se resuelve en el arranque y se guarda aquí para que la barra de navegación pueda
    /// decidir si enseña el enlace. Antes lo enseñaba siempre y en producción llevaba a un
    /// 404, porque Swagger solo se montaba en desarrollo: un menú que no lleva a ningún sitio
    /// es peor que no tener menú.
    /// </summary>
    public static class OpcionesSwagger {

        /// <summary>Si /swagger responde en esta instancia.</summary>
        public static bool Habilitado { get; private set; }

        /// <summary>Si además exige sesión de administrador para entrar.</summary>
        public static bool SoloAdmin { get; private set; }

        /// <summary>Fija el estado. La llama el arranque.</summary>
        /// <param name="habilitado">Si se publica.</param>
        /// <param name="soloAdmin">Si se protege tras la sesión de administrador.</param>
        public static void Configura(bool habilitado, bool soloAdmin) {
            Habilitado = habilitado;
            SoloAdmin = soloAdmin;
        }
    }
}
