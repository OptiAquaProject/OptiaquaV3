namespace DatosOptiaqua {
    using Models;
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Un ensayo de LAB-ONE: la copia completa y editable de TODO lo que entra en el balance
    /// hídrico de una unidad de cultivo en una temporada.
    ///
    /// La idea es poder tocar cualquier dato —el suelo, el clima, las etapas, los riegos, la
    /// superficie— y ver qué sale, sin escribir una sola fila en la base de datos. Por eso el
    /// ensayo se lleva dentro todo lo que el cálculo necesita: una vez cargado, recalcular no
    /// vuelve a consultar nada. Lo único que se sigue leyendo es el catálogo de tipos de estrés
    /// y sus umbrales, que es común a toda la aplicación; el ensayo elige cuál usa cada etapa,
    /// no redefine la escala.
    ///
    /// Se serializa a JSON tal cual: así es como se guarda en disco y como se comparte.
    /// </summary>
    public class LabOneEnsayo {
        /// <summary>Nombre con el que se guarda y se reconoce el ensayo.</summary>
        public string Nombre { get; set; }

        /// <summary>Para anotar qué se estaba probando. No interviene en el cálculo.</summary>
        public string Notas { get; set; }

        public DateTime Creado { get; set; }
        public DateTime Modificado { get; set; }

        /// <summary>De qué unidad de cultivo se sacaron los datos originales.</summary>
        public string IdUnidadCultivo { get; set; }
        public string IdTemporada { get; set; }

        /// <summary>Fecha para la que se pide el estado hídrico.</summary>
        public DateTime Fecha { get; set; }

        public DateTime TemporadaFechaInicial { get; set; }
        public DateTime TemporadaFechaFinal { get; set; }

        // ===== Cabecera de la unidad de cultivo =====
        public string Alias { get; set; }
        public string TipoSueloDescripcion { get; set; }
        public int IdRegante { get; set; }
        public string ReganteNombre { get; set; }
        public string ReganteNif { get; set; }
        public string Municipios { get; set; }
        public string Parajes { get; set; }

        /// <summary>Superficie en m². Es la que convierte los m³ de riego en mm.</summary>
        public double SuperficieM2 { get; set; }
        public int? NParcelas { get; set; }

        /// <summary>Caudal del sistema de riego en mm/h: de aquí sale el tiempo recomendado.</summary>
        public double Pluviometria { get; set; }

        public int IdTipoRiego { get; set; }
        public string TipoRiego { get; set; }

        /// <summary>Fracción del agua aplicada que llega a la zona de raíces (0..1).</summary>
        public double EficienciaRiego { get; set; }

        // ===== Cultivo =====
        public int IdCultivo { get; set; }
        public string CultivoNombre { get; set; }

        /// <summary>Temperatura base para la integral térmica.</summary>
        public double? TBase { get; set; }

        /// <summary>Profundidad de raíz al nacer, en metros.</summary>
        public double ProfRaizInicial { get; set; }

        /// <summary>Profundidad máxima de raíz, en metros.</summary>
        public double ProfRaizMax { get; set; }

        /// <summary>Grados-día acumulados que hacen falta para la emergencia.</summary>
        public double IntegralEmergencia { get; set; }

        // ===== Estación climática =====
        public int IdEstacion { get; set; }
        public string EstacionNombre { get; set; }

        // ===== Series y tablas =====
        public List<UnidadCultivoCultivoEtapas> Etapas { get; set; } = new List<UnidadCultivoCultivoEtapas>();
        public List<DatosSuelo> Suelo { get; set; } = new List<DatosSuelo>();
        public List<DatoClimatico> Clima { get; set; } = new List<DatoClimatico>();
        public List<Riego> Riegos { get; set; } = new List<Riego>();
        public List<UnidadCultivoDatosExtra> DatosExtra { get; set; } = new List<UnidadCultivoDatosExtra>();

        /// <summary>
        /// Saca de la base de datos el ensayo de partida de una unidad de cultivo.
        ///
        /// Se monta a partir de un <see cref="UnidadCultivoDatosHidricos"/> real y no de
        /// consultas propias, para que el punto de partida del laboratorio sea EXACTAMENTE lo
        /// que usa el cálculo de producción. Si aquí se leyera por otro camino, el ensayo podría
        /// salir distinto del original y no habría forma de saber si la diferencia la ha metido
        /// el usuario o la carga.
        /// </summary>
        /// <param name="idUnidadCultivo">Unidad de cultivo de la que copiar los datos.</param>
        /// <param name="fecha">Fecha dentro de la temporada que se quiere ensayar.</param>
        /// <returns>El ensayo listo para editar.</returns>
        public static LabOneEnsayo Cargar(string idUnidadCultivo, DateTime fecha) {
            var dh = new UnidadCultivoDatosHidricos(idUnidadCultivo, fecha);
            var ret = dh.ACopiaLab();
            ret.Fecha = fecha.Date;
            ret.Nombre = idUnidadCultivo + " " + ret.IdTemporada;
            ret.Creado = DateTime.Now;
            ret.Modificado = ret.Creado;
            return ret;
        }

        /// <summary>Primer día del periodo de estudio: el inicio de la etapa 1.</summary>
        public DateTime FechaSiembra =>
            Etapas != null && Etapas.Count > 0 ? Etapas[0].FechaInicioEtapa : Fecha;

        /// <summary>Profundidad del suelo medida, en cm. 0 si no hay suelo.</summary>
        public double SueloProfundidadCM {
            get {
                double ret = 0;
                if (Suelo != null)
                    foreach (var h in Suelo)
                        if (h.ProfundidadCM > ret) ret = h.ProfundidadCM;
                return ret;
            }
        }
    }

    /// <summary>
    /// Lo que se enseña en la pantalla de un ensayo: los datos editables y lo que ha salido de
    /// recalcular con ellos.
    ///
    /// El resultado va aparte del ensayo y no dentro, porque no forma parte de él: el ensayo es
    /// la entrada —lo que se guarda en disco y se comparte—, y esto es lo que produce en esta
    /// pasada. Mezclarlos llevaría a guardar en el JSON números que dejan de valer en cuanto se
    /// cambia un dato.
    /// </summary>
    public class LabOneFicha {
        public LabOneEnsayo Ensayo { get; set; }

        /// <summary>El estado hídrico que ha salido, o null si el cálculo no llegó a término.</summary>
        public DatosEstadoHidrico Estado { get; set; }

        /// <summary>Por qué no hay resultado, si es el caso.</summary>
        public string ErrorCalculo { get; set; }

        /// <summary>Las líneas del balance, una por día.</summary>
        public List<LineaBalance> Lineas { get; set; } = new List<LineaBalance>();

        /// <summary>Lo que el cálculo ha tenido que sortear para llegar hasta aquí.</summary>
        public List<Incidencia> Incidencias { get; set; } = new List<Incidencia>();

        /// <summary>Lo que ha tardado el recálculo, para saber si conviene tocar de uno en uno.</summary>
        public long Milisegundos { get; set; }

        /// <summary>Tipos de estrés disponibles, para el desplegable de cada etapa.</summary>
        public List<string> TiposEstres { get; set; } = new List<string>();

        /// <summary>Si alguna incidencia ha impedido el cálculo.</summary>
        public bool HayError {
            get {
                if (Estado == null) return true;
                foreach (var i in Incidencias)
                    if (i.Gravedad == GravedadIncidencia.Error) return true;
                return false;
            }
        }
    }
}
