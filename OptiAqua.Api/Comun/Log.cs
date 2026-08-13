
namespace webapi.Utiles {
    /// <summary>
    /// Fachada de registro para el código estático heredado (DB, cachés, importación), que no
    /// puede recibir un ILogger por inyección. Por debajo escribe con Serilog, configurado en
    /// el arranque.
    ///
    /// El código nuevo debería pedir ILogger&lt;T&gt; por constructor en lugar de usar esta clase.
    /// </summary>
    public static class Log {
        public static void Error(string contexto, Exception ex) {
            Serilog.Log.Error(ex, "{Contexto}", contexto);
        }

        public static void Aviso(string contexto, Exception ex) {
            if (ex == null)
                Serilog.Log.Warning("{Contexto}", contexto);
            else
                Serilog.Log.Warning(ex, "{Contexto}", contexto);
        }

        public static void Info(string mensaje) {
            Serilog.Log.Information("{Mensaje}", mensaje);
        }
    }
}
