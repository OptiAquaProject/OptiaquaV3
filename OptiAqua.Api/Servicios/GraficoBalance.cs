namespace DatosOptiaqua {
    using Models;
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// Dibuja la evolución del balance hídrico como un SVG incrustado en la página.
    ///
    /// SVG a mano y no una biblioteca de gráficos a propósito: el proyecto no carga ningún
    /// JavaScript de terceros —bootstrap y jQuery van servidos desde wwwroot— y añadir uno
    /// solo para esta pantalla obligaría a mantenerlo y a que el navegador salga a Internet.
    /// El gráfico es estático, así que el servidor lo puede dejar dibujado.
    /// </summary>
    public static class GraficoBalance {
        private const int Ancho = 1100;
        private const int Alto = 340;
        private const int MargenIzq = 55;
        private const int MargenDer = 15;
        private const int MargenSup = 15;
        private const int AlturaAportes = 70;   // franja de abajo para lluvia y riego
        private const int MargenInf = 30;

        private static string N(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

        /// <summary>
        /// Gráfico del agua en el suelo a lo largo de la campaña, con los aportes debajo.
        ///
        /// Arriba, en milímetros: capacidad de campo y punto de marchitez como referencias,
        /// el umbral de riego, y el agua que hay en el suelo cada día. Abajo, en barras, la
        /// lluvia y el riego. Es la lectura agronómica de un vistazo: si la curva del agua
        /// baja del umbral, tocaba regar.
        /// </summary>
        /// <param name="lineas">Las líneas del balance, en orden de fecha.</param>
        /// <returns>El SVG listo para incrustar, o un aviso si no hay datos.</returns>
        public static string Svg(List<LineaBalance> lineas) {
            if (lineas == null || lineas.Count < 2)
                return "<p class=\"text-muted\">No hay balance suficiente para dibujar la evolución.</p>";

            var puntos = lineas.Where(x => x.Fecha != null).OrderBy(x => x.Fecha).ToList();
            if (puntos.Count < 2)
                return "<p class=\"text-muted\">No hay balance suficiente para dibujar la evolución.</p>";

            double maxY = puntos.Max(p => Math.Max(p.CapacidadCampo, p.ContenidoAguaSuelo));
            if (maxY <= 0) maxY = 1;
            maxY *= 1.05;

            int altoSerie = Alto - MargenSup - AlturaAportes - MargenInf;
            double anchoUtil = Ancho - MargenIzq - MargenDer;
            double paso = anchoUtil / (puntos.Count - 1);

            double X(int i) => MargenIzq + i * paso;
            double Y(double mm) => MargenSup + altoSerie - (mm / maxY) * altoSerie;

            var sb = new StringBuilder();
            sb.Append($"<svg viewBox=\"0 0 {Ancho} {Alto}\" width=\"100%\" height=\"{Alto}\" role=\"img\" ")
              .Append("aria-label=\"Evolución del agua en el suelo\" style=\"max-width:100%\">");

            // Rejilla horizontal y escala en mm
            int lineasRejilla = 4;
            for (int i = 0; i <= lineasRejilla; i++) {
                double mm = maxY * i / lineasRejilla;
                double y = Y(mm);
                sb.Append($"<line x1=\"{MargenIzq}\" y1=\"{N(y)}\" x2=\"{Ancho - MargenDer}\" y2=\"{N(y)}\" stroke=\"#e9ecef\" stroke-width=\"1\"/>");
                sb.Append($"<text x=\"{MargenIzq - 6}\" y=\"{N(y + 4)}\" font-size=\"11\" fill=\"#6c757d\" text-anchor=\"end\">{N(Math.Round(mm))}</text>");
            }
            sb.Append($"<text x=\"6\" y=\"{MargenSup + 10}\" font-size=\"11\" fill=\"#6c757d\">mm</text>");

            // Series de referencia y el agua del suelo
            sb.Append(Linea(puntos, i => puntos[i].CapacidadCampo, X, Y, "#adb5bd", "4 3"));
            sb.Append(Linea(puntos, i => puntos[i].PuntoMarchitez, X, Y, "#adb5bd", "4 3"));
            sb.Append(Linea(puntos, i => puntos[i].LimiteAgotamiento, X, Y, "#fd7e14", null));
            sb.Append(Linea(puntos, i => puntos[i].ContenidoAguaSuelo, X, Y, "#0d6efd", null, 2.2));

            // Aportes: lluvia y riego, en barras, con su propia escala
            double baseAportes = Alto - MargenInf;
            double maxAporte = puntos.Max(p => Math.Max(p.Lluvia, p.Riego));
            if (maxAporte <= 0) maxAporte = 1;
            double anchoBarra = Math.Max(1.0, paso * 0.7);
            for (int i = 0; i < puntos.Count; i++) {
                var p = puntos[i];
                if (p.Lluvia > 0) {
                    double h = (p.Lluvia / maxAporte) * (AlturaAportes - 6);
                    sb.Append($"<rect x=\"{N(X(i) - anchoBarra / 2)}\" y=\"{N(baseAportes - h)}\" width=\"{N(anchoBarra)}\" height=\"{N(h)}\" fill=\"#4dabf7\"><title>{p.Fecha:dd/MM/yyyy} · lluvia {N(p.Lluvia)} mm</title></rect>");
                }
                if (p.Riego > 0) {
                    double h = (p.Riego / maxAporte) * (AlturaAportes - 6);
                    sb.Append($"<rect x=\"{N(X(i) - anchoBarra / 2)}\" y=\"{N(baseAportes - h)}\" width=\"{N(anchoBarra)}\" height=\"{N(h)}\" fill=\"#20c997\" opacity=\"0.85\"><title>{p.Fecha:dd/MM/yyyy} · riego {N(p.Riego)} mm</title></rect>");
                }
            }
            sb.Append($"<line x1=\"{MargenIzq}\" y1=\"{N(baseAportes)}\" x2=\"{Ancho - MargenDer}\" y2=\"{N(baseAportes)}\" stroke=\"#dee2e6\"/>");

            // Meses en el eje: una marca por cada cambio de mes, que con 300 días no cabe más
            for (int i = 1; i < puntos.Count; i++) {
                if (puntos[i].Fecha.Value.Month == puntos[i - 1].Fecha.Value.Month)
                    continue;
                sb.Append($"<line x1=\"{N(X(i))}\" y1=\"{MargenSup}\" x2=\"{N(X(i))}\" y2=\"{N(baseAportes)}\" stroke=\"#f1f3f5\"/>");
                sb.Append($"<text x=\"{N(X(i))}\" y=\"{Alto - 10}\" font-size=\"11\" fill=\"#6c757d\" text-anchor=\"middle\">{puntos[i].Fecha:MMM yy}</text>");
            }

            sb.Append("</svg>");
            return sb.ToString();
        }

        private static string Linea(List<LineaBalance> puntos, Func<int, double> valor,
                                    Func<int, double> X, Func<double, double> Y,
                                    string color, string discontinua, double grosor = 1.4) {
            var sb = new StringBuilder("<polyline fill=\"none\" stroke=\"").Append(color)
                .Append("\" stroke-width=\"").Append(N(grosor)).Append('"');
            if (discontinua != null)
                sb.Append(" stroke-dasharray=\"").Append(discontinua).Append('"');
            sb.Append(" points=\"");
            for (int i = 0; i < puntos.Count; i++)
                sb.Append(N(X(i))).Append(',').Append(N(Y(valor(i)))).Append(' ');
            return sb.Append("\"/>").ToString();
        }
    }
}
