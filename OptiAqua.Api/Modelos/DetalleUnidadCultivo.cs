namespace DatosOptiaqua {
    using Models;
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Todo lo que se enseña de una unidad de cultivo en su pantalla de detalle.
    ///
    /// Se monta de una sola vez en el controlador, y cada bloque se rellena por separado con
    /// su propio control de errores: si una parte falla —por ejemplo una unidad sin suelo—,
    /// esa tabla sale vacía con su motivo y el resto de la pantalla se sigue viendo.
    /// </summary>
    public class DetalleUnidadCultivo {
        public string IdUnidadCultivo { get; set; }
        public string IdTemporada { get; set; }

        /// <summary>Fecha para la que se ha pedido el estado.</summary>
        public DateTime Fecha { get; set; }

        /// <summary>El estado hídrico, el mismo que sale en la lista. null si no se pudo.</summary>
        public DatosEstadoHidrico Estado { get; set; }

        /// <summary>Motivo por el que no hay estado, si es el caso.</summary>
        public string ErrorEstado { get; set; }

        public int IdEstacion { get; set; }
        public string EstacionNombre { get; set; }

        public List<DatosSuelo> Suelo { get; } = new List<DatosSuelo>();
        public string ErrorSuelo { get; set; }

        public List<UnidadCultivoCultivoEtapas> Etapas { get; } = new List<UnidadCultivoCultivoEtapas>();
        public string ErrorEtapas { get; set; }

        public List<Riego> Riegos { get; } = new List<Riego>();
        public string ErrorRiegos { get; set; }

        public List<UnidadCultivoDatosExtra> DatosExtra { get; } = new List<UnidadCultivoDatosExtra>();
        public string ErrorDatosExtra { get; set; }

        /// <summary>Parcelas con su geometría en WKT, para dibujar el plano.</summary>
        public List<GeoLocParcela> Parcelas { get; } = new List<GeoLocParcela>();
        public string ErrorParcelas { get; set; }

        /// <summary>
        /// Las líneas del balance. Alimentan el gráfico y la tabla de lluvias: así lo que se
        /// enseña es EXACTAMENTE lo que ha usado el cálculo, no una consulta paralela que
        /// podría no coincidir.
        /// </summary>
        public List<LineaBalance> Lineas { get; } = new List<LineaBalance>();
        public string ErrorBalance { get; set; }

        /// <summary>Días del balance en los que llovió, para la tabla de lluvias.</summary>
        public IEnumerable<LineaBalance> DiasConLluvia {
            get {
                foreach (var l in Lineas)
                    if (l.Lluvia > 0)
                        yield return l;
            }
        }

        /// <summary>Suma de la lluvia de todo el periodo, en mm.</summary>
        public double LluviaTotal {
            get {
                double ret = 0;
                foreach (var l in Lineas) ret += l.Lluvia;
                return ret;
            }
        }

        /// <summary>Suma del riego de todo el periodo, en mm.</summary>
        public double RiegoTotal {
            get {
                double ret = 0;
                foreach (var l in Lineas) ret += l.Riego;
                return ret;
            }
        }
    }
}
