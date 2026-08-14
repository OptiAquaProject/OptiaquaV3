namespace DatosOptiaqua {
    using Models;
    using Newtonsoft.Json;
    using System;
    using System.Globalization;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using webapi.Utiles;

    /// <summary>
    /// Estado hídrico de una unidad de cultivo servido desde la tabla EstadoHidricoUC.
    ///
    /// Es la puerta única por la que deben pasar las pantallas y la API para pedir
    /// "cómo está esta unidad de cultivo hoy". Si la fila guardada sirve, se devuelve tal
    /// cual: una lectura indexada, sin cargar los datos hídricos ni recorrer el balance.
    /// Si no sirve, se calcula por el camino de siempre y se guarda para la próxima.
    ///
    /// Cuánto se ahorra, medido: montarlo desde cero cuesta 7,68 ms de cargar
    /// UnidadCultivoDatosHidricos, 5,13 ms de calcular el balance y ~2 ms de componer la
    /// respuesta. Leerlo de la tabla es una fila.
    ///
    /// La tabla es una MATERIALIZACIÓN: si no existe, o si falla al leer o al escribir,
    /// todo sigue funcionando por el camino de cálculo. Nada de esto puede tumbar una
    /// petición.
    /// </summary>
    public static class EstadoHidricoMaterializado {
        /// <summary>
        /// Versión del algoritmo de cálculo. SUBIRLA A MANO al tocar una fórmula del
        /// balance o el contenido de DatosEstadoHidrico: invalida de golpe todo lo
        /// guardado, sin tener que borrar nada.
        ///
        ///   1 — primera versión materializada (14/08/2026).
        ///   2 — DatosEstadoHidrico lleva IdTipoEstres, que necesita la barra de severidad
        ///       para saber qué escala de umbrales pintar (14/08/2026).
        ///   3 — DatosEstadoHidrico lleva la lista de incidencias del cálculo (14/08/2026).
        /// </summary>
        public const int VersionAlgoritmo = 3;

        /// <summary>
        /// Estado hídrico de la unidad de cultivo en la fecha indicada.
        /// </summary>
        /// <param name="idUnidadCultivo">Unidad de cultivo.</param>
        /// <param name="fecha">Día pedido. La fila guardada solo vale para su mismo día.</param>
        /// <param name="usarMaterializado">
        /// false para forzar el cálculo y reescribir la fila; lo usa la pasada nocturna.
        /// </param>
        /// <returns>El estado hídrico, o null si la unidad de cultivo no tiene temporada.</returns>
        public static DatosEstadoHidrico Obtener(string idUnidadCultivo, DateTime fecha, bool usarMaterializado = true) {
            string idTemporada = DB.TemporadaDeFecha(idUnidadCultivo, fecha);
            if (idTemporada == null)
                return null;

            if (usarMaterializado) {
                DatosEstadoHidrico guardado = DB.EstadoHidricoLeer(idUnidadCultivo, idTemporada, fecha.Date, VersionAlgoritmo);
                if (guardado != null)
                    return guardado;
            }

            // Cuando se fuerza el recálculo (la pasada nocturna) tampoco se pasa por la caché
            // en memoria: ni se lee de ella, que es lo que se quiere rehacer, ni se llena con
            // 1.264 balances que nadie ha pedido y que solo harían rotar la caché.
            BalanceHidrico bh = BalanceHidrico.Balance(idUnidadCultivo, fecha, true, usarMaterializado);
            if (bh == null)
                return null;
            DatosEstadoHidrico ret = bh.DatosEstadoHidrico(fecha);
            // Se guarda bajo la fecha PEDIDA, no bajo ret.Fecha: el balance termina ayer y,
            // si el cultivo ya cerró su ciclo, mucho antes, así que las dos casi nunca
            // coinciden. Guardar por ret.Fecha dejaba la tabla sin acertar (250 de 1.262).
            DB.EstadoHidricoGuardar(idUnidadCultivo, idTemporada, fecha.Date, ret,
                                    Huella(bh.unidadCultivoDatosHidricos), VersionAlgoritmo);
            return ret;
        }

        /// <summary>
        /// Estado hídrico de una lista de unidades de cultivo de la MISMA temporada.
        ///
        /// Es lo que usan MiZona y el panel. La diferencia con llamar a Obtener en un bucle
        /// es que lo guardado se lee de una sola vez: antes eran tantas consultas —y tantas
        /// conexiones— como unidades de cultivo en la pantalla.
        /// </summary>
        /// <param name="idTemporada">Temporada de todas ellas.</param>
        /// <param name="idsUnidadCultivo">Unidades de cultivo a mostrar, en el orden deseado.</param>
        /// <param name="fecha">Día pedido.</param>
        /// <returns>
        /// Un estado por unidad de cultivo, en el mismo orden. Las que fallen salen con
        /// Status = "ERROR: …", que es como ya lo pintaban las vistas.
        /// </returns>
        public static List<DatosEstadoHidrico> ObtenerLista(string idTemporada, IEnumerable<string> idsUnidadCultivo, DateTime fecha) {
            var ret = new List<DatosEstadoHidrico>();
            var guardados = DB.EstadoHidricoLeerLista(idTemporada, fecha.Date, VersionAlgoritmo);
            foreach (var idUC in idsUnidadCultivo) {
                try {
                    if (guardados.TryGetValue(idUC, out var guardado)) {
                        ret.Add(guardado);
                        continue;
                    }
                    var calculado = Obtener(idUC, fecha);
                    if (calculado != null)
                        ret.Add(calculado);
                } catch (Exception ex) {
                    ret.Add(new DatosEstadoHidrico { IdUnidadCultivo = idUC, IdTemporada = idTemporada, Status = "ERROR: " + ex.Message });
                }
            }
            return ret;
        }

        /// <summary>
        /// SHA-256 en hexadecimal de las entradas ESTRUCTURALES del balance: las que, al
        /// cambiar, dejan sin valor la temporada entera.
        ///
        /// No entra nada con fecha —clima, riegos, datos extra—: eso invalida desde un día
        /// concreto y se trata con las marcas de sucio, no con la huella.
        ///
        /// Cuidados que hacen que la huella no cambie sola:
        ///  - los números van con formato invariante y redondeados a 6 decimales, que es
        ///    mucho más de lo que significa cualquiera de estas magnitudes;
        ///  - las listas se recorren en el orden en que las devuelve el cálculo, que es el
        ///    de la consulta, y las etapas van por su orden de etapa;
        ///  - las fechas en yyyyMMdd, nunca en formato local.
        /// </summary>
        public static string Huella(UnidadCultivoDatosHidricos dh) {
            if (dh == null)
                return null;
            var sb = new StringBuilder();
            void N(double? v) => sb.Append(v == null ? "~" : Math.Round(v.Value, 6).ToString("0.######", CultureInfo.InvariantCulture)).Append('|');
            void S(string v) => sb.Append(v ?? "~").Append('|');
            void F(DateTime? v) => sb.Append(v == null ? "~" : v.Value.ToString("yyyyMMdd", CultureInfo.InvariantCulture)).Append('|');

            S(dh.IdUnidadCultivo); S(dh.IdTemporada);
            N(dh.UnidadCultivoExtensionM2); N(dh.EficienciaRiego); N(dh.Pluviometria);
            S(dh.IdCultivo?.ToString(CultureInfo.InvariantCulture)); S(dh.IdTipoRiego?.ToString(CultureInfo.InvariantCulture));
            sb.Append(dh.IdEstacion).Append('|');
            F(dh.FechaSiembra()); F(dh.FechaFinalDeEstudio());
            N(dh.CultivoTBase); N(dh.CultivoProfRaizInicial); N(dh.CultivoProfRaizMax); N(dh.CultivoIntegralEmergencia);

            sb.Append("ETAPAS|");
            foreach (var e in dh.UnidadCultivoCultivoEtapasList) {
                sb.Append(e.IdEtapaCultivo).Append('|');
                sb.Append(e.DuracionDiasEtapa).Append('|');
                sb.Append(e.DefinicionPorDias ? '1' : '0').Append('|');
                sb.Append(e.SeAplicaRiego == true ? '1' : '0').Append('|');
                N(e.CobInicial); N(e.CobFinal); N(e.AlturaInicial); N(e.AlturaFinal);
                sb.Append(e.IdTipoEstres).Append('|');
                F(e.FechaInicioEtapaConfirmada);
                S(e.ParametrosJson);
            }

            sb.Append("SUELO|");
            foreach (var s in dh.lUCSuelo) {
                sb.Append(s.IdParcelaInt).Append('|');
                N(s.ProfundidadCM); N(s.Arena); N(s.Limo); N(s.Arcilla);
                N(s.ElementosGruesos); N(s.MateriaOrganica); N(s.Superficie);
            }

            using (var sha = SHA256.Create())
                return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()))).ToLowerInvariant();
        }

        /// <summary>Serializa el estado hídrico para guardarlo.</summary>
        internal static string ASerializado(DatosEstadoHidrico datos) => JsonConvert.SerializeObject(datos);

        /// <summary>Recupera el estado hídrico guardado. Devuelve null si el texto no vale.</summary>
        internal static DatosEstadoHidrico DeSerializado(string json) {
            if (string.IsNullOrWhiteSpace(json))
                return null;
            try {
                return JsonConvert.DeserializeObject<DatosEstadoHidrico>(json);
            } catch (Exception ex) {
                Log.Aviso("No se pudo interpretar un estado hídrico guardado; se recalculará", ex);
                return null;
            }
        }
    }
}
