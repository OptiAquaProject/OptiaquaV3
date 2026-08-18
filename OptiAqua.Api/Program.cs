using System.Text;
using DatosOptiaqua;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using OptiAqua.Api.Infraestructura;
using Quartz;
using Serilog;
using Serilog.Events;
using System.Security.Claims;

// ---------------------------------------------------------------------------------------
// Registro de incidencias. Se configura ANTES que nada para que cualquier fallo durante el
// arranque quede escrito. Sustituye al Log de fichero de la fase 1 y a la ausencia total de
// registro que había antes de ella.
// ---------------------------------------------------------------------------------------
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .WriteTo.Console()
    .WriteTo.File(
        Path.Combine(AppContext.BaseDirectory, "Logs", "optiaqua-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 60,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try {
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    // -----------------------------------------------------------------------------------
    // Configuración
    // -----------------------------------------------------------------------------------
    // Sobrescritura local: `appsettings.local.json`. Es donde va la cadena de conexión de la
    // máquina de cada uno para depurar, sin tener que acordarse de los secretos de usuario ni
    // exportar variables. Sin entorno en el nombre a propósito: vale igual arrancando desde
    // Visual Studio (Development) que con `dotnet run` a secas (Production). Se añade el
    // ÚLTIMO para que mande sobre todo lo demás: si el fichero existe es porque alguien lo ha
    // puesto ahí a mano, y esa es la intención más explícita que hay.
    //
    // EL REPOSITORIO ES PÚBLICO y este fichero lleva credenciales: no puede entrar en git
    // nunca. Está en .gitignore, y hay un ejemplo sin secretos en appsettings.local.ejemplo.json.
    builder.Configuration.AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true);

    var config = builder.Configuration;
    string cadenaConexion = config.GetConnectionString("OptiAqua");
    if (string.IsNullOrWhiteSpace(cadenaConexion))
        throw new InvalidOperationException(
            "No hay cadena de conexión. Defina ConnectionStrings:OptiAqua en " +
            "appsettings.local.json (copie appsettings.local.ejemplo.json), en variables de " +
            "entorno o en los secretos de usuario.");

    string jwtClave = config["Jwt:ClaveSecreta"];
    if (string.IsNullOrWhiteSpace(jwtClave))
        throw new InvalidOperationException(
            "No hay clave de firma JWT. Defina Jwt:ClaveSecreta fuera del control de versiones.");

    string jwtEmisor = config["Jwt:Emisor"];
    string jwtAudiencia = config["Jwt:Audiencia"];

    // La conexión deja de crearse con "new Database(...)" suelto en cada operación.
    Conexion.Configura(cadenaConexion);

    webapi.OpcionesJwt.Configura(
        jwtClave, jwtEmisor, jwtAudiencia,
        config.GetValue<int>("Jwt:CaducidadMinutos"));

    // Servicio externo de riegos. Opcional: si no está configurado no se llama y quien lo pide
    // recibe "no hay datos", que es lo que ya sabía manejar.
    OptiAqua.Api.Infraestructura.OpcionesRiegosApi.Configura(
        config["RiegosApi:Url"], config["RiegosApi:Clave"]);
    if (!OptiAqua.Api.Infraestructura.OpcionesRiegosApi.Configurado)
        Log.Warning("El servicio externo de riegos no está configurado (RiegosApi:Url / RiegosApi:Clave): no se descargarán riegos de fuera.");

    // -----------------------------------------------------------------------------------
    // Servicios
    // -----------------------------------------------------------------------------------
    builder.Services.AddControllersWithViews(opciones => {
            // Acciones heredadas de Web API 2 sin verbo explícito -> GET por defecto (ver la convención).
            opciones.Conventions.Add(new VerboGetPorDefectoConvention());
        })
        .AddNewtonsoftJson(opciones => {
            // Se mantiene Newtonsoft y estos dos ajustes porque definen el JSON que reciben
            // los clientes actuales ($id/$ref). Cambiar a System.Text.Json alteraría el
            // contrato de la API: es una decisión aparte, no parte de la migración.
            opciones.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Serialize;
            opciones.SerializerSettings.PreserveReferencesHandling = PreserveReferencesHandling.Objects;
        });

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c => {
        // Varias acciones definen clases de parámetros anidadas con el mismo nombre corto
        // (PostDatosExtraParam, etc.). Usar el nombre completo evita colisiones de schemaId.
        c.CustomSchemaIds(t => (t.FullName ?? t.Name).Replace("+", "."));
    });
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddMemoryCache();

    // HttpClient inyectado: evita el agotamiento de puertos del "new HttpClient()" por llamada.
    builder.Services.AddHttpClient("siar", c => c.Timeout = TimeSpan.FromSeconds(30));
    builder.Services.AddHttpClient("riegos", c => c.Timeout = TimeSpan.FromSeconds(30));
    builder.Services.AddHttpClient("sigpac", c => c.Timeout = TimeSpan.FromSeconds(30));

    builder.Services.AddAuthentication(opciones => {
            // La WEB (navegador) se identifica con cookie de sesión; la API móvil sigue con
            // JWT o clave de API. Por eso el esquema por defecto es la cookie: las páginas MVC
            // leen la identidad de la cookie y los retos de autorización redirigen al login.
            opciones.DefaultScheme = "Cookies";
            opciones.DefaultChallengeScheme = "Cookies";
        })
        .AddCookie("Cookies", opciones => {
            opciones.LoginPath = "/Cuenta/Login";
            opciones.AccessDeniedPath = "/Cuenta/Login";
            opciones.LogoutPath = "/Cuenta/Logout";
            opciones.ExpireTimeSpan = TimeSpan.FromHours(8);
            opciones.SlidingExpiration = true;
            opciones.Cookie.Name = "OptiAqua.Auth";
        })
        .AddJwtBearer(opciones => {
            opciones.MapInboundClaims = false;   // los claims llegan tal y como se emiten
            opciones.TokenValidationParameters = new TokenValidationParameters {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtEmisor,
                ValidAudience = jwtAudiencia,
                // UTF8 explícito: en .NET Framework, Encoding.Default era la ANSI del sistema.
                // La clave es ASCII, así que los bytes coinciden y los tokens ya emitidos siguen valiendo.
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtClave)),
                RoleClaimType = ClaimTypes.Role,
                NameClaimType = "NifRegante",
                ClockSkew = TimeSpan.FromMinutes(1)
            };
        })
        // Autenticación por clave de API, para integraciones que no pueden mantener sesión.
        // Emite los mismos claims que el token, así que hereda el control de acceso existente.
        .AddScheme<AuthenticationSchemeOptions, AutenticacionApiKey>(AutenticacionApiKey.Esquema, null);

    builder.Services.AddAuthorization(opciones => {
        // Se admite indistintamente token JWT o clave de API en todo lo que exija [Authorize].
        var ambosEsquemas = new AuthorizationPolicyBuilder(
                JwtBearerDefaults.AuthenticationScheme,
                AutenticacionApiKey.Esquema)
            .RequireAuthenticatedUser()
            .Build();
        opciones.DefaultPolicy = ambosEsquemas;

        opciones.AddPolicy("Administrador", p => p
            .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme, AutenticacionApiKey.Esquema)
            .RequireAuthenticatedUser()
            .RequireRole("admin"));
    });

    // CORS: la lista de orígenes se configura, ya no está fijada a "*" en el código.
    string[] origenes = config.GetSection("Cors:Origenes").Get<string[]>() ?? Array.Empty<string>();
    builder.Services.AddCors(opciones => {
        opciones.AddDefaultPolicy(p => {
            if (origenes.Length == 0 || origenes.Contains("*")) {
                Log.Warning("CORS admite cualquier origen. Conviene fijar Cors:Origenes con los dominios legítimos.");
                p.AllowAnyOrigin();
            } else {
                p.WithOrigins(origenes);
            }
            p.AllowAnyHeader().AllowAnyMethod();
        });
    });

    // UNA sola tarea diaria, a las 9:00: clima del SIAR, suelos y estado hídrico, en ese
    // orden. Antes había una tarea que solo rehacía balances y los suelos se lanzaban a mano
    // desde el panel; como el balance lee el suelo y el clima, hacerlo en tres momentos
    // distintos dejaba resultados de ayer con fecha de hoy. La hora se puede cambiar con
    // Tareas:CronRecalculo (formato cron de Quartz, con segundos delante).
    // La ZONA HORARIA va fijada a propósito. Sin ella Quartz usa la del servidor, y un VPS
    // Linux normalmente va en UTC: "las 9:00" acabarían siendo las 11:00 de aquí en verano y
    // las 10:00 en invierno, moviéndose solas dos veces al año. La aplicación es de riego en
    // La Rioja; la hora que importa es la de aquí, no la de la máquina.
    var zonaTareas = ZonaHoraria(config["Tareas:ZonaHoraria"] ?? "Europe/Madrid");
    builder.Services.AddQuartz(q => {
        var clave = new JobKey("RecalculoDiario");
        q.AddJob<TareaRecalculoDiario>(opciones => opciones.WithIdentity(clave));
        q.AddTrigger(opciones => opciones
            .ForJob(clave)
            .WithIdentity("RecalculoDiario-disparador")
            .WithCronSchedule(config["Tareas:CronRecalculo"] ?? TareaRecalculoDiario.CronPorDefecto,
                              x => x.InTimeZone(zonaTareas)));
        // Segundo disparador, una sola vez al arrancar: recupera la pasada si el proceso no
        // estaba vivo a su hora. El planificador es en memoria y muere con el proceso; en IIS
        // el grupo de aplicaciones se apaga por inactividad a los 20 minutos, y de madrugada
        // no entra nadie. La tarea comprueba por su cuenta si hace falta y, si no, no hace
        // nada: el minuto de espera es para no competir con el arranque.
        q.AddTrigger(opciones => opciones
            .ForJob(clave)
            .WithIdentity(TareaRecalculoDiario.DisparadorArranque)
            .StartAt(DateTimeOffset.Now.AddMinutes(1))
            .WithSimpleSchedule(x => x.WithRepeatCount(0)));
    });
    builder.Services.AddQuartzHostedService(opciones => opciones.WaitForJobsToComplete = true);

    builder.Services.AddHealthChecks().AddCheck<ComprobacionBaseDatos>("base-de-datos");

    // -----------------------------------------------------------------------------------
    // Tubería
    // -----------------------------------------------------------------------------------
    var app = builder.Build();

    // Un fallo no controlado se registra con el identificador de la petición y al cliente
    // se le devuelve ese identificador, no el detalle interno. Sustituye a los
    // "BadRequest(ex.Message)" repartidos por todos los controladores.
    app.UseMiddleware<ManejadorDeErrores>();

    app.UseSerilogRequestLogging();
    app.UseStaticFiles();
    app.UseCors();
    app.UseAuthentication();
    app.UseAuthorization();

    // Documentación interactiva de la API. En desarrollo, siempre; fuera de desarrollo solo si
    // se pide explícitamente con Swagger:Habilitado, y entonces **detrás de la sesión de
    // administrador**: publica la superficie entera de la API, y en una instalación abierta a
    // los regantes eso es un plano del edificio para cualquiera que pase por la URL.
    //
    // El estado se guarda para que la barra de navegación no enseñe el enlace cuando no lleva
    // a ninguna parte, que es lo que pasaba antes en producción.
    bool swaggerHabilitado = app.Environment.IsDevelopment()
                             || config.GetValue<bool>("Swagger:Habilitado");
    bool swaggerSoloAdmin = swaggerHabilitado && !app.Environment.IsDevelopment();
    OptiAqua.Api.Infraestructura.OpcionesSwagger.Configura(swaggerHabilitado, swaggerSoloAdmin);

    if (swaggerHabilitado) {
        if (swaggerSoloAdmin) {
            // Va aquí y no como filtro porque Swagger es middleware, no un endpoint: no hay
            // dónde colgarle un [Authorize]. Se mira la identidad que ya dejó UseAuthentication.
            app.UseWhen(
                ctx => ctx.Request.Path.StartsWithSegments("/swagger"),
                rama => rama.Use(async (ctx, siguiente) => {
                    if (ctx.User?.Identity?.IsAuthenticated != true) {
                        ctx.Response.Redirect("/Cuenta/Login");
                        return;
                    }
                    if (!ctx.User.IsInRole("admin")) {
                        ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                        return;
                    }
                    await siguiente();
                }));
        }
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.MapControllers();
    app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");
    app.MapHealthChecks("/health");

    Log.Information("OptiAqua API iniciada. Entorno: {Entorno}", app.Environment.EnvironmentName);
    app.Run();
} catch (Exception ex) {
    Log.Fatal(ex, "La aplicación no pudo arrancar");
    throw;
} finally {
    Log.CloseAndFlush();
}

/// <summary>
/// Zona horaria para las tareas programadas. Se admite tanto el identificador IANA
/// ("Europe/Madrid") como el de Windows ("Romance Standard Time"): .NET convierte entre
/// ambos, pero no en todas las máquinas, así que se prueban los dos antes de rendirse.
/// Si ninguno existe se usa la del servidor y se avisa: la aplicación tiene que arrancar.
/// </summary>
static TimeZoneInfo ZonaHoraria(string id) {
    foreach (var candidato in new[] { id, "Europe/Madrid", "Romance Standard Time" }) {
        try {
            return TimeZoneInfo.FindSystemTimeZoneById(candidato);
        } catch (Exception) {
            // se prueba el siguiente
        }
    }
    Log.Warning("No se encontró la zona horaria '{Zona}'; las tareas usarán la del servidor ({Local})",
                id, TimeZoneInfo.Local.Id);
    return TimeZoneInfo.Local;
}
