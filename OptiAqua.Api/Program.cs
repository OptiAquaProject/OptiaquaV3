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
    var config = builder.Configuration;
    string cadenaConexion = config.GetConnectionString("OptiAqua");
    if (string.IsNullOrWhiteSpace(cadenaConexion))
        throw new InvalidOperationException(
            "No hay cadena de conexión. Defina ConnectionStrings:OptiAqua en appsettings, " +
            "en variables de entorno o en los secretos de usuario.");

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

    // Recálculo diario a las 8:00, antes en Quartz arrancado a mano desde Application_Start.
    builder.Services.AddQuartz(q => {
        var clave = new JobKey("RecalculoDiario");
        q.AddJob<TareaRecalculoDiario>(opciones => opciones.WithIdentity(clave));
        q.AddTrigger(opciones => opciones
            .ForJob(clave)
            .WithIdentity("RecalculoDiario-disparador")
            .WithCronSchedule(config["Tareas:CronRecalculo"] ?? "0 0 8 * * ?"));
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

    if (app.Environment.IsDevelopment()) {
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
