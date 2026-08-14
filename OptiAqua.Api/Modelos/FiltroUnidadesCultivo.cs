namespace DatosOptiaqua {
    using System.Collections.Generic;

    /// <summary>
    /// Lo que el usuario ha pedido desde la cabecera de la lista de unidades de cultivo:
    /// un filtro por columna y una ordenación.
    ///
    /// Viaja por la URL para que la pantalla se pueda enlazar y recargar tal cual: filtrar
    /// una lista y perder el filtro al volver de una ficha es de las cosas que más molestan.
    /// </summary>
    public class FiltroUnidadesCultivo {
        /// <summary>Texto que debe contener el identificador.</summary>
        public string Unidad { get; set; }
        /// <summary>Texto que debe contener el nombre del regante.</summary>
        public string Regante { get; set; }
        /// <summary>Texto que debe contener el cultivo.</summary>
        public string Cultivo { get; set; }
        /// <summary>Texto que debe contener el municipio.</summary>
        public string Municipio { get; set; }
        /// <summary>Superficie mínima en m².</summary>
        public double? SuperficieMin { get; set; }
        /// <summary>Riego mínimo acumulado en mm.</summary>
        public double? RiegoMin { get; set; }
        /// <summary>
        /// Índice de estrés MÁXIMO. Se filtra por arriba y no por abajo porque lo que se busca
        /// en esta lista es lo que peor está: cuanto más bajo el índice, más seco el suelo.
        /// </summary>
        public double? EstadoMax { get; set; }
        /// <summary>"si" solo las que tienen incidencias, "no" solo las limpias, vacío todas.</summary>
        public string Incidencias { get; set; }

        /// <summary>Columna por la que ordenar; vacío para el orden natural por unidad.</summary>
        public string Orden { get; set; }
        /// <summary>"asc" o "desc".</summary>
        public string Dir { get; set; }

        /// <summary>Si hay algún filtro puesto (la ordenación no cuenta).</summary>
        public bool HayFiltro =>
            !string.IsNullOrWhiteSpace(Unidad) || !string.IsNullOrWhiteSpace(Regante) ||
            !string.IsNullOrWhiteSpace(Cultivo) || !string.IsNullOrWhiteSpace(Municipio) ||
            SuperficieMin != null || RiegoMin != null || EstadoMax != null ||
            !string.IsNullOrWhiteSpace(Incidencias);

        /// <summary>
        /// Los valores actuales como pares nombre/valor, para poder rehacer la URL al pulsar
        /// en una cabecera sin perder lo que ya estaba filtrado.
        /// </summary>
        public Dictionary<string, string> AParametros() {
            var d = new Dictionary<string, string>();
            void P(string n, string v) { if (!string.IsNullOrWhiteSpace(v)) d["filtro." + n] = v; }
            P("Unidad", Unidad); P("Regante", Regante); P("Cultivo", Cultivo); P("Municipio", Municipio);
            P("SuperficieMin", SuperficieMin?.ToString(System.Globalization.CultureInfo.InvariantCulture));
            P("RiegoMin", RiegoMin?.ToString(System.Globalization.CultureInfo.InvariantCulture));
            P("EstadoMax", EstadoMax?.ToString(System.Globalization.CultureInfo.InvariantCulture));
            P("Incidencias", Incidencias);
            return d;
        }

        /// <summary>
        /// Qué hay que pedir al pulsar en la cabecera de una columna. El ciclo es el de
        /// siempre: sin orden, de menos a más, de más a menos y otra vez sin orden.
        /// </summary>
        /// <param name="columna">Columna pulsada.</param>
        /// <returns>Los parámetros de orden y dirección; vacíos para quitar la ordenación.</returns>
        public (string Orden, string Dir) SiguienteOrden(string columna) {
            if (Orden != columna)
                return (columna, "asc");
            if (Dir == "asc")
                return (columna, "desc");
            return (null, null);
        }

        /// <summary>Flecha que le toca a la cabecera de una columna.</summary>
        /// <param name="columna">Columna.</param>
        public string Flecha(string columna) {
            if (Orden != columna)
                return "";
            return Dir == "desc" ? " ▼" : " ▲";
        }
    }
}
