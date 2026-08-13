namespace DatosOptiaqua {
    using Models;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using webapi.Utiles;

    /// <summary>
    /// Defines the <see cref="CacheUnidadCultivo" />.
    /// </summary>
    public class CacheUnidadCultivo {
        /// <summary>
        /// Gets or sets the Fecha.
        /// </summary>
        public DateTime Fecha { set; get; }

        /// <summary>
        /// Gets or sets the Balance.
        /// </summary>
        public BalanceHidrico Balance { set; get; }

        /// <summary>
        /// Momento del último uso. Es lo que decide a quién se descarta cuando la caché llega
        /// al tope; sin esto no había forma de elegir, porque Fecha es el día de cálculo y
        /// después de un recálculo total la tienen todas igual.
        /// </summary>
        public DateTime UltimoUso { set; get; }
    }

    /// <summary>
    /// Entrada de la caché de respuestas, con el momento en que se calculó.
    /// </summary>
    internal class EntradaRespuesta {
        public object Valor { get; set; }
        public DateTime Calculada { get; set; }
    }

    /// <summary>
    /// Caché en memoria de los balances hídricos y de las respuestas ya calculadas.
    ///
    /// Diferencias con la versión de .NET Framework, que era la causa más probable de los
    /// cuelgues bajo carga:
    ///
    ///  - Los diccionarios eran Dictionary estáticos SIN sincronización. Dos peticiones
    ///    simultáneas podían corromper la estructura interna y dejar una lectura posterior en
    ///    bucle infinito, o lanzar ArgumentException al insertar una clave repetida. Ahora son
    ///    ConcurrentDictionary.
    ///  - Se memorizaba el IHttpActionResult completo, que retiene referencias a la petición.
    ///    Ahora se guarda sólo el dato; el controlador construye la respuesta.
    ///  - No había caducidad ni límite: bastaban peticiones con URI distinta para agotar la
    ///    memoria. Ahora las entradas caducan y el número está acotado.
    ///  - "recalculando" era un bool sin sincronizar; ahora es un contador con Interlocked, que
    ///    sí garantiza que sólo entre un hilo.
    /// </summary>
    public static class CacheDatosHidricos {
        private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, CacheUnidadCultivo>> lCacheBalances =
            new ConcurrentDictionary<string, ConcurrentDictionary<string, CacheUnidadCultivo>>();

        private static readonly ConcurrentDictionary<string, EntradaRespuesta> lCacheRespuestas =
            new ConcurrentDictionary<string, EntradaRespuesta>();

        /// <summary>Tiempo que se considera válida una respuesta memorizada.</summary>
        private static readonly TimeSpan Caducidad = TimeSpan.FromHours(12);

        /// <summary>Tope de respuestas memorizadas. Al superarlo se descartan las más antiguas.</summary>
        private const int MaximoRespuestas = 5000;

        /// <summary>
        /// Tope de balances memorizados, sumando todas las temporadas.
        ///
        /// Un balance completo ocupa del orden de 170 KB: guardar los 1.264 que hay hoy son
        /// 211 MB medidos, y con una temporada configurada del todo (unas 8.000 unidades de
        /// cultivo) pasarían de 1 GB. No compensa: recalcular uno cuesta unos 13 ms, así que
        /// un fallo de caché es imperceptible y la memoria deja de crecer sin control.
        /// </summary>
        private const int MaximoBalances = 300;

        /// <summary>
        /// The Balance.
        /// </summary>
        public static BalanceHidrico Balance(string idUC, DateTime fecha) {
            string idTemporada = DB.TemporadaDeFecha(idUC, fecha);
            if (idTemporada == null)
                return null;
            ConcurrentDictionary<string, CacheUnidadCultivo> cacheTemporada;
            if (!lCacheBalances.TryGetValue(idTemporada, out cacheTemporada))
                return null;
            CacheUnidadCultivo cacheUnidadCultivo;
            if (!cacheTemporada.TryGetValue(idUC, out cacheUnidadCultivo))
                return null;
            if (fecha > cacheUnidadCultivo.Fecha)
                return null;
            cacheUnidadCultivo.UltimoUso = DateTime.UtcNow;
            return cacheUnidadCultivo.Balance;
        }

        /// <summary>Balances memorizados ahora mismo, sumando todas las temporadas.</summary>
        internal static int BalancesMemorizados() {
            return lCacheBalances.Values.Sum(x => x.Count);
        }

        /// <summary>
        /// Deja la caché de balances por debajo del tope descartando los de uso más antiguo.
        /// Se quita un 20% de más para no volver a entrar aquí en la siguiente petición.
        /// </summary>
        private static void DescartaBalancesMasAntiguos() {
            int sobran = BalancesMemorizados() - MaximoBalances + MaximoBalances / 5;
            if (sobran <= 0)
                return;
            var candidatos = lCacheBalances
                .SelectMany(t => t.Value.Select(uc => new { Temporada = t.Key, Uc = uc.Key, uc.Value.UltimoUso }))
                .OrderBy(x => x.UltimoUso)
                .Take(sobran)
                .ToList();
            foreach (var c in candidatos)
                if (lCacheBalances.TryGetValue(c.Temporada, out var cacheTemporada))
                    cacheTemporada.TryRemove(c.Uc, out _);
            Log.Info($"Caché de balances: descartados {candidatos.Count} de uso más antiguo; quedan {BalancesMemorizados()}");
        }

        /// <summary>
        /// 0 = libre, 1 = recálculo en curso. Se manipula con Interlocked.
        /// </summary>
        private static int enRecalculo = 0;

        public static bool Recalculando {
            get { return Volatile.Read(ref enRecalculo) == 1; }
        }

        internal static void SetDirtyContainsKey(string key) {
            string patron = key.ToUpperInvariant();
            foreach (var clave in lCacheRespuestas.Keys.Where(k => k.ToUpperInvariant().Contains(patron)).ToList()) {
                EntradaRespuesta descartada;
                lCacheRespuestas.TryRemove(clave, out descartada);
            }
        }

        /// <summary>
        /// The RecreateAll.
        /// </summary>
        public static bool RecreateAll() {
            // Comprobar-y-marcar en una sola operación atómica: con el bool anterior, dos hilos
            // podían leer false a la vez y lanzar dos recálculos simultáneos.
            if (Interlocked.CompareExchange(ref enRecalculo, 1, 0) == 1)
                return false;
            bool correcto = false;
            // El proceso recorre todas las unidades de cultivo de todas las temporadas y puede
            // durar mucho: se publica el progreso para poder seguirlo desde el cuadro de mando.
            ProgresoRecalculo.Comienza("Balances hídricos", 0);
            try {
                DateTime dateUpdate = DateTime.Now.Date;
                DateTime fechaCalculo = DateTime.Now.Date;
                lCacheBalances.Clear();
                lCacheRespuestas.Clear();
                DB.InsertaEvento("Inicia RecreateAll" + DateTime.Now.ToString());

                ProgresoRecalculo.CambiaFase("Descargando datos climáticos del SIAR");
                DB.DatosClimaticosSiarForceRefresh();

                List<Models.Temporada> lTemporadas = DB.TemporadasList();
                using (var db = Conexion.Nueva()) {
                    // Conteo previo para que la barra tenga referencia desde el principio.
                    ProgresoRecalculo.CambiaFase("Contando unidades de cultivo");
                    try {
                        int totalUC = db.SingleOrDefault<int?>(
                            "SELECT COUNT(*) FROM (SELECT DISTINCT IdTemporada, IdUnidadCultivo FROM UnidadCultivoCultivo) x") ?? 0;
                        ProgresoRecalculo.FijaTotal(totalUC);
                    } catch (Exception ex) {
                        Log.Aviso("No se pudo contar las unidades de cultivo para estimar el progreso", ex);
                    }

                    foreach (Models.Temporada temporada in lTemporadas) {
                        string idTemporada = temporada.IdTemporada;
                        ProgresoRecalculo.CambiaFase("Temporada " + idTemporada);
                        if (dateUpdate >= temporada.FechaFinal)
                            fechaCalculo = temporada.FechaFinal;
                        else
                            fechaCalculo = dateUpdate;
                        List<string> lIdUnidadCultivo = db.Fetch<string>("SELECT DISTINCT IdUnidadCultivo from UnidadCultivoCultivo WHERE IdTemporada=@0", idTemporada);
                        foreach (string idUC in lIdUnidadCultivo) {
                            try {
                                // El balance se calcula pero NO se memoriza. Antes esta pasada
                                // dejaba en memoria los 1.264 balances (211 MB medidos, y más de
                                // 1 GB con una temporada configurada del todo) para ahorrar unos
                                // 13 ms por unidad de cultivo. Lo que sí hace falta de esta pasada
                                // es actualizar las fechas de etapa y sacar a la luz las unidades
                                // que no calculan; la caché se llena sola con lo que se consulte.
                                BalanceHidrico.Balance(idUC, fechaCalculo, true, false);
                            } catch (Exception ex) {
                                // Antes se asignaba el mensaje a una variable que nadie leía: el
                                // fallo de una unidad de cultivo desaparecía sin rastro.
                                ProgresoRecalculo.ApuntaError();
                                Log.Aviso("No se pudo calcular el balance de la unidad de cultivo " + idUC + " en la temporada " + idTemporada, ex);
                            }
                            ProgresoRecalculo.Avanza(idUC + " (" + idTemporada + ")");
                        }
                    }
                }
                DB.InsertaEvento("Finaliza RecreateAll" + DateTime.Now.ToString());
                correcto = true;
                return true;
            } finally {
                ProgresoRecalculo.Termina(correcto);
                Volatile.Write(ref enRecalculo, 0);
            }
        }

        internal static void ClearAll() {
            lCacheBalances.Clear();
            lCacheRespuestas.Clear();
        }

        static public string RecalculaSuelos() {
            if (Interlocked.CompareExchange(ref enRecalculo, 1, 0) == 1)
                return "ya se está recalculando";
            bool correcto = false;
            ProgresoRecalculo.Comienza("Suelos por unidad de cultivo", 0);
            try {
                DB.InsertaEvento("Inicia recalculando suelos" + DateTime.Now.ToString());
                var ret = new List<SueloUnidadCultivoTemporada>();
                int unidadesFallidas = 0;
                var lTemporadas = DB.TemporadasList();
                using (var db = Conexion.Nueva()) {
                    // Cuántas filas hay antes de empezar: es la referencia para no dejar la tabla
                    // peor de como estaba (ver la guarda de más abajo).
                    int filasAntes = db.SingleOrDefault<int?>("SELECT COUNT(*) FROM SueloUnidadCultivoTemporada") ?? 0;
                    ProgresoRecalculo.CambiaFase("Contando unidades de cultivo");
                    try {
                        int totalUC = db.SingleOrDefault<int?>(
                            "SELECT COUNT(*) FROM (SELECT DISTINCT IdTemporada, IdUnidadCultivo FROM UnidadCultivoCultivo) x") ?? 0;
                        ProgresoRecalculo.FijaTotal(totalUC);
                    } catch (Exception ex) {
                        Log.Aviso("No se pudo contar las unidades de cultivo para estimar el progreso", ex);
                    }

                    foreach (var temporada in lTemporadas) {
                        string idTemporada = temporada.IdTemporada;
                        ProgresoRecalculo.CambiaFase("Temporada " + idTemporada);
                        List<string> lIdUnidadCultivo = db.Fetch<string>("SELECT DISTINCT IdUnidadCultivo from UnidadCultivoCultivo WHERE IdTemporada=@0", idTemporada);
                        foreach (string idUC in lIdUnidadCultivo) {
                            try {
                                // UnidadCultivoSueloListNew lanza excepción cuando una unidad de
                                // cultivo no tiene datos de suelo. Sin este try, una sola unidad
                                // mal configurada abortaba el recálculo entero.
                                var lUcSuelo = DB.UnidadCultivoSueloListNew(idUC, idTemporada);
                                foreach (var suelo in lUcSuelo) {
                                    ret.Add(new SueloUnidadCultivoTemporada {
                                        Arcilla = suelo.Arcilla,
                                        Arena = suelo.Arena,
                                        ElementosGruesos = suelo.ElementosGruesos,
                                        IdParcelaInt = suelo.IdParcelaInt,
                                        IdTemporada = idTemporada,
                                        IdUC = idUC,
                                        Limo = suelo.Limo,
                                        MateriaOrganica = suelo.MateriaOrganica,
                                        ProfundidadCM = suelo.ProfundidadCM,
                                        Superficie = suelo.Superficie,
                                    });
                                }
                            } catch (Exception ex) {
                                unidadesFallidas++;
                                ProgresoRecalculo.ApuntaError();
                                Log.Aviso("No se pudieron obtener los datos de suelo de la unidad de cultivo " + idUC + " en la temporada " + idTemporada, ex);
                            }
                            ProgresoRecalculo.Avanza(idUC + " (" + idTemporada + ")");
                        }
                    }

                    // No se borra lo que hay si el recálculo ha salido peor. La transacción de
                    // abajo garantiza que el borrado y la carga van juntos, pero no protege del
                    // caso de verdad peligroso: que el cálculo falle en casi todas las unidades y
                    // se sustituyan miles de filas buenas por cuatro. Pasó: mientras faltó el
                    // ensamblado de tipos espaciales, 1.263 de 1.264 unidades lanzaban excepción y
                    // este método habría dejado la tabla vacía, con todos los balances muriendo
                    // después por "No se ha definido suelo".
                    if (ret.Count == 0 || (unidadesFallidas > 0 && ret.Count < filasAntes)) {
                        string aviso = $"Recálculo de suelos DESCARTADO: salen {ret.Count:N0} filas frente a las " +
                                       $"{filasAntes:N0} que ya había, y {unidadesFallidas:N0} unidades de cultivo han fallado. " +
                                       "No se toca la tabla; el motivo de cada fallo está en el registro.";
                        Log.Info(aviso);
                        DB.InsertaEvento(aviso);
                        return aviso;
                    }

                    // Borrado y recarga dentro de una transacción: antes, si el InsertBulk fallaba
                    // después del Delete, la tabla quedaba vacía.
                    ProgresoRecalculo.CambiaFase("Guardando " + ret.Count.ToString("N0") + " registros de suelo");
                    using (var transaccion = db.GetTransaction()) {
                        db.Delete<SueloUnidadCultivoTemporada>("where 1=1");
                        db.InsertBulk(ret);
                        transaccion.Complete();
                    }
                }
                DB.InsertaEvento("Finaliza RecalculaSuelos" + DateTime.Now.ToString());
                correcto = true;
                return "OK";
            } finally {
                ProgresoRecalculo.Termina(correcto);
                Volatile.Write(ref enRecalculo, 0);
            }
        }

        /// <summary>
        /// Devuelve el valor memorizado para la clave indicada o, si no lo hay o ha caducado,
        /// el que produzca el generador.
        /// </summary>
        public static T Cache<T>(string clave, Func<T> generador) {
            EntradaRespuesta entrada;
            if (lCacheRespuestas.TryGetValue(clave, out entrada)) {
                if (DateTime.UtcNow - entrada.Calculada < Caducidad)
                    return (T)entrada.Valor;
                EntradaRespuesta caducada;
                lCacheRespuestas.TryRemove(clave, out caducada);
            }

            T ret = generador();

            if (lCacheRespuestas.Count >= MaximoRespuestas)
                DescartaMasAntiguas();

            lCacheRespuestas[clave] = new EntradaRespuesta { Valor = ret, Calculada = DateTime.UtcNow };
            return ret;
        }

        /// <summary>
        /// Deja sitio descartando el 20% de entradas más antiguas.
        /// </summary>
        private static void DescartaMasAntiguas() {
            var aDescartar = lCacheRespuestas
                .OrderBy(kv => kv.Value.Calculada)
                .Take(MaximoRespuestas / 5)
                .Select(kv => kv.Key)
                .ToList();
            foreach (var clave in aDescartar) {
                EntradaRespuesta descartada;
                lCacheRespuestas.TryRemove(clave, out descartada);
            }
            Log.Info("Caché de respuestas: descartadas " + aDescartar.Count + " entradas antiguas");
        }

        internal static void SetDirtyParcela(int idParcelaInt) {
            var lUC = DB.UnidadCultivosDePacela(idParcelaInt).Select(x => x.IdUC).Distinct();
            foreach (var idUC in lUC) {
                foreach (var cacheTemporada in lCacheBalances.Values) {
                    CacheUnidadCultivo descartado;
                    cacheTemporada.TryRemove(idUC, out descartado);
                }
            }
            // Se ponen en duda todas las respuestas memorizadas.
            lCacheRespuestas.Clear();
        }

        internal static void SetDirtyUC(string idUC) {
            foreach (var cacheTemporada in lCacheBalances.Values) {
                CacheUnidadCultivo descartado;
                cacheTemporada.TryRemove(idUC, out descartado);
            }
            // Se ponen en duda todas las respuestas memorizadas.
            lCacheRespuestas.Clear();
        }

        /// <summary>
        /// The Add.
        /// </summary>
        internal static void Add(BalanceHidrico bh, DateTime fecha) {
            if (bh == null)
                return;
            string idUC = bh.unidadCultivoDatosHidricos.IdUnidadCultivo;
            string idTemporada = DB.TemporadaDeFecha(idUC, fecha);
            if (idTemporada == null)
                return;
            var cacheTemporada = lCacheBalances.GetOrAdd(idTemporada, _ => new ConcurrentDictionary<string, CacheUnidadCultivo>());
            cacheTemporada[idUC] = new CacheUnidadCultivo { Fecha = fecha, Balance = bh, UltimoUso = DateTime.UtcNow };
            if (BalancesMemorizados() > MaximoBalances)
                DescartaBalancesMasAntiguos();
        }
    }
}
