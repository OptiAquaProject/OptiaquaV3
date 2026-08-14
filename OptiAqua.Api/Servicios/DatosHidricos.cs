namespace DatosOptiaqua {
    using Models;
    using System;
    using System.Collections.Generic;
    using Utiles;

    /// <summary>
    /// Define <see cref="UnidadCultivoDatosHidricos" />
    /// UnidadCultivoDatosHidricos es una clase que almacena y proporciona la mayoria de los datos necesarios para el calculo del balance hídrico de una unidad de cultivo en una temporada.
    /// Encapsula los datos necesarios para los cálculos del balance hídrico.
    /// </summary>
    public class UnidadCultivoDatosHidricos {
        /// <summary>
        /// Defines the lDatosClimaticos.
        /// </summary>
        private List<DatoClimatico> lDatosClimaticos;

        /// <summary>
        /// Defines the lTiposEstres.
        /// </summary>
        private readonly Dictionary<string, TipoEstres> lTiposEstres;

        /// <summary>
        /// Defines the lTipoEstresUmbralList.
        /// </summary>
        private readonly Dictionary<string, List<TipoEstresUmbral>> lTipoEstresUmbralList;

        /// <summary>
        /// Defines the unidadCultivo.
        /// </summary>
        private UnidadCultivo unidadCultivo;

        /// <summary>
        /// Defines the pUnidadCultivoExtensionM2.
        /// </summary>
        private readonly double pUnidadCultivoExtensionM2;

        /// <summary>
        /// Defines the cultivo.
        /// </summary>
        private Cultivo cultivo;

        /// <summary>
        /// Defines the temporada.
        /// </summary>
        private Temporada temporada;

        /// <summary>
        /// Defines the riegoTipo.
        /// </summary>
        private RiegoTipo riegoTipo;

        /// <summary>
        /// Defines the regante.
        /// </summary>
        private Regante regante;

        /// <summary>
        /// Define pUnidadCultivoCultivo.
        /// </summary>
        private UnidadCultivoCultivo unidadCultivoCultivo;

        /// <summary>
        /// Define pUnidadCultivoDatosExtas.
        /// </summary>
        private List<UnidadCultivoDatosExtra> lUnidadCultivoDatosExtas;

        /// <summary>
        /// Define pDatosRiego.
        /// </summary>
        private readonly List<Riego> lDatosRiego;

        /// <summary>
        /// Gets the EficienciaRiego.
        /// </summary>
        public double EficienciaRiego => riegoTipo.Eficiencia;

        /// <summary>
        /// Gets the Alias.
        /// </summary>
        public string Alias => unidadCultivo.Alias;

        /// <summary>
        /// Gets the IdCultivo.
        /// </summary>
        public int? IdCultivo => cultivo.IdCultivo;

        /// <summary>
        /// Gets the UnidadCultivoExtensionM2.
        /// </summary>
        public double? UnidadCultivoExtensionM2 => DB.UnidadCultivoExtensionM2(unidadCultivo.IdUnidadCultivo, temporada.IdTemporada);

        /// <summary>
        /// Gets the IdTemporada.
        /// </summary>
        public string IdTemporada => temporada.IdTemporada;

        /// <summary>
        /// Gets the IdTipoRiego.
        /// </summary>
        public int? IdTipoRiego => riegoTipo.IdTipoRiego;

        /// <summary>
        /// Gets the NParcelas.
        /// </summary>
        public int? NParcelas => DB.NParcelas(unidadCultivo.IdUnidadCultivo, temporada.IdTemporada);

        /// <summary>
        /// Gets the ReganteNombre.
        /// </summary>
        public string ReganteNombre => regante.Nombre;

        /// <summary>
        /// Gets the ReganteNif.
        /// </summary>
        public string ReganteNif => regante.NIF;

        /// <summary>
        /// Gets the ReganteTelefono.
        /// </summary>
        public string ReganteTelefono => regante.Telefono;

        /// <summary>
        /// Gets the ReganteTelefonoSMS.
        /// </summary>
        public string ReganteTelefonoSMS => regante.TelefonoSMS;

        /// <summary>
        /// Gets the Pluviometria.
        /// </summary>
        public double Pluviometria => unidadCultivoCultivo.Pluviometria;

        /// <summary>
        /// Gets the TipoRiego.
        /// </summary>
        public string TipoRiego => riegoTipo.TipoRiego;

        /// <summary>
        /// Gets the UnidadCultivoCultivoEtapasList.
        /// </summary>
        public List<UnidadCultivoCultivoEtapas> UnidadCultivoCultivoEtapasList { get; private set; }

        public ParametrosEtapasCalculos ParametrosEtapas { get; private set; }

        /// <summary>
        /// Retorna primera fecha de las etapas como fecha de siembra.
        /// En caso de que no existandatos retorna fecha actual.
        /// </summary>
        /// <returns>.</returns>
        public DateTime FechaSiembra() => (DateTime)unidadCultivoCultivo.FechaSiembra();

        /// <summary>
        /// Gets the CultivoNombre.
        /// </summary>
        public string CultivoNombre => cultivo.Nombre;

        /// <summary>
        /// Gets the IdUnidadCultivo.
        /// </summary>
        public string IdUnidadCultivo => unidadCultivo.IdUnidadCultivo;


        /// <summary>
        /// Gets the IdRegante.
        /// </summary>
        public int? IdRegante => unidadCultivoCultivo.IdRegante;

        /// <summary>
        /// Gets the IdEstacion.
        /// </summary>
        public int IdEstacion => DB.EstacionDeUC(unidadCultivo.IdUnidadCultivo, IdTemporada);

        /// <summary>
        /// Gets the TipoSueloDescripcion.
        /// </summary>
        public string TipoSueloDescripcion => unidadCultivo.TipoSueloDescripcion;

        /// <summary>
        /// Gets the nEtapas.
        /// </summary>
        public int nEtapas => UnidadCultivoCultivoEtapasList.Count;

        /// <summary>
        /// Gets the CultivoTBase.
        /// </summary>
        public double? CultivoTBase => cultivo.TBase;

        /// <summary>
        /// Gets the CultivoIntegralEmergencia.
        /// </summary>
        public double CultivoIntegralEmergencia => cultivo.IntegralEmergencia;


        /// <summary>
        /// Gets the CultivoProfRaizInicial.
        /// </summary>
        public double CultivoProfRaizInicial => cultivo.ProfRaizInicial;

        /// <summary>
        /// Gets the CultivoProfRaizMax.
        /// </summary>
        public double CultivoProfRaizMax => cultivo.ProfRaizMax;

        /// <summary>
        /// Gets the ListaUcSuelo.
        /// </summary>
        public List<DatosSuelo> lUCSuelo { get; private set; }

        /// <summary>
        /// Retorna los dias que tarda en crecer completamente la raiz (se estima la suma de las dos primeras etapas)
        /// </summary>
        public double DiasCrecimientoRaiz {
            get {
                double ret = 0;
                if (UnidadCultivoCultivoEtapasList.Count > 0)
                    ret += UnidadCultivoCultivoEtapasList[0].DuracionDiasEtapa;
                if (UnidadCultivoCultivoEtapasList.Count > 1)
                    ret += UnidadCultivoCultivoEtapasList[1].DuracionDiasEtapa;
                return ret;
            }
        }

        // Devuelve los límites inferior y superior para la etapa indicada.
        // de la etapa se optiene el tipo de estres (con idUmbralInferior Riego y idUmbralSuperiorRiego)
        // Los valores de los límite inferior y superior se obtienen de la lista de umbrales. Buscando por identificador.
        /// <summary>
        /// The ClaseEstresUmbralInferiorYSuperior.
        /// </summary>
        /// <param name="nEtapa">The nEtapa<see cref="int"/>.</param>
        /// <param name="limiteInferior">The limiteInferior<see cref="double"/>.</param>
        /// <param name="limiteSuperior">The limiteSuperior<see cref="double"/>.</param>
        public void ClaseEstresUmbralInferiorYSuperior(int nEtapa, out double limiteInferior, out double limiteSuperior) {
            int nEtapaBase0 = nEtapa - 1 > 0 ? nEtapa - 1 : 0;
            string idTipoEstres = UnidadCultivoCultivoEtapasList[nEtapaBase0].IdTipoEstres;
            if (idTipoEstres == null)
                throw new Exception("No se ha definido tipo de estrés");
            TipoEstres estres = lTiposEstres[idTipoEstres];
            int? idInferior = estres.IdUmbralInferiorRiego;
            int? idSuperior = estres.IdUmbralSuperiorRiego;
            List<TipoEstresUmbral> lUmbrales = lTipoEstresUmbralList[idTipoEstres];
            lUmbrales = lTipoEstresUmbralList[idTipoEstres];

            if (idInferior == null)
                throw new Exception("No se ha definido IdUmbraInferior para el tipo de estrés: " + idTipoEstres);
            if (idSuperior == null)
                throw new Exception("No se ha definido IdUmbraSuperior para el tipo de estrés: " + idTipoEstres);
            double? valInferior = lUmbrales.Find(x => x.IdUmbral == idInferior)?.UmbralMaximo;
            double? valSuperior = lUmbrales.Find(x => x.IdUmbral == idSuperior)?.UmbralMaximo;
            if (valInferior == null || valSuperior == null) {
                throw new Exception("Umbrales para el tipo de estes mal definidos: " + idTipoEstres);
            }
            limiteInferior = (double)valInferior;
            limiteSuperior = (double)valSuperior;
        }

        /// <summary>
        /// Retorna a la tabla de umbrales la clase de estrés.
        /// Ordena la tabla por el valor umbral.
        /// Retonar la descripción del maxímo umbral que puede superar el indiceEstres.
        /// </summary>
        /// <param name="idTipoEstres">idTipoEstres<see cref="string"/>.</param>
        /// <param name="indiceEstres">ie<see cref="double"/>.</param>
        /// <returns><see cref="string"/>.</returns>
        public TipoEstresUmbral TipoEstresUmbral(string idTipoEstres, double indiceEstres) {
            TipoEstresUmbral ret = null;
            List<TipoEstresUmbral> ltu = lTipoEstresUmbralList[idTipoEstres];
            if (ltu?.Count == 0)
                return ret;
            ret = ltu[0];
            int i = 0;
            while (indiceEstres > ltu[i].UmbralMaximo && (i + 1 < ltu.Count)) {
                ret = ltu[++i];
            }
            return ret;
        }
        
        /// <summary>
        /// Initializes a new instance of the <see cref="UnidadCultivoDatosHidricos"/> class.
        /// </summary>
        /// <param name="idUnidadCultivo">The IdUnidadCultivo<see cref="string"/>.</param>
        /// <param name="fecha">The idTemporada<see cref="string"/>.</param>
        public UnidadCultivoDatosHidricos(string idUnidadCultivo, DateTime fecha) {

            string idTemporada = DB.TemporadaDeFecha(idUnidadCultivo, fecha);
            if ((temporada = DB.Temporada(idTemporada)) == null)
                throw new Exception($"Imposible cargar datos de la temporada {idTemporada}.");

            if ((unidadCultivo = DB.UnidadCultivo(idUnidadCultivo)) == null)
                throw new Exception($"Imposible cargar datos del cultivo {idUnidadCultivo}.");

            pUnidadCultivoExtensionM2 = DB.UnidadCultivoExtensionM2(idUnidadCultivo, idTemporada);
            if (pUnidadCultivoExtensionM2 == 0) {
                throw new Exception($"La superficie de la unidad  de cultivo '{IdUnidadCultivo}' es 0.");
            }
            if ((UnidadCultivoCultivoEtapasList = DB.UnidadCultivoCultivoEtapasList(idUnidadCultivo, idTemporada)).Count == 0)
                throw new Exception($"Imposible cargar las etapas para la unidad de cultivo {idUnidadCultivo}.");

            ParametrosEtapas = DeserializaParamtros(UnidadCultivoCultivoEtapasList);

            if ((unidadCultivoCultivo = DB.UnidadCultivoCultivo(idUnidadCultivo, idTemporada)) == null)
                throw new Exception("Imposible datos del cultivo en la temporada indicada.");

            int idEstacion = DB.EstacionDeUC(idUnidadCultivo, idTemporada);

            lTiposEstres = DB.ListaTipoEstres();
            lTipoEstresUmbralList = DB.ListaEstresUmbral();

            if ((lDatosClimaticos = DB.DatosClimaticosList(FechaSiembra(), FechaFinalDeEstudio(), idEstacion)) == null)
                throw new Exception($"Imposible cargar datos climáticos para la estación {idEstacion}  en el intervalo de fechas de {FechaSiembra()} a {FechaFinalDeEstudio()}");

            cultivo = DB.Cultivo(unidadCultivoCultivo.IdCultivo);

            regante = (Regante)DB.Regante(unidadCultivoCultivo.IdRegante);

            lUCSuelo = DB.SueloUnidadCultivoTemporada(IdUnidadCultivo, IdTemporada);

            if (lUCSuelo == null || lUCSuelo.Count == 0)
                throw new Exception("No se ha definido suelo para la unidad de Cultivo:" + idUnidadCultivo);

            // La raíz se mide en METROS y el suelo en centímetros. Si la raíz del cultivo
            // llega más hondo de lo que se ha medido el suelo, el agua disponible se calcula
            // solo hasta donde hay dato: no se extrapola. Se avisa porque cambia el resultado
            // —en viña llegó a ser un 29% menos de agua disponible— y no es un fallo, es una
            // decisión: el suelo por debajo de lo medido no se inventa.
            double sueloCm = lUCSuelo.Max(x => x.ProfundidadCM);
            double raizCm = CultivoProfRaizMax * 100.0;
            if (raizCm > sueloCm + 0.5)
                Incidencias.Añade("RAIZ_SUPERA_SUELO", GravedadIncidencia.Aviso,
                    $"La raíz del cultivo llega a {raizCm:N0} cm y el suelo está medido hasta " +
                    $"{sueloCm:N0} cm: el agua disponible se calcula solo hasta esa profundidad.");

            riegoTipo = DB.RiegoTipo(unidadCultivoCultivo.IdTipoRiego);

            DateTime fechaSiembra = FechaSiembra();
            DateTime fechaFinal = FechaFinalDeEstudio(); ;
            lDatosRiego = DB.RiegosList(idUnidadCultivo, fechaSiembra, fechaFinal);

            lUnidadCultivoDatosExtas = DB.ParcelasDatosExtrasList(idUnidadCultivo, fechaSiembra, fechaFinal);
        }

        internal double? ParamGet(string parametro, int etapaBase0) {
            // El índice válido llega hasta Count-1: la guarda tenía que ser '>', no '>='.
            // Con '>=' y etapaBase0==Count se accedía a ParametrosEtapas[Count] -> excepción.
            if (ParametrosEtapas.Count > etapaBase0 && ParametrosEtapas[etapaBase0].TryGetValue(parametro, out var valor))
                return valor;
            // Quien llama sustituye el hueco por double.MaxValue, y en una exponencial eso da
            // 0 sin protestar: la curva de crecimiento se queda plana y nadie se entera. Que
            // al menos quede dicho de qué etapa y qué parámetro se trata.
            string etapa = etapaBase0 >= 0 && etapaBase0 < UnidadCultivoCultivoEtapasList.Count
                ? UnidadCultivoCultivoEtapasList[etapaBase0].Etapa
                : "nº " + (etapaBase0 + 1);
            // Aviso y no Error: el cálculo sale adelante, con peor apoyo. Error se reserva
            // para cuando NO hay resultado, que es lo único que se marca en rojo.
            Incidencias.Añade("PARAMETRO_AUSENTE", GravedadIncidencia.Aviso,
                $"Falta el parámetro '{parametro}' en la etapa '{etapa}': el crecimiento de esa " +
                "etapa se calcula sin él y el resultado puede no tener sentido.");
            return null;
        }

        private ParametrosEtapasCalculos DeserializaParamtros(List<UnidadCultivoCultivoEtapas> unidadCultivoCultivoEtapasList) {
            var ret = new ParametrosEtapasCalculos();
            foreach (var ucce in unidadCultivoCultivoEtapasList) {
                if (ucce.ParametrosJson == null) {
                    ret.Add(new Dictionary<string, double>());
                } else {
                    try {
                        var d = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, double>>(ucce.ParametrosJson);
                        ret.Add(d);
                    } catch {
                        throw new Exception($"Error en valores json. UC:{ucce.IdUnidadCultivo}   etapa:{ucce.IdEtapaCultivo}");
                    }
                }
            }
            return ret;
        }

        /// <summary>
        /// Retorna los datos extra a una fecha.  0 Si no se dispone de datos en esa fecha.
        /// </summary>
        /// <param name="fecha">.</param>
        /// <returns>.</returns>
        public UnidadCultivoDatosExtra DatoExtra(DateTime fecha) => lUnidadCultivoDatosExtas.Find(d => d.Fecha == fecha);

        /// <summary>
        /// ObtenerMunicicioParaje.
        /// </summary>
        /// <param name="provincias">.</param>
        /// <param name="municipios">The municipios<see cref="string"/>.</param>
        /// <param name="parajes">The parajes<see cref="string"/>.</param>
        public void ObtenerMunicicioParaje(out string provincias, out string municipios, out string parajes) => DB.ObtenerMunicicioParaje(temporada.IdTemporada, unidadCultivo.IdUnidadCultivo, out provincias, out municipios, out parajes);

        /// <summary>
        /// Retonar el cálculo de la fecha de fin de estudio.
        /// Si se excede de fecha del día retornar la fecha del día.
        /// Si no hay datos suficientes para el cálculo retorna fecha del día.
        /// </summary>
        /// <returns>Fecha din del estudio.</returns>
        public DateTime FechaFinalDeEstudio() {
            DateTime ret = temporada.FechaFinal;

            int duracionDias = 0;
            if (UnidadCultivoCultivoEtapasList != null && UnidadCultivoCultivoEtapasList.Count > 0)
                duracionDias = UnidadCultivoCultivoEtapasList[UnidadCultivoCultivoEtapasList.Count - 1].DuracionDiasEtapa;

            if (UnidadCultivoCultivoEtapasList == null)
                return ret;
            if (UnidadCultivoCultivoEtapasList.Count == 0)
                return ret;
            DateTime fechaInicioUltimaEtapa = UnidadCultivoCultivoEtapasList[UnidadCultivoCultivoEtapasList.Count - 1].FechaInicioEtapa;

            if (UnidadCultivoCultivoEtapasList[UnidadCultivoCultivoEtapasList.Count - 1].FechaInicioEtapaConfirmada != null)
                fechaInicioUltimaEtapa = (DateTime)UnidadCultivoCultivoEtapasList[UnidadCultivoCultivoEtapasList.Count - 1].FechaInicioEtapaConfirmada;
            fechaInicioUltimaEtapa = fechaInicioUltimaEtapa.AddDays(duracionDias);
            ret = fechaInicioUltimaEtapa;
            if (ret > DateTime.Today) {
                ret = DateTime.Today;
            }
            return ret;
        }

        /// <summary>
        /// Retorna los datos de riego a una fecha. 0 Si no se dispone de datos en esa fecha.
        /// Primero consulta en la tabla datos extra.
        /// </summary>
        /// <param name="fecha">.</param>
        /// <returns>.</returns>
        private double RiegoM3(DateTime fecha) {
            UnidadCultivoDatosExtra extra = DatoExtra(fecha);
            if (extra?.RiegoM3 != null) {
                return extra.RiegoM3 ?? 0;
            } else
                return (lDatosRiego?.Find(x => x.Fecha == fecha)?.RiegoM3) ?? 0;
        }

        /// <summary>
        /// RiegoMm.
        /// </summary>
        /// <param name="fecha">The fecha<see cref="DateTime"/>.</param>
        /// <returns>The <see cref="double"/>.</returns>
        public double RiegoMm(DateTime fecha) {
            double r3 = RiegoM3(fecha);
            return (r3 * 1000 / pUnidadCultivoExtensionM2);
        }

        /// <summary>
        /// Días en los que ha habido que recurrir a las medias del mes por no tener ni el dato
        /// del día ni ninguno de los tres anteriores. Lo publica <see cref="DiasSinClima"/>
        /// para que el estado hídrico pueda advertirlo en vez de callárselo.
        /// </summary>
        private readonly HashSet<DateTime> diasSinClima = new HashSet<DateTime>();

        /// <summary>Días del balance estimados con las medias del mes, si los hay.</summary>
        public IReadOnlyCollection<DateTime> DiasSinClima => diasSinClima;

        /// <summary>
        /// Lo que ha ido pasando durante el cálculo y merece contarse: días estimados, datos
        /// que faltan, límites que se han tenido que aplicar. Se vuelca en el estado hídrico.
        /// </summary>
        public RegistroIncidencias Incidencias { get; } = new RegistroIncidencias();

        /// <summary>
        /// Valor de una variable climática a una fecha, con dos respaldos encadenados:
        /// el promedio de los tres días anteriores QUE TENGAN dato y, si tampoco hay ninguno,
        /// la media del mes de <see cref="ClimaPorDefecto"/>.
        ///
        /// Antes cada variable duplicaba este bloque y, salvo la temperatura, todas caían por
        /// error en TempMedia: cuando faltaba ETo, viento o humedad, se rellenaban con la
        /// temperatura (mm/día, m/s o % sustituidos por °C). Con un único helper parametrizado
        /// por el selector del campo, el respaldo usa siempre la MISMA magnitud y el fallo no
        /// puede reaparecer al tocar una de ellas.
        ///
        /// El segundo respaldo se añadió porque devolver 0 dejaba el balance corriendo a
        /// ciegas —ETo 0 es "el cultivo no gasta agua", no "no sé cuánto gasta"— y el
        /// resultado era una ficha impecable y falsa. Ver <see cref="ClimaPorDefecto"/>.
        /// </summary>
        /// <param name="datos">Serie climática cargada para la unidad de cultivo.</param>
        /// <param name="fecha">Día que se busca.</param>
        /// <param name="selector">Campo de la serie: ETo, temperatura, viento o humedad.</param>
        /// <param name="porDefectoDelMes">Media del mes para ese mismo campo.</param>
        /// <param name="estimadoDelMes">true si se ha tenido que recurrir a la media del mes.</param>
        internal static double ValorClimaticoConRespaldo(List<DatoClimatico> datos, DateTime fecha,
                                                        Func<DatoClimatico, double> selector,
                                                        Func<int, double> porDefectoDelMes,
                                                        out bool estimadoDelMes) {
            estimadoDelMes = false;
            DatoClimatico hoy = datos?.Find(d => d.Fecha == fecha);
            if (hoy != null)
                return selector(hoy);

            double suma = 0;
            int n = 0;
            for (int i = 1; i <= 3; i++) {
                DatoClimatico dia = datos?.Find(d => d.Fecha == fecha.AddDays(-i));
                if (dia != null) {
                    suma += selector(dia);
                    n++;
                }
            }
            if (n > 0)
                return suma / n;
            estimadoDelMes = true;
            return porDefectoDelMes(fecha.Month);
        }

        /// <summary>
        /// Envoltorio de instancia: aplica el respaldo y toma nota del día si ha hecho falta
        /// la media del mes.
        /// </summary>
        private double ValorClimatico(DateTime fecha, Func<DatoClimatico, double> selector, Func<int, double> porDefectoDelMes) {
            double valor = ValorClimaticoConRespaldo(lDatosClimaticos, fecha, selector, porDefectoDelMes, out bool estimado);
            if (estimado) {
                diasSinClima.Add(fecha.Date);
                Incidencias.AñadeDia("CLIMA_ESTIMADO", GravedadIncidencia.Aviso,
                    "Sin datos de la estación: {0} día(s) calculados con las medias del mes ({1}). " +
                    "La lluvia de esos días se toma como 0, nunca se estima.", fecha);
            } else if (lDatosClimaticos?.Find(x => x.Fecha == fecha) == null) {
                // Hay dato en los tres días anteriores, así que el hueco se rellena con su
                // media. Es mucho mejor que la media del mes, pero sigue sin ser el dato.
                Incidencias.AñadeDia("CLIMA_INTERPOLADO", GravedadIncidencia.Aviso,
                    "Días sin publicar en la estación, rellenados con la media de los tres " +
                    "anteriores: {0} día(s) ({1}).", fecha);
            }
            return valor;
        }

        /// <summary>
        /// Retorna velocidad del viento a una Fecha.  0 Si no se dispone de datos en esa fecha.
        /// </summary>
        public double VelocidadViento(DateTime fecha) {
            return ValorClimatico(fecha, d => d.VelViento, ClimaPorDefecto.Viento);
        }

        /// <summary>
        /// Retorna Humedad media a una fecha. 0 Si no se dispone de datos en esa fecha.
        /// </summary>
        public double HumedadMedia(DateTime fecha) {
            return ValorClimatico(fecha, d => d.HumedadMedia, ClimaPorDefecto.Humedad);
        }

        /// <summary>
        /// Retorna la temperatura a una fecha.  0 Si no se dispone de datos en esa fecha.
        /// </summary>
        public double Temperatura(DateTime fecha) {
            return ValorClimatico(fecha, d => d.TempMedia, ClimaPorDefecto.Temperatura);
        }

        /// <summary>
        /// Retorna los mm de lluvía a una fecha.  0 Si no se dispone de datos en esa fecha.
        /// </summary>
        /// <param name="fecha">.</param>
        /// <returns>.</returns>
        public double LluviaMm(DateTime fecha) {
            UnidadCultivoDatosExtra extra = DatoExtra(fecha);
            if (extra?.LluviaMm != null)
                return extra.LluviaMm ?? 0;
            else
                return (lDatosClimaticos.Find(d => d.Fecha == fecha)?.Precipitacion) ?? 0;
        }

        /// <summary>
        /// Retorna el valor de Evotranspieración a una fecha.  0 Si no se dispone de datos en esa fecha.
        /// </summary>
        /// <param name="fecha">.</param>
        /// <returns>.</returns>
        public double Eto(DateTime fecha) {
            return ValorClimatico(fecha, d => d.Eto, ClimaPorDefecto.Eto);
        }

        /// <summary>
        /// Retorna la definición de la clase de estrés.
        /// </summary>
        /// <param name="indiceEstres">The ie<see cref="double"/>.</param>
        /// <param name="nEtapa">The nEtapa<see cref="int"/>.</param>
        /// <returns>The <see cref="string"/>.</returns>
        public TipoEstresUmbral TipoEstresUmbral(double indiceEstres, int nEtapa) {
            int nEtapaBase0 = nEtapa - 1;
            return TipoEstresUmbral(UnidadCultivoCultivoEtapasList[nEtapaBase0].IdTipoEstres, indiceEstres);
        }



        /// <summary>
        /// Umbral (UmbralMaximo) del tipo de estrés de la etapa, localizado por su IdUmbral.
        ///
        /// Antes se usaba el IdUmbral como POSICIÓN de la lista (lEstresUmbral[(int)id]), lo que
        /// solo acierta si los identificadores empiezan en 0 y coinciden con el orden de la lista.
        /// ClaseEstresUmbralInferiorYSuperior ya lo hacía bien con Find(IdUmbral==id); aquí se
        /// aplica el mismo criterio. selectorIdUmbral elige umbral superior u óptimo del TipoEstres.
        /// </summary>
        private double UmbralRiegoPorId(int numeroEtapaDesarrollo, Func<TipoEstres, int?> selectorIdUmbral) {
            int nEtapaBas0 = numeroEtapaDesarrollo - 1;
            if (nEtapaBas0 < 0 || UnidadCultivoCultivoEtapasList.Count <= nEtapaBas0)
                return double.MinValue;
            var idTipoEstres = UnidadCultivoCultivoEtapasList[nEtapaBas0].IdTipoEstres;
            if (idTipoEstres == null || !lTiposEstres.ContainsKey(idTipoEstres))
                return double.MinValue;
            int? idUmbral = selectorIdUmbral(lTiposEstres[idTipoEstres]);
            if (idUmbral == null || !lTipoEstresUmbralList.ContainsKey(idTipoEstres))
                return double.MinValue;
            var umbral = lTipoEstresUmbralList[idTipoEstres].Find(x => x.IdUmbral == idUmbral);
            return umbral?.UmbralMaximo ?? double.MinValue;
        }

        internal double UmbralSuperiorRiego(int numeroEtapaDesarrollo) {
            return UmbralRiegoPorId(numeroEtapaDesarrollo, e => e.IdUmbralSuperiorRiego);
        }

        internal double UmbralOptimoRiego(int numeroEtapaDesarrollo) {
            return UmbralRiegoPorId(numeroEtapaDesarrollo, e => e.IdUmbralOptimoRiego);
        }
    }
}
