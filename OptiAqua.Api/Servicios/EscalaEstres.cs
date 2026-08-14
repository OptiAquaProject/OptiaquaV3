namespace DatosOptiaqua {
    using Models;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using webapi.Utiles;

    /// <summary>
    /// Un tramo de la escala de severidad, ya resuelto para pintarlo.
    /// </summary>
    public class TramoEstres {
        /// <summary>Índice de estrés donde empieza el tramo.</summary>
        public double Desde { get; set; }
        /// <summary>Índice de estrés donde acaba (el UmbralMaximo de la base).</summary>
        public double Hasta { get; set; }
        /// <summary>Color del tramo, ya con almohadilla.</summary>
        public string Color { get; set; }
        /// <summary>Lo que significa, para el título emergente.</summary>
        public string Descripcion { get; set; }
    }

    /// <summary>
    /// Las escalas de estrés hídrico tal y como están en la base de datos.
    ///
    /// Los tramos y sus colores NO se inventan aquí: salen de `TipoEstresUmbral`, que es donde
    /// el agrónomo los tiene definidos, con una escala distinta por tipo de estrés ("aDemanda"
    /// y "Deficitario" hoy). Esta clase solo los ordena, los convierte en intervalos cerrados
    /// —en la tabla cada fila guarda su UmbralMaximo, no el mínimo— y los recuerda, porque son
    /// una docena de filas que no cambian entre peticiones.
    /// </summary>
    public static class EscalaEstres {
        /// <summary>
        /// Suelo de la escala. El índice de estrés está definido entre -1 y 1, y el primer
        /// umbral de cada escala solo dice hasta dónde llega, no desde dónde empieza.
        /// </summary>
        public const double Minimo = -1.0;

        private static Dictionary<string, List<TramoEstres>> escalas;
        private static readonly object cerrojo = new object();

        /// <summary>
        /// Tramos de una escala, de menor a mayor severidad de agua (del más seco al más
        /// encharcado). Lista vacía si no se conoce esa escala.
        /// </summary>
        /// <param name="idTipoEstres">El tipo de estrés de la etapa.</param>
        public static List<TramoEstres> Tramos(string idTipoEstres) {
            if (string.IsNullOrWhiteSpace(idTipoEstres))
                return new List<TramoEstres>();
            var todas = Todas();
            return todas.TryGetValue(idTipoEstres, out var tramos) ? tramos : new List<TramoEstres>();
        }

        /// <summary>Olvida lo memorizado. Para cuando se toquen los umbrales.</summary>
        public static void Olvida() {
            lock (cerrojo)
                escalas = null;
        }

        private static Dictionary<string, List<TramoEstres>> Todas() {
            var actual = escalas;
            if (actual != null)
                return actual;
            lock (cerrojo) {
                if (escalas != null)
                    return escalas;
                var ret = new Dictionary<string, List<TramoEstres>>();
                try {
                    foreach (var par in DB.ListaEstresUmbral()) {
                        var lista = new List<TramoEstres>();
                        double desde = Minimo;
                        foreach (TipoEstresUmbral u in par.Value.OrderBy(x => x.UmbralMaximo)) {
                            lista.Add(new TramoEstres {
                                Desde = desde,
                                Hasta = u.UmbralMaximo,
                                Color = Colores.Css(u.Color),
                                Descripcion = u.Descripcion
                            });
                            desde = u.UmbralMaximo;
                        }
                        ret[par.Key] = lista;
                    }
                } catch (Exception ex) {
                    // Sin escala la barra se pinta de un solo color; no puede tumbar la página.
                    Log.Aviso("No se pudieron cargar las escalas de estrés", ex);
                }
                escalas = ret;
                return ret;
            }
        }
    }
}
