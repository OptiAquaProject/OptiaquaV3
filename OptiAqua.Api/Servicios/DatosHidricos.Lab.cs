namespace DatosOptiaqua {
    using Models;
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// La cara de laboratorio de <see cref="UnidadCultivoDatosHidricos"/>: construirlo desde un
    /// ensayo en memoria en vez de desde la base de datos, y volcarlo a un ensayo.
    ///
    /// Va aparte del fichero principal a propósito. El constructor de producción y el de
    /// laboratorio rellenan los mismos campos, pero uno consulta y el otro copia; teniéndolos
    /// separados se ve de un vistazo que el de laboratorio no escribe ni lee nada, y el motor
    /// de cálculo no se entera de por cuál de los dos ha venido.
    /// </summary>
    public partial class UnidadCultivoDatosHidricos {

        /// <summary>
        /// El ensayo del que salen los datos, o null si vienen de la base de datos. Las
        /// propiedades que en producción consultan —superficie, estación, número de parcelas—
        /// miran aquí primero: en un ensayo el usuario ha podido cambiarlas.
        /// </summary>
        internal LabOneEnsayo Ensayo { get; private set; }

        /// <summary>Si esta instancia es un ensayo de laboratorio y no datos reales.</summary>
        internal bool EsLab => Ensayo != null;

        /// <summary>
        /// Monta los datos hídricos a partir de un ensayo, sin tocar la base de datos salvo
        /// para el catálogo de tipos de estrés, que es común a toda la aplicación.
        /// </summary>
        /// <param name="ensayo">El ensayo con todos los datos ya editados.</param>
        public UnidadCultivoDatosHidricos(LabOneEnsayo ensayo) {
            Ensayo = ensayo ?? throw new ArgumentNullException(nameof(ensayo));

            temporada = new Temporada {
                IdTemporada = ensayo.IdTemporada,
                FechaInicial = ensayo.TemporadaFechaInicial,
                FechaFinal = ensayo.TemporadaFechaFinal,
            };

            unidadCultivo = new UnidadCultivo {
                IdUnidadCultivo = ensayo.IdUnidadCultivo,
                IdRegante = ensayo.IdRegante,
                Alias = ensayo.Alias,
                TipoSueloDescripcion = ensayo.TipoSueloDescripcion,
            };

            // Sin superficie no hay forma de pasar los m³ de riego a mm: el balance entero
            // saldría en blanco. Se corta aquí, que es donde se puede explicar por qué.
            pUnidadCultivoExtensionM2 = ensayo.SuperficieM2;
            if (pUnidadCultivoExtensionM2 <= 0)
                throw new Exception("La superficie del ensayo es 0: sin ella no se puede pasar el riego de m³ a mm.");

            UnidadCultivoCultivoEtapasList = ensayo.Etapas ?? new List<UnidadCultivoCultivoEtapas>();
            if (UnidadCultivoCultivoEtapasList.Count == 0)
                throw new Exception("El ensayo no tiene etapas de desarrollo.");
            ParametrosEtapas = DeserializaParamtros(UnidadCultivoCultivoEtapasList);

            unidadCultivoCultivo = new UnidadCultivoCultivo {
                IdUnidadCultivo = ensayo.IdUnidadCultivo,
                IdTemporada = ensayo.IdTemporada,
                IdCultivo = ensayo.IdCultivo,
                IdRegante = ensayo.IdRegante,
                IdTipoRiego = ensayo.IdTipoRiego,
                Pluviometria = ensayo.Pluviometria,
                SuperficieM2 = ensayo.SuperficieM2,
            };

            cultivo = new Cultivo {
                IdCultivo = ensayo.IdCultivo,
                Nombre = ensayo.CultivoNombre,
                TBase = ensayo.TBase,
                ProfRaizInicial = ensayo.ProfRaizInicial,
                ProfRaizMax = ensayo.ProfRaizMax,
                IntegralEmergencia = ensayo.IntegralEmergencia,
            };

            riegoTipo = new RiegoTipo {
                IdTipoRiego = ensayo.IdTipoRiego,
                TipoRiego = ensayo.TipoRiego,
                Eficiencia = ensayo.EficienciaRiego,
            };

            regante = new Regante {
                IdRegante = ensayo.IdRegante,
                Nombre = ensayo.ReganteNombre,
                NIF = ensayo.ReganteNif,
            };

            // El catálogo de tipos de estrés y sus umbrales no se copia al ensayo: es la escala
            // con la que se mide, común a toda la aplicación. Lo que el ensayo elige es cuál usa
            // cada etapa.
            lTiposEstres = DB.ListaTipoEstres();
            lTipoEstresUmbralList = DB.ListaEstresUmbral();

            lDatosClimaticos = ensayo.Clima ?? new List<DatoClimatico>();

            lUCSuelo = ensayo.Suelo ?? new List<DatosSuelo>();
            if (lUCSuelo.Count == 0)
                throw new Exception("El ensayo no tiene ningún horizonte de suelo.");

            lDatosRiego = ensayo.Riegos ?? new List<Riego>();
            lUnidadCultivoDatosExtas = ensayo.DatosExtra ?? new List<UnidadCultivoDatosExtra>();

            AvisaSiLaRaizSuperaElSuelo();
        }

        /// <summary>
        /// La raíz se mide en METROS y el suelo en centímetros. Si la raíz del cultivo llega más
        /// hondo de lo que se ha medido el suelo, el agua disponible se calcula solo hasta donde
        /// hay dato: no se extrapola. Se avisa porque cambia el resultado —en viña llegó a ser un
        /// 29% menos de agua disponible— y no es un fallo, es una decisión: el suelo por debajo de
        /// lo medido no se inventa.
        /// </summary>
        private void AvisaSiLaRaizSuperaElSuelo() {
            double sueloCm = 0;
            foreach (var h in lUCSuelo)
                if (h.ProfundidadCM > sueloCm) sueloCm = h.ProfundidadCM;
            double raizCm = CultivoProfRaizMax * 100.0;
            if (raizCm > sueloCm + 0.5)
                Incidencias.Añade("RAIZ_SUPERA_SUELO", GravedadIncidencia.Aviso,
                    $"La raíz del cultivo llega a {raizCm:N0} cm y el suelo está medido hasta " +
                    $"{sueloCm:N0} cm: el agua disponible se calcula solo hasta esa profundidad.");
        }

        /// <summary>
        /// Vuelca a un ensayo editable todo lo que esta instancia tiene cargado.
        ///
        /// Está aquí, dentro de la clase, porque los datos viven en campos privados: copiarlos
        /// desde fuera obligaría a abrirlos o a repetir las consultas, y repetirlas es justo lo
        /// que no interesa —el punto de partida del laboratorio tiene que ser el mismo que usa
        /// el cálculo de producción, no una segunda lectura que podría diferir—.
        /// </summary>
        /// <returns>El ensayo con una copia de los datos.</returns>
        internal LabOneEnsayo ACopiaLab() {
            ObtenerMunicicioParaje(out string _, out string municipios, out string parajes);
            return new LabOneEnsayo {
                IdUnidadCultivo = unidadCultivo.IdUnidadCultivo,
                IdTemporada = temporada.IdTemporada,
                TemporadaFechaInicial = temporada.FechaInicial,
                TemporadaFechaFinal = temporada.FechaFinal,
                Alias = unidadCultivo.Alias,
                TipoSueloDescripcion = unidadCultivo.TipoSueloDescripcion,
                IdRegante = regante?.IdRegante ?? 0,
                ReganteNombre = regante?.Nombre,
                ReganteNif = regante?.NIF,
                Municipios = municipios,
                Parajes = parajes,
                SuperficieM2 = pUnidadCultivoExtensionM2,
                NParcelas = NParcelas,
                Pluviometria = unidadCultivoCultivo.Pluviometria,
                IdTipoRiego = riegoTipo.IdTipoRiego,
                TipoRiego = riegoTipo.TipoRiego,
                EficienciaRiego = riegoTipo.Eficiencia,
                IdCultivo = cultivo.IdCultivo,
                CultivoNombre = cultivo.Nombre,
                TBase = cultivo.TBase,
                ProfRaizInicial = cultivo.ProfRaizInicial,
                ProfRaizMax = cultivo.ProfRaizMax,
                IntegralEmergencia = cultivo.IntegralEmergencia,
                IdEstacion = IdEstacion,
                EstacionNombre = DB.EstacionNombre(IdEstacion),
                Etapas = new List<UnidadCultivoCultivoEtapas>(UnidadCultivoCultivoEtapasList),
                Suelo = new List<DatosSuelo>(lUCSuelo),
                Clima = new List<DatoClimatico>(lDatosClimaticos),
                Riegos = new List<Riego>(lDatosRiego ?? new List<Riego>()),
                DatosExtra = new List<UnidadCultivoDatosExtra>(lUnidadCultivoDatosExtas ?? new List<UnidadCultivoDatosExtra>()),
            };
        }
    }
}
