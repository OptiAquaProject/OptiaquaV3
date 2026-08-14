namespace DatosOptiaqua {
    /// <summary>
    /// Colores que vienen de la base de datos, listos para meter en CSS.
    /// </summary>
    public static class Colores {
        /// <summary>Gris de Bootstrap, para cuando no hay color.</summary>
        public const string Neutro = "#6c757d";

        /// <summary>
        /// Color de un umbral de estrés tal y como se puede usar en un style.
        ///
        /// En `TipoEstresUmbral.Color` los colores están guardados SIN almohadilla
        /// ("00CD00", "EE2C2C"…). Puestos tal cual en un `style="background:00CD00"` el
        /// navegador los descarta por inválidos y el fondo se queda transparente: la insignia
        /// del estado hídrico salía en blanco sobre blanco, prácticamente invisible.
        /// </summary>
        /// <param name="color">Lo que venga de la base de datos.</param>
        /// <returns>Un color usable en CSS; el gris neutro si no hay nada.</returns>
        public static string Css(string color) {
            if (string.IsNullOrWhiteSpace(color))
                return Neutro;
            color = color.Trim();
            if (color.StartsWith("#"))
                return color;
            // Solo se le pone la almohadilla a lo que de verdad es hexadecimal; un nombre de
            // color ("red", "green") tiene que pasar tal cual.
            bool esHex = (color.Length == 3 || color.Length == 6 || color.Length == 8);
            foreach (char c in color)
                if (!Uri.IsHexDigit(c)) { esHex = false; break; }
            return esHex ? "#" + color : color;
        }

        /// <summary>
        /// Color de texto que se lee sobre el fondo indicado: negro sobre los claros y blanco
        /// sobre los oscuros. Con los amarillos ("EEEE00") el texto blanco no se ve.
        /// </summary>
        /// <param name="colorFondo">Color de fondo, con o sin almohadilla.</param>
        public static string TextoSobre(string colorFondo) {
            string css = Css(colorFondo);
            if (css.Length != 7 || !css.StartsWith("#"))
                return "#fff";
            try {
                int r = Convert.ToInt32(css.Substring(1, 2), 16);
                int g = Convert.ToInt32(css.Substring(3, 2), 16);
                int b = Convert.ToInt32(css.Substring(5, 2), 16);
                // Luminancia percibida (Rec. 601): el verde pesa mucho más que el azul.
                double luz = (0.299 * r + 0.587 * g + 0.114 * b) / 255.0;
                return luz > 0.6 ? "#212529" : "#fff";
            } catch {
                return "#fff";
            }
        }
    }
}
