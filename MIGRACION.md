# Migración a .NET 10 — estado

El proyecto viene de una aplicación ASP.NET MVC 5 + Web API 2 sobre .NET Framework 4.8. La
versión actual (`OptiAqua.Api/`) está reescrita sobre ASP.NET Core 10, y el original se
conserva fuera de este repositorio hasta terminar de validarla.

## Qué se ha portado y qué se ha reescrito

**Sin un solo cambio**: la lógica agronómica y el modelo de datos. El balance hídrico, los
cálculos, la descarga del SIAR, el cifrado, las utilidades y los modelos de importación
compilaron tal cual. El trabajo estuvo entero en la capa web.

**Reescrito**:

| Antes | Ahora |
|---|---|
| `Global.asax` + `App_Start/*` | `Program.cs` |
| Validación de token a mano | Middleware `JwtBearer` estándar |
| Quartz montado a mano | `AddQuartzHostedService` |
| `ConfigurationManager` | `IConfiguration` |
| `ApiController` / `IHttpActionResult` | `ControllerBase` / `IActionResult` |
| `Thread.CurrentPrincipal` | `HttpContext.User` |
| `System.Data.SQLite` | `Microsoft.Data.Sqlite` |
| Bundles de `System.Web.Optimization` | Ficheros estáticos desde `wwwroot` |
| Sin registro de errores | Serilog a consola y a fichero rodado por día |

Las contraseñas y los token ya emitidos siguen siendo válidos tras la migración: se comprobó
que el esquema de cifrado da el mismo resultado en .NET 10.

## Criterio de arranque

La aplicación **corre siempre que puede e informa del problema**, en vez de morir al
arrancar. Con la base de datos inaccesible, arranca igual y `/health` responde 503.

La única excepción es la configuración sensible: si falta la cadena de conexión o la clave de
firma, se niega a arrancar en lugar de tirar de un valor por defecto.

## Pendiente antes de sustituir al proyecto anterior

1. Validar el mapeo de NPoco 6 contra el esquema real, sobre todo los modelos de clave
   compuesta.
2. Probar la importación de mapas `.gpkg`, que cambia de proveedor SQLite.
3. Decidir sobre el serializador JSON: se ha conservado Newtonsoft con
   `PreserveReferencesHandling.Objects` para no cambiarle el contrato a los clientes ya
   desplegados. Pasar a `System.Text.Json` es una decisión aparte.
4. Fijar `Cors:Origenes` con los dominios reales.
5. Publicación y alojamiento: Kestrel tras IIS o nginx, y el despliegue.

Los scripts de base de datos pendientes están en [`sql/`](sql) y se aplican a mano.

---

*El seguimiento detallado de la auditoría —fallos concretos, endpoints afectados y estado de
cada corrección— se lleva fuera de este repositorio.*
