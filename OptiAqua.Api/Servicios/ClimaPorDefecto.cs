namespace DatosOptiaqua {
    using System;

    /// <summary>
    /// Valores climáticos por defecto, por mes, para cuando falta el dato de la estación.
    ///
    /// POR QUÉ: `ValorClimaticoConRespaldo` estima el día que falta con la media de los tres
    /// anteriores, pero si tampoco hay ninguno de esos devolvía CERO. Y un ETo de 0 no es un
    /// día sin demanda: es un balance corriendo a ciegas. El suelo no se seca nunca, el
    /// agotamiento se queda en 0, el contenido de agua se queda clavado en capacidad de campo
    /// y sale una ficha de aspecto impecable que no significa nada. Medido sobre la base real:
    /// 151 de 1.264 unidades de cultivo estaban así, todas de temporadas anteriores a que su
    /// estación empezara a publicar (la 503 y la 510 arrancan el 19/11/2020).
    ///
    /// DE DÓNDE SALEN LOS NÚMEROS: media por mes de la propia tabla DatoClimatico —21 años,
    /// del 01/01/2005 al 12/08/2026, 23 estaciones, 57.727 días tras descartar los valores
    /// imposibles (ETo ≤ 0 o > 15, humedad fuera de 0-100, etc.)—. La dispersión entre
    /// estaciones es del 8-15% según el mes, y las tres estaciones que hoy usan las parcelas
    /// tienen medias anuales de 2,99 / 3,05 / 3,12 mm/día: por eso la tabla es única y no una
    /// por estación.
    ///
    /// LA LLUVIA NO SE ESTIMA, Y ES A PROPÓSITO. La media de precipitación sale entre 0,6 y
    /// 1,9 mm/día, pero repartirla por igual todos los días metería en el balance un agua que
    /// no ha caído y rebajaría el riego recomendado. Cuando falta el dato, la lluvia es 0:
    /// equivocarse recomendando regar de más es recuperable; recomendar de menos, no.
    /// </summary>
    public static class ClimaPorDefecto {
        // Índice 0 = enero … 11 = diciembre.
        private static readonly double[] eto =      { 1.00, 1.50, 2.24, 3.26, 4.10, 5.11, 5.84, 5.18, 3.34, 2.16, 1.16, 0.75 };
        private static readonly double[] temperatura = { 5.38, 7.31, 9.07, 12.03, 15.16, 20.11, 22.19, 22.17, 17.91, 14.80, 9.35, 6.38 };
        private static readonly double[] humedad =  { 62.61, 61.61, 58.25, 52.82, 50.63, 46.73, 42.79, 41.28, 49.04, 54.57, 65.05, 72.25 };
        private static readonly double[] viento =   { 2.45, 2.43, 2.70, 2.35, 2.00, 1.86, 2.04, 1.96, 1.86, 1.86, 2.24, 2.14 };

        /// <summary>Evapotranspiración de referencia media del mes, en mm/día.</summary>
        /// <param name="mes">Mes del año, de 1 a 12.</param>
        public static double Eto(int mes) => eto[Indice(mes)];

        /// <summary>Humedad relativa media del mes, en %.</summary>
        /// <param name="mes">Mes del año, de 1 a 12.</param>
        public static double Humedad(int mes) => humedad[Indice(mes)];

        /// <summary>
        /// Lluvia por defecto: SIEMPRE 0. Está aquí para dejar constancia de que es una
        /// decisión, no un olvido. Ver el comentario de la clase.
        /// </summary>
        /// <param name="mes">Mes del año, de 1 a 12. No se usa.</param>
        public static double Lluvia(int mes) => 0;

        /// <summary>Temperatura media del mes, en °C.</summary>
        /// <param name="mes">Mes del año, de 1 a 12.</param>
        public static double Temperatura(int mes) => temperatura[Indice(mes)];

        /// <summary>Velocidad media del viento del mes, en m/s.</summary>
        /// <param name="mes">Mes del año, de 1 a 12.</param>
        public static double Viento(int mes) => viento[Indice(mes)];

        private static int Indice(int mes) {
            if (mes < 1 || mes > 12)
                throw new ArgumentOutOfRangeException(nameof(mes), mes, "El mes va de 1 a 12.");
            return mes - 1;
        }
    }
}
