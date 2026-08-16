# OptiAqua

Aplicación para la optimización del regadío. Calcula, día a día y para cada unidad de
cultivo, cuánta agua queda disponible en el suelo y cuándo conviene regar, a partir del
**balance hídrico FAO-56** alimentado con los datos climáticos del **SIAR de La Rioja**, la
textura del suelo de cada parcela y los riegos ya aplicados.

La usan los regantes (el estado de sus parcelas y la recomendación de riego), los gestores
que les representan y los administradores. Además de la web hay una **API REST** (`/api/…`,
documentada en `/swagger`) que es la que consume la aplicación móvil.

## Cómo se calcula

Un balance de agua en el suelo, día a día, desde la siembra:

```
agotamiento(D) = agotamiento(D-1) + ETc(D) − lluvia efectiva(D) − riego efectivo(D) + drenaje(D)
```

**ETc = ETo × Kc ajustado × Ks**: la ETo viene del SIAR, el Kc de la etapa de desarrollo en
curso y el Ks frena el consumo cuando el suelo se seca. La capacidad de campo y el punto de
marchitez salen de la textura de cada horizonte por las ecuaciones de **Saxton-Rawls**,
integrando hasta donde llega la raíz.

Todas las mañanas a las 9:00 se descarga el SIAR, se recalculan los suelos y se actualiza el
estado hídrico de todas las unidades de cultivo, en ese orden, porque el balance lee suelo y
clima.

Cuando falta un dato el cálculo no se abandona: se apaña —la media histórica del mes si no
hay clima— y **queda anotado**. Cada resultado lleva su lista de incidencias, en ámbar cuando
el número se apoya en estimaciones y en rojo solo cuando no ha habido forma de calcular.

El detalle está en [`COMO-SE-CALCULA.md`](COMO-SE-CALCULA.md).

## LAB-ONE

Un banco de pruebas del cálculo. Copia una unidad de cultivo entera a memoria y permite
cambiar cualquier dato de entrada —suelo, clima, etapas, riegos, superficie— y recalcular
las veces que haga falta, sin escribir nada en la base de datos. Los ensayos se guardan en
JSON.

## Puesta en marcha

```bash
cp OptiAqua.Api/appsettings.local.ejemplo.json OptiAqua.Api/appsettings.local.json
dotnet run --project OptiAqua.Api
```

**Los secretos no están en el repositorio y la aplicación se niega a arrancar sin ellos.**
`appsettings.local.json` está en `.gitignore`, no se copia al publicar y manda sobre todo lo
demás. En un servidor, por variables de entorno (`ConnectionStrings__OptiAqua`,
`Jwt__ClaveSecreta`) o por secretos de usuario.

Los scripts de migración de la base de datos están en [`sql/`](sql) y se aplican a mano.

## Cómo está montado

.NET 10, ASP.NET Core MVC para la web y controladores de API para el móvil, **NPoco** sobre
SQL Server, **Quartz** para la pasada diaria. Sin frameworks de JavaScript: las gráficas se
dibujan en SVG en el servidor y el mapa usa Leaflet servido desde `wwwroot`.

```
OptiAqua.Api/
  Controllers/     Api/ para el móvil, Web/ para las pantallas
  Servicios/       el motor de cálculo, la caché, el SIAR
  Datos/           acceso a datos, en parciales por dominio
  Modelos/         POCOs y modelos de pantalla
  Seguridad/       cookie para la web, JWT o clave de API para el móvil
  Infraestructura/ arranque, conexión, tareas programadas, registro
```

Una pantalla MVC tiene que declarar `AuthenticationSchemes = "Cookies"` explícitamente: la
política por defecto es la de la API y si no responde 401.

## Estado

En desarrollo activo. Ver [`MIGRACION.md`](MIGRACION.md).
