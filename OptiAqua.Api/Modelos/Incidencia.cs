namespace DatosOptiaqua {
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Gravedad de una incidencia del cálculo.
    ///
    /// La frontera es UNA y muy concreta: si hay resultado o no lo hay. Todo lo que el
    /// cálculo consigue sortear —datos estimados, parámetros que faltan, límites que se
    /// aplican— es Aviso, por gordo que sea, porque hay un número que enseñar y lo que
    /// procede es decir de qué se fía. Error se reserva para cuando NO se ha podido
    /// calcular; es lo único que se marca en rojo.
    /// </summary>
    public enum GravedadIncidencia {
        /// <summary>Hay resultado, pero conviene saber sobre qué se apoya.</summary>
        Aviso = 0,
        /// <summary>No hay resultado: la incidencia ha impedido el cálculo.</summary>
        Error = 1
    }

    /// <summary>
    /// Algo que ha pasado durante el cálculo y que quien lea el resultado debería saber.
    ///
    /// El criterio del proyecto es que la aplicación siga adelante siempre que pueda, y eso
    /// tiene una contrapartida: un balance calculado con datos estimados sale con la misma
    /// pinta que uno con datos reales. Las incidencias son lo que rompe esa igualdad.
    /// </summary>
    public class Incidencia {
        /// <summary>Código estable, para poder contarlas y filtrarlas sin mirar el texto.</summary>
        public string Codigo { get; set; }
        public GravedadIncidencia Gravedad { get; set; }
        /// <summary>Explicación en castellano, ya montada, lista para enseñar.</summary>
        public string Mensaje { get; set; }
        /// <summary>Primer día afectado, si la incidencia tiene periodo.</summary>
        public DateTime? Desde { get; set; }
        /// <summary>Último día afectado, si la incidencia tiene periodo.</summary>
        public DateTime? Hasta { get; set; }

        public override string ToString() => Mensaje;
    }

    /// <summary>
    /// Recoge las incidencias mientras se calcula.
    ///
    /// Las de periodo (los días sin clima, por ejemplo) se apuntan día a día y se resumen al
    /// final agrupando los días CONSECUTIVOS en tramos: "del 12/03 al 18/03" se lee, y una
    /// lista de siete fechas sueltas no.
    /// </summary>
    public class RegistroIncidencias {
        private readonly List<Incidencia> sueltas = new List<Incidencia>();
        private readonly Dictionary<string, SortedSet<DateTime>> porPeriodo = new Dictionary<string, SortedSet<DateTime>>();
        private readonly Dictionary<string, (GravedadIncidencia Gravedad, string Plantilla)> definiciones =
            new Dictionary<string, (GravedadIncidencia, string)>();

        /// <summary>Apunta una incidencia sin periodo. No se repite si ya está el mismo código y mensaje.</summary>
        /// <param name="codigo">Código estable de la incidencia.</param>
        /// <param name="gravedad">Si invalida el resultado o solo hay que saberlo.</param>
        /// <param name="mensaje">Explicación para quien lo lea.</param>
        public void Añade(string codigo, GravedadIncidencia gravedad, string mensaje) {
            if (sueltas.Any(x => x.Codigo == codigo && x.Mensaje == mensaje))
                return;
            sueltas.Add(new Incidencia { Codigo = codigo, Gravedad = gravedad, Mensaje = mensaje });
        }

        /// <summary>
        /// Apunta un día afectado por una incidencia de periodo. Se llama tantas veces como
        /// días haga falta; el resumen los agrupa.
        /// </summary>
        /// <param name="codigo">Código estable de la incidencia.</param>
        /// <param name="gravedad">Si invalida el resultado o solo hay que saberlo.</param>
        /// <param name="plantilla">
        /// Texto con {0} para el número de días y {1} para los tramos ya redactados.
        /// </param>
        /// <param name="dia">Día afectado.</param>
        public void AñadeDia(string codigo, GravedadIncidencia gravedad, string plantilla, DateTime dia) {
            if (!porPeriodo.TryGetValue(codigo, out var dias)) {
                dias = new SortedSet<DateTime>();
                porPeriodo[codigo] = dias;
                definiciones[codigo] = (gravedad, plantilla);
            }
            dias.Add(dia.Date);
        }

        /// <summary>Las incidencias ya resumidas, con los periodos agrupados.</summary>
        public List<Incidencia> Resumen() {
            var ret = new List<Incidencia>(sueltas);
            foreach (var par in porPeriodo) {
                var dias = par.Value;
                if (dias.Count == 0)
                    continue;
                var tramos = Tramos(dias);
                var (gravedad, plantilla) = definiciones[par.Key];
                ret.Add(new Incidencia {
                    Codigo = par.Key,
                    Gravedad = gravedad,
                    Mensaje = string.Format(plantilla, dias.Count, Redacta(tramos)),
                    Desde = dias.Min,
                    Hasta = dias.Max
                });
            }
            return ret.OrderByDescending(x => x.Gravedad).ThenBy(x => x.Codigo).ToList();
        }

        /// <summary>Agrupa días consecutivos en tramos.</summary>
        private static List<(DateTime Desde, DateTime Hasta)> Tramos(SortedSet<DateTime> dias) {
            var ret = new List<(DateTime, DateTime)>();
            DateTime? inicio = null, anterior = null;
            foreach (var d in dias) {
                if (inicio == null) { inicio = d; anterior = d; continue; }
                if (d == anterior.Value.AddDays(1)) { anterior = d; continue; }
                ret.Add((inicio.Value, anterior.Value));
                inicio = d; anterior = d;
            }
            if (inicio != null)
                ret.Add((inicio.Value, anterior.Value));
            return ret;
        }

        /// <summary>
        /// Redacta los tramos. Se enseñan como mucho tres: con veinte, la lista deja de
        /// informar y pasa a estorbar.
        /// </summary>
        private static string Redacta(List<(DateTime Desde, DateTime Hasta)> tramos) {
            string Uno((DateTime Desde, DateTime Hasta) t) =>
                t.Desde == t.Hasta
                    ? "el " + t.Desde.ToString("dd/MM/yyyy")
                    : "del " + t.Desde.ToString("dd/MM/yyyy") + " al " + t.Hasta.ToString("dd/MM/yyyy");

            if (tramos.Count <= 3)
                return string.Join("; ", tramos.Select(Uno));
            return string.Join("; ", tramos.Take(3).Select(Uno)) + "; y " + (tramos.Count - 3) + " tramo(s) más";
        }
    }
}
