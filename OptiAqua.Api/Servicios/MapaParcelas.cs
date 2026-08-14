namespace DatosOptiaqua {
    using Models;
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Dibuja las parcelas de una unidad de cultivo a partir de su geometría.
    ///
    /// `Parcela.GEO` es un `geometry` de SQL Server en EPSG:4326 —longitud y latitud en
    /// grados— y `GeoLocParcelasList` ya lo devuelve como texto WKT. Aquí se interpreta ese
    /// texto y se dibuja en SVG.
    ///
    /// SIN MAPA DE FONDO Y SIN BIBLIOTECAS. Una capa de teselas obligaría a que el navegador
    /// del usuario saliera a un servicio externo (OpenStreetMap, Google…) cada vez que se
    /// abre la ficha, y eso es una dependencia que conviene decidir, no colar. Lo que sí se
    /// da es la forma y el tamaño reales de las parcelas, con su escala en metros, y enlaces
    /// para abrir la posición en SIGPAC o en Google Maps, que es donde está el mapa de verdad.
    /// </summary>
    public static class MapaParcelas {
        private const int Ancho = 520;
        private const int AltoMaximo = 420;
        private const int Margen = 12;

        /// <summary>Metros por grado de latitud. Sobra para dibujar una parcela.</summary>
        private const double MetrosPorGrado = 111320.0;

        private static string N(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

        /// <summary>
        /// Las parcelas en GeoJSON, para pintarlas sobre el mapa.
        ///
        /// Se compone a mano en vez de con una biblioteca de geometría: lo que hay que
        /// convertir es un WKT de polígonos a coordenadas, y meter NetTopologySuite en la
        /// capa web para esto sería traerse un mundo por un puñado de paréntesis. GeoJSON
        /// va SIEMPRE en [longitud, latitud] y en EPSG:4326, que es justo lo que guarda
        /// `Parcela.GEO`.
        /// </summary>
        /// <param name="parcelas">Las parcelas con su WKT.</param>
        /// <returns>Una FeatureCollection; vacía si ninguna tiene geometría.</returns>
        public static string GeoJson(List<GeoLocParcela> parcelas) {
            var sb = new StringBuilder("{\"type\":\"FeatureCollection\",\"features\":[");
            bool primera = true;
            foreach (var p in parcelas ?? new List<GeoLocParcela>()) {
                var anillos = Anillos(p.GEO).Where(a => a.Count >= 3).ToList();
                if (anillos.Count == 0)
                    continue;
                if (!primera) sb.Append(',');
                primera = false;
                sb.Append("{\"type\":\"Feature\",\"properties\":{")
                  .Append("\"idParcela\":").Append(p.IdParcelaInt)
                  .Append(",\"municipio\":").Append(Cadena(p.Municipio))
                  .Append(",\"poligono\":").Append(p.IdPoligono)
                  .Append(",\"numero\":").Append(p.IdParcela)
                  .Append("},\"geometry\":{\"type\":\"Polygon\",\"coordinates\":[");
                bool primerAnillo = true;
                foreach (var anillo in anillos) {
                    if (!primerAnillo) sb.Append(',');
                    primerAnillo = false;
                    sb.Append('[');
                    for (int i = 0; i < anillo.Count; i++) {
                        if (i > 0) sb.Append(',');
                        sb.Append('[').Append(Coord(anillo[i].Lon)).Append(',').Append(Coord(anillo[i].Lat)).Append(']');
                    }
                    sb.Append(']');
                }
                sb.Append("]}}");
            }
            return sb.Append("]}").ToString();
        }

        private static string Coord(double v) => v.ToString("0.########", CultureInfo.InvariantCulture);

        private static string Cadena(string v) =>
            v == null ? "null" : "\"" + v.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

        /// <summary>Centro de las parcelas, para los enlaces a los mapas de verdad.</summary>
        /// <param name="parcelas">Las parcelas con su WKT.</param>
        /// <returns>Latitud y longitud medias, o null si no hay geometría.</returns>
        public static (double Lat, double Lon)? Centro(List<GeoLocParcela> parcelas) {
            var puntos = Puntos(parcelas);
            if (puntos.Count == 0)
                return null;
            return (puntos.Average(p => p.Lat), puntos.Average(p => p.Lon));
        }

        /// <summary>
        /// SVG con el contorno de las parcelas, a escala y con la referencia en metros.
        /// </summary>
        /// <param name="parcelas">Las parcelas de la unidad de cultivo, con su WKT.</param>
        /// <returns>El SVG listo para incrustar, o un aviso si no hay geometría.</returns>
        public static string Svg(List<GeoLocParcela> parcelas) {
            var anillos = new List<(int IdParcela, List<(double Lat, double Lon)> Puntos)>();
            foreach (var p in parcelas ?? new List<GeoLocParcela>())
                foreach (var anillo in Anillos(p.GEO))
                    if (anillo.Count >= 3)
                        anillos.Add((p.IdParcelaInt, anillo));

            if (anillos.Count == 0)
                return "<p class=\"text-muted mb-0\">Las parcelas de esta unidad de cultivo no tienen geometría registrada.</p>";

            var todos = anillos.SelectMany(a => a.Puntos).ToList();
            double latMin = todos.Min(p => p.Lat), latMax = todos.Max(p => p.Lat);
            double lonMin = todos.Min(p => p.Lon), lonMax = todos.Max(p => p.Lon);
            double latMedia = (latMin + latMax) / 2;
            // Un grado de longitud mide menos cuanto más al norte: sin corregirlo la parcela
            // sale estirada a lo ancho.
            double cos = Math.Cos(latMedia * Math.PI / 180.0);

            double anchoGrados = Math.Max((lonMax - lonMin) * cos, 1e-9);
            double altoGrados = Math.Max(latMax - latMin, 1e-9);
            double escala = (Ancho - 2 * Margen) / anchoGrados;
            double alto = altoGrados * escala + 2 * Margen;
            if (alto > AltoMaximo) {
                escala = (AltoMaximo - 2 * Margen) / altoGrados;
                alto = AltoMaximo;
            }
            double anchoDibujo = anchoGrados * escala;
            double desplazaX = (Ancho - 2 * Margen - anchoDibujo) / 2;

            double X(double lon) => Margen + desplazaX + (lon - lonMin) * cos * escala;
            double Y(double lat) => Margen + (latMax - lat) * escala;

            var sb = new StringBuilder();
            sb.Append($"<svg viewBox=\"0 0 {Ancho} {N(alto)}\" width=\"100%\" height=\"{N(alto)}\" role=\"img\" ")
              .Append("aria-label=\"Parcelas de la unidad de cultivo\" style=\"max-width:100%;background:#f8f9fa;border-radius:.375rem\">");

            var colores = new[] { "#0d6efd", "#20c997", "#fd7e14", "#6f42c1", "#d63384", "#198754" };
            int i = 0;
            foreach (var grupo in anillos.GroupBy(a => a.IdParcela)) {
                string color = colores[i++ % colores.Length];
                foreach (var anillo in grupo) {
                    var puntos = string.Join(" ", anillo.Puntos.Select(p => N(X(p.Lon)) + "," + N(Y(p.Lat))));
                    sb.Append($"<polygon points=\"{puntos}\" fill=\"{color}\" fill-opacity=\"0.25\" stroke=\"{color}\" stroke-width=\"1.5\">")
                      .Append($"<title>Parcela {grupo.Key}</title></polygon>");
                }
                // Etiqueta en el centro de la parcela
                var centroX = grupo.SelectMany(g => g.Puntos).Average(p => X(p.Lon));
                var centroY = grupo.SelectMany(g => g.Puntos).Average(p => Y(p.Lat));
                sb.Append($"<text x=\"{N(centroX)}\" y=\"{N(centroY)}\" font-size=\"11\" font-weight=\"600\" fill=\"{color}\" ")
                  .Append($"text-anchor=\"middle\" paint-order=\"stroke\" stroke=\"#fff\" stroke-width=\"3\">{grupo.Key}</text>");
            }

            // Escala en metros: se busca una longitud redonda que ocupe como mucho un tercio
            double metrosTotales = altoGrados * MetrosPorGrado * (anchoGrados / altoGrados);
            double objetivo = metrosTotales / 3.0;
            double[] pasos = { 5, 10, 20, 25, 50, 100, 200, 250, 500, 1000, 2000 };
            double metros = pasos.FirstOrDefault(p => p >= objetivo);
            if (metros == 0) metros = pasos[pasos.Length - 1];
            double pxEscala = metros / MetrosPorGrado * escala;
            double yEscala = alto - 8;
            sb.Append($"<line x1=\"{Margen}\" y1=\"{N(yEscala)}\" x2=\"{N(Margen + pxEscala)}\" y2=\"{N(yEscala)}\" stroke=\"#495057\" stroke-width=\"2\"/>")
              .Append($"<text x=\"{N(Margen + pxEscala + 5)}\" y=\"{N(yEscala + 4)}\" font-size=\"11\" fill=\"#495057\">{N(metros)} m</text>");

            sb.Append("</svg>");
            return sb.ToString();
        }

        private static List<(double Lat, double Lon)> Puntos(List<GeoLocParcela> parcelas) {
            var ret = new List<(double Lat, double Lon)>();
            foreach (var p in parcelas ?? new List<GeoLocParcela>())
                foreach (var anillo in Anillos(p.GEO))
                    ret.AddRange(anillo);
            return ret;
        }

        /// <summary>
        /// Anillos de coordenadas de un WKT. Se sacan los grupos de paréntesis MÁS INTERNOS,
        /// que es lo que sirve igual para POLYGON, para POLYGON con huecos y para MULTIPOLYGON
        /// sin tener que escribir un analizador de WKT completo.
        /// </summary>
        private static IEnumerable<List<(double Lat, double Lon)>> Anillos(string wkt) {
            if (string.IsNullOrWhiteSpace(wkt))
                yield break;
            foreach (Match m in Regex.Matches(wkt, @"\(([^()]*)\)")) {
                var puntos = new List<(double Lat, double Lon)>();
                foreach (var par in m.Groups[1].Value.Split(',')) {
                    var trozos = par.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (trozos.Length < 2)
                        continue;
                    // WKT va (longitud latitud), en ese orden.
                    if (double.TryParse(trozos[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double lon)
                     && double.TryParse(trozos[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double lat))
                        puntos.Add((lat, lon));
                }
                if (puntos.Count > 0)
                    yield return puntos;
            }
        }
    }
}
