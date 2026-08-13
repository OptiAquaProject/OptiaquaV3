using System;
using System.Threading;

namespace DatosOptiaqua {

    /// <summary>
    /// Foto del progreso, para enviar a la página o a un cliente sin exponer el estado vivo.
    /// </summary>
    public class EstadoRecalculo {
        public bool EnCurso { get; set; }
        public string Tarea { get; set; }
        public string Fase { get; set; }
        public int Total { get; set; }
        public int Procesados { get; set; }
        public int Errores { get; set; }
        public int Porcentaje { get; set; }
        public string Transcurrido { get; set; }
        public string Restante { get; set; }
        public string UltimoElemento { get; set; }

        /// <summary>Resultado de la última ejecución terminada (aunque ahora no haya ninguna en curso).</summary>
        public string UltimaEjecucion { get; set; }
    }

    /// <summary>
    /// Progreso del recálculo de balances hídricos.
    ///
    /// El recálculo recorre todas las unidades de cultivo de todas las temporadas y puede tardar
    /// mucho, así que no basta con saber si está corriendo: hay que poder ver por dónde va.
    ///
    /// Un único hilo escribe (el recálculo, que está serializado con Interlocked en
    /// CacheDatosHidricos) y muchos leen desde las peticiones web. Los contadores se tocan con
    /// Interlocked y las referencias son volatile, de modo que los lectores ven siempre valores
    /// coherentes sin bloquear al que trabaja.
    /// </summary>
    public static class ProgresoRecalculo {
        private static volatile bool enCurso;
        private static volatile string tarea;
        private static volatile string fase;
        private static volatile string ultimoElemento;
        private static int total;
        private static int procesados;
        private static int errores;
        private static long inicioTicks;

        // Resumen de la última ejecución terminada.
        private static volatile string ultimaEjecucion;

        public static bool EnCurso { get { return enCurso; } }

        public static void Comienza(string nombreTarea, int totalElementos) {
            tarea = nombreTarea;
            fase = "Preparando";
            ultimoElemento = null;
            Interlocked.Exchange(ref total, totalElementos);
            Interlocked.Exchange(ref procesados, 0);
            Interlocked.Exchange(ref errores, 0);
            Interlocked.Exchange(ref inicioTicks, DateTime.Now.Ticks);
            enCurso = true;
        }

        /// <summary>Ajusta el total cuando no se conocía de antemano.</summary>
        public static void FijaTotal(int totalElementos) {
            Interlocked.Exchange(ref total, totalElementos);
        }

        public static void CambiaFase(string nombreFase) {
            fase = nombreFase;
        }

        public static void Avanza(string elemento) {
            Interlocked.Increment(ref procesados);
            ultimoElemento = elemento;
        }

        public static void ApuntaError() {
            Interlocked.Increment(ref errores);
        }

        public static void Termina(bool correcto) {
            enCurso = false;
            fase = null;
            var duracion = Transcurrido();
            int hechos = Volatile.Read(ref procesados);
            int fallos = Volatile.Read(ref errores);
            ultimaEjecucion = string.Format("{0}: {1} el {2} ({3}, {4}, {5})",
                tarea ?? "Recálculo",
                correcto ? "terminado" : "interrumpido",
                DateTime.Now.ToString("dd/MM/yyyy HH:mm"),
                hechos == 1 ? "1 elemento" : hechos.ToString("N0") + " elementos",
                FormatoDuracion(duracion),
                fallos == 0 ? "sin errores" : fallos + (fallos == 1 ? " error" : " errores"));
        }

        private static TimeSpan Transcurrido() {
            long ticks = Interlocked.Read(ref inicioTicks);
            if (ticks == 0)
                return TimeSpan.Zero;
            return DateTime.Now - new DateTime(ticks);
        }

        /// <summary>Foto coherente del estado actual.</summary>
        public static EstadoRecalculo Foto() {
            int hechos = Volatile.Read(ref procesados);
            int cuantos = Volatile.Read(ref total);
            int fallos = Volatile.Read(ref errores);
            bool corriendo = enCurso;

            var estado = new EstadoRecalculo {
                EnCurso = corriendo,
                Tarea = tarea,
                Fase = fase,
                Total = cuantos,
                Procesados = hechos,
                Errores = fallos,
                UltimoElemento = ultimoElemento,
                UltimaEjecucion = ultimaEjecucion
            };

            if (!corriendo)
                return estado;

            TimeSpan transcurrido = Transcurrido();
            estado.Transcurrido = FormatoDuracion(transcurrido);
            estado.Porcentaje = cuantos > 0 ? (int)Math.Min(100, (hechos * 100L) / cuantos) : 0;

            // Estimación por regla de tres sobre el ritmo medio. Basta para saber si son
            // dos minutos o media hora, que es lo que se quiere saber.
            if (hechos > 0 && cuantos > hechos) {
                double segundosPorElemento = transcurrido.TotalSeconds / hechos;
                var restante = TimeSpan.FromSeconds(segundosPorElemento * (cuantos - hechos));
                estado.Restante = FormatoDuracion(restante);
            } else if (cuantos > 0 && hechos >= cuantos) {
                estado.Restante = "terminando";
            } else {
                estado.Restante = "calculando…";
            }
            return estado;
        }

        private static string FormatoDuracion(TimeSpan t) {
            if (t.TotalSeconds < 60)
                return ((int)t.TotalSeconds) + " s";
            if (t.TotalMinutes < 60)
                return ((int)t.TotalMinutes) + " min " + t.Seconds + " s";
            return ((int)t.TotalHours) + " h " + t.Minutes + " min";
        }
    }
}
