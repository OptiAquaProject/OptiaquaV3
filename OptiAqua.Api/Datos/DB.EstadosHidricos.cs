namespace DatosOptiaqua {
    using Models;
    using System;
    using System.Collections.Generic;
    using webapi.Utiles;

    /// <summary>
    /// Capa de acceso a datos de OptiAqua sobre SQL Server (librería NPoco).
    /// Estado hídrico materializado: la tabla EstadoHidricoUC guarda, por unidad de
    /// cultivo, la respuesta ya montada del último día calculado.
    /// DB es una clase parcial repartida por dominios; dentro de cada fichero
    /// los miembros van en orden alfabético.
    /// </summary>
    public static partial class DB {

        /// <summary>
        /// Si la tabla existe. Se comprueba una vez y se recuerda: la consulta al catálogo
        /// en cada lectura costaría más que lo que ahorra la propia materialización.
        /// Vuelve a null al crear la tabla desde el panel.
        /// </summary>
        private static bool? existeTablaEstadoHidrico;

        /// <summary>
        /// Borra el estado guardado de TODAS las unidades de cultivo.
        /// Para cuando cambia algo que afecta a todos: parámetros globales, catálogos de
        /// estrés o el propio algoritmo.
        /// </summary>
        /// <returns>Filas borradas.</returns>
        internal static int EstadoHidricoInvalidarTodo() {
            if (!EstadoHidricoTablaExiste())
                return 0;
            try {
                using (var db = Conexion.Nueva())
                    return db.Execute("DELETE FROM EstadoHidricoUC");
            } catch (Exception ex) {
                Log.Aviso("No se pudo invalidar el estado hídrico materializado", ex);
                return 0;
            }
        }

        /// <summary>
        /// Borra el estado guardado de las unidades de cultivo de una estación climática.
        /// Es el camino del SIAR: cuando cambia el clima de una estación, deja de valer lo
        /// calculado para todo lo que riega bajo ella.
        /// </summary>
        /// <param name="idEstacion">Estación cuyos datos han cambiado.</param>
        /// <returns>Filas borradas.</returns>
        internal static int EstadoHidricoInvalidarPorEstacion(int idEstacion) {
            if (!EstadoHidricoTablaExiste())
                return 0;
            try {
                using (var db = Conexion.Nueva())
                    return db.Execute(
                        "DELETE e FROM EstadoHidricoUC e" +
                        " WHERE EXISTS (SELECT 1 FROM ParcelasDeUC p" +
                        "               WHERE p.IdUnidadCultivo = e.IdUnidadCultivo" +
                        "                 AND p.IdTemporada     = e.IdTemporada" +
                        "                 AND p.IdEstacion      = @0)", idEstacion.ToString());
            } catch (Exception ex) {
                Log.Aviso("No se pudo invalidar el estado hídrico de la estación " + idEstacion, ex);
                return 0;
            }
        }

        /// <summary>
        /// Borra el estado guardado de una unidad de cultivo, en todas sus temporadas.
        /// </summary>
        /// <param name="idUnidadCultivo">Unidad de cultivo que ha cambiado.</param>
        /// <returns>Filas borradas.</returns>
        internal static int EstadoHidricoInvalidarUC(string idUnidadCultivo) {
            if (!EstadoHidricoTablaExiste() || string.IsNullOrWhiteSpace(idUnidadCultivo))
                return 0;
            try {
                using (var db = Conexion.Nueva())
                    return db.Execute("DELETE FROM EstadoHidricoUC WHERE IdUnidadCultivo=@0", idUnidadCultivo);
            } catch (Exception ex) {
                Log.Aviso("No se pudo invalidar el estado hídrico de la unidad de cultivo " + idUnidadCultivo, ex);
                return 0;
            }
        }

        /// <summary>
        /// Borra el estado guardado de las unidades de cultivo que usan una parcela.
        /// </summary>
        /// <param name="idParcelaInt">Parcela que ha cambiado (superficie, regante…).</param>
        /// <returns>Filas borradas.</returns>
        internal static int EstadoHidricoInvalidarPorParcela(int idParcelaInt) {
            if (!EstadoHidricoTablaExiste())
                return 0;
            try {
                using (var db = Conexion.Nueva())
                    return db.Execute(
                        "DELETE e FROM EstadoHidricoUC e" +
                        " WHERE EXISTS (SELECT 1 FROM ParcelasDeUC p" +
                        "               WHERE p.IdUnidadCultivo = e.IdUnidadCultivo" +
                        "                 AND p.IdTemporada     = e.IdTemporada" +
                        "                 AND p.IdParcelaInt    = @0)", idParcelaInt);
            } catch (Exception ex) {
                Log.Aviso("No se pudo invalidar el estado hídrico de la parcela " + idParcelaInt, ex);
                return 0;
            }
        }

        /// <summary>
        /// Guarda (o reescribe) el estado hídrico de una unidad de cultivo.
        /// Un fallo aquí no puede tumbar la petición: se anota y se sigue, que el dato ya
        /// está calculado y devuelto.
        /// </summary>
        /// <param name="idUnidadCultivo">Unidad de cultivo.</param>
        /// <param name="idTemporada">Temporada.</param>
        /// <param name="fechaPedida">
        /// El día para el que vale la fila: el que ha pedido la pantalla. NO es
        /// datos.Fecha, que es el último día que alcanza el balance y casi nunca coincide
        /// —el balance termina ayer y, si el cultivo cerró su ciclo, mucho antes—. Buscar
        /// por la fecha del estado en vez de por la pedida hacía que la tabla no acertara
        /// casi nunca: 250 aciertos de 1.262 filas, medido.
        /// </param>
        /// <param name="datos">El estado hídrico ya montado.</param>
        /// <param name="huella">SHA-256 de las entradas estructurales, para la pasada de control.</param>
        /// <param name="versionAlgoritmo">Versión del cálculo con la que se ha obtenido.</param>
        internal static void EstadoHidricoGuardar(string idUnidadCultivo, string idTemporada, DateTime fechaPedida,
                                                  DatosEstadoHidrico datos, string huella, int versionAlgoritmo) {
            if (!EstadoHidricoTablaExiste() || datos == null)
                return;
            try {
                using (var db = Conexion.Nueva()) {
                    db.Execute("DELETE FROM EstadoHidricoUC WHERE IdTemporada=@0 AND IdUnidadCultivo=@1",
                               idTemporada, idUnidadCultivo);
                    db.Execute("INSERT INTO EstadoHidricoUC" +
                               " (IdTemporada, IdUnidadCultivo, FechaPedida, FechaEstado, Datos, HashEntradas, VersionAlgoritmo, FechaCalculo)" +
                               " VALUES (@0, @1, @2, @3, @4, @5, @6, @7)",
                               idTemporada, idUnidadCultivo, fechaPedida.Date, datos.Fecha.Date,
                               EstadoHidricoMaterializado.ASerializado(datos), huella, versionAlgoritmo, DateTime.Now);
                }
            } catch (Exception ex) {
                Log.Aviso("No se pudo guardar el estado hídrico de " + idUnidadCultivo + " (" + idTemporada + ")", ex);
            }
        }

        /// <summary>
        /// Lee el estado hídrico guardado si sirve para la fecha y la versión pedidas.
        /// </summary>
        /// <param name="idUnidadCultivo">Unidad de cultivo.</param>
        /// <param name="idTemporada">Temporada.</param>
        /// <param name="fecha">Día pedido; se compara con FechaPedida, no con FechaEstado.</param>
        /// <param name="versionAlgoritmo">Versión de cálculo que se espera.</param>
        /// <returns>El estado hídrico, o null si no hay fila válida.</returns>
        internal static DatosEstadoHidrico EstadoHidricoLeer(string idUnidadCultivo, string idTemporada,
                                                             DateTime fecha, int versionAlgoritmo) {
            if (!EstadoHidricoTablaExiste())
                return null;
            try {
                using (var db = Conexion.Nueva()) {
                    string json = db.SingleOrDefault<string>(
                        "SELECT Datos FROM EstadoHidricoUC" +
                        " WHERE IdTemporada=@0 AND IdUnidadCultivo=@1 AND FechaPedida=@2 AND VersionAlgoritmo=@3",
                        idTemporada, idUnidadCultivo, fecha.Date, versionAlgoritmo);
                    return EstadoHidricoMaterializado.DeSerializado(json);
                }
            } catch (Exception ex) {
                Log.Aviso("No se pudo leer el estado hídrico de " + idUnidadCultivo + " (" + idTemporada + ")", ex);
                return null;
            }
        }

        /// <summary>
        /// Lee de una vez el estado guardado de muchas unidades de cultivo.
        /// Es lo que piden las pantallas de lista —MiZona y el panel—, que antes hacían una
        /// consulta (y una conexión) por cada unidad.
        /// </summary>
        /// <param name="idTemporada">Temporada.</param>
        /// <param name="fecha">Día pedido.</param>
        /// <param name="versionAlgoritmo">Versión de cálculo que se espera.</param>
        /// <returns>Los que había guardados, por IdUnidadCultivo. Los que falten, no están.</returns>
        internal static Dictionary<string, DatosEstadoHidrico> EstadoHidricoLeerLista(
                string idTemporada, DateTime fecha, int versionAlgoritmo) {
            var ret = new Dictionary<string, DatosEstadoHidrico>();
            if (!EstadoHidricoTablaExiste())
                return ret;
            try {
                using (var db = Conexion.Nueva())
                    foreach (var f in db.Fetch<dynamic>(
                            "SELECT IdUnidadCultivo, Datos FROM EstadoHidricoUC" +
                            " WHERE IdTemporada=@0 AND FechaPedida=@1 AND VersionAlgoritmo=@2",
                            idTemporada, fecha.Date, versionAlgoritmo)) {
                        var datos = EstadoHidricoMaterializado.DeSerializado(f.Datos as string);
                        if (datos != null)
                            ret[(string)f.IdUnidadCultivo] = datos;
                    }
            } catch (Exception ex) {
                Log.Aviso("No se pudo leer el estado hídrico de la temporada " + idTemporada, ex);
            }
            return ret;
        }

        /// <summary>
        /// Huella guardada de cada unidad de cultivo, para que la pasada de control pueda
        /// comparar sin releer los JSON.
        /// </summary>
        /// <returns>Clave "IdTemporada|IdUnidadCultivo" y su huella.</returns>
        internal static Dictionary<string, string> EstadoHidricoHuellas() {
            var ret = new Dictionary<string, string>();
            if (!EstadoHidricoTablaExiste())
                return ret;
            try {
                using (var db = Conexion.Nueva())
                    foreach (var f in db.Fetch<dynamic>("SELECT IdTemporada, IdUnidadCultivo, HashEntradas FROM EstadoHidricoUC"))
                        ret[f.IdTemporada + "|" + f.IdUnidadCultivo] = f.HashEntradas as string;
            } catch (Exception ex) {
                Log.Aviso("No se pudieron leer las huellas del estado hídrico", ex);
            }
            return ret;
        }

        /// <summary>
        /// Si existe la tabla EstadoHidricoUC. Mientras no exista, todo funciona por el
        /// camino de cálculo de siempre: la materialización es opcional.
        /// </summary>
        public static bool EstadoHidricoTablaExiste() {
            if (existeTablaEstadoHidrico != null)
                return existeTablaEstadoHidrico.Value;
            try {
                using (var db = Conexion.Nueva())
                    existeTablaEstadoHidrico = (db.SingleOrDefault<int?>(
                        "SELECT COUNT(*) FROM sys.tables WHERE name='EstadoHidricoUC'") ?? 0) > 0;
            } catch (Exception ex) {
                Log.Aviso("No se pudo comprobar si existe la tabla EstadoHidricoUC", ex);
                existeTablaEstadoHidrico = false;
            }
            return existeTablaEstadoHidrico.Value;
        }

        /// <summary>
        /// Olvida lo que sabe de la tabla. Se llama tras ejecutar el script que la crea,
        /// para no tener que reiniciar la aplicación.
        /// </summary>
        public static void EstadoHidricoTablaOlvidaComprobacion() {
            existeTablaEstadoHidrico = null;
        }
    }
}
