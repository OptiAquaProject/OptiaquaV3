# OptiAqua

Aplicación para la optimización del regadío. Calcula, día a día y para cada unidad de
cultivo, cuánta agua queda disponible en el suelo y cuándo conviene regar, a partir del
**balance hídrico FAO-56** alimentado con los datos climáticos del **SIAR de La Rioja**,
la textura del suelo de cada parcela y los riegos ya aplicados.

La usan tres perfiles:

| Perfil | Qué ve |
|---|---|
| **Regante** | El estado de sus unidades de cultivo y la recomendación de riego. |
| **Gestor** | Lo mismo, para los regantes a los que representa. |
| **Administrador** | Cuadro de mando, gestión de regantes, parcelas, temporadas y mapas, y el laboratorio de cálculo. |

Además de la web hay una **API REST** (`/api/…`, documentada en `/swagger`) que es la que
consume la aplicación móvil.

---

## Cómo se calcula

El motor es un balance de agua en el suelo, día a día, desde la siembra:

```
agotamiento(D) = agotamiento(D-1) + ETc(D) − lluvia efectiva(D) − riego efectivo(D) + drenaje(D)
```

- **ETc = ETo × Kc ajustado × Ks**. La ETo viene del SIAR; el Kc, de la etapa de desarrollo
  en curso; el Ks es el coeficiente de estrés, que frena el consumo cuando el suelo se seca.
- **Capacidad de campo y punto de marchitez** salen de la textura de cada horizonte
  (arena, limo, arcilla, materia orgánica y elementos gruesos) por las ecuaciones de
  **Saxton-Rawls**, integrando por horizontes hasta donde llega la raíz.
- Las **etapas de desarrollo** avanzan por integral térmica o por días, según el cultivo.

De ahí salen el índice de estrés, los días que quedan hasta el próximo riego y el tiempo
de riego recomendado.

El detalle está en [`COMO-SE-CALCULA.md`](COMO-SE-CALCULA.md), la auditoría del modelo en
[`AUDITORIA-CALCULO.md`](AUDITORIA-CALCULO.md) y las correcciones aplicadas en
[`CORRECCIONES-CALCULO.md`](CORRECCIONES-CALCULO.md).

### El cálculo se completa siempre que puede, y lo dice

Cuando falta un dato, el cálculo no se abandona: se apaña y **queda anotado**. Si no hay
clima de un día se usa la media histórica de ese mes; si un parámetro de etapa no está, se
sigue sin él. Cada resultado lleva su lista de incidencias, que la pantalla enseña: ámbar
cuando hay número pero se apoya en estimaciones, rojo solo cuando no ha habido forma de
calcular. Un balance con datos inventados tiene la misma pinta que uno bueno, y esa es
precisamente la confusión que las incidencias evitan.

### Una pasada diaria

Todas las mañanas a las 9:00 (hora de España, fijada explícitamente) se ejecuta, en este
orden: descarga del SIAR → recálculo de suelos → estado hídrico de todas las unidades de
cultivo. El orden importa, porque el balance lee suelo y clima. Del SIAR se piden varios
días atrás, porque publica correcciones, pero **solo se escribe lo que ha cambiado de
verdad**, y ese cambio es lo que invalida los resultados guardados.

---

## LAB-ONE

Un banco de pruebas del cálculo. Se copia una unidad de cultivo entera a memoria y desde
ahí se puede cambiar **cualquier** dato de entrada —superficie, pluviometría, eficiencia
de riego, profundidad de raíz, etapas, horizontes de suelo, serie climática, riegos— y
recalcular las veces que haga falta. Nada de lo que se hace en LAB-ONE llega a la base de
datos. Los ensayos se guardan en JSON, que es también la forma de compartirlos.

Sirve para contestar preguntas del tipo «¿y si el año hubiera sido un 20% más seco?» o
«¿cuánto cambia la recomendación si el suelo tuviera un horizonte más?» sin tocar nada.

---

## Puesta en marcha

```bash
dotnet build
```

**Los secretos no están en el repositorio y la aplicación se niega a arrancar sin ellos.**
Hay que definir la cadena de conexión y la clave de firma de los JWT en los secretos de
usuario o en variables de entorno:

```bash
dotnet user-secrets set "ConnectionStrings:OptiAqua" "Data Source=...;Initial Catalog=OptiAquaV2;..." --project OptiAqua.Api
dotnet user-secrets set "Jwt:ClaveSecreta" "<una clave larga y aleatoria>" --project OptiAqua.Api
```

Las variables equivalentes son `ConnectionStrings__OptiAqua` y `Jwt__ClaveSecreta`.

```bash
dotnet run --project OptiAqua.Api
```

Los scripts de migración de la base de datos están en [`sql/`](sql); se aplican a mano.

---

## Cómo está montado

.NET 10, ASP.NET Core MVC para la web y controladores de API para el móvil, **NPoco**
sobre SQL Server. Sin frameworks de JavaScript: las gráficas se dibujan en SVG en el
servidor y el mapa usa Leaflet servido desde `wwwroot`.

```
OptiAqua.Api/
  Controllers/Api/     endpoints REST que consume el móvil
  Controllers/Web/     pantallas: cuadro de mando, panel, importación, LAB-ONE
  Servicios/           el motor: BalanceHidrico, CalculosHidricos, DatosHidricos,
                       caché, SIAR, materialización del estado hídrico
  Datos/               acceso a datos (DB, en parciales por dominio)
  Modelos/             POCOs y modelos de pantalla
  Seguridad/           autenticación por cookie (web) y JWT/clave de API (móvil)
  Infraestructura/     conexión, arranque, tareas programadas, registro
  Views/               Razor
sql/                   scripts de migración
```

Autenticación: la web va por **cookie**; la API, por **JWT o clave de API**. Una pantalla
MVC tiene que declarar `AuthenticationSchemes = "Cookies"` explícitamente, porque la
política por defecto es la de la API y si no responde 401.

---

## Estado

En desarrollo activo. Lo pendiente y lo decidido está anotado en
[`MIGRACION.md`](MIGRACION.md) y en los documentos de cálculo.
