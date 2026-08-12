# Correcciones del cálculo hídrico

Fecha: 2026-08-12
Rama: `fix-calculo`. Sobre el proyecto `OptiAqua.Api` (.NET 10). Responde a los hallazgos de `AUDITORIA-CALCULO.md`.

Se han corregido **seis** de los ocho hallazgos. Los dos restantes (C5 y C8) se dejan pendientes a propósito, con su motivo abajo.

> Compila sin errores. Cada corrección se ha comprobado con un banco de pruebas numérico que replica fielmente las fórmulas reales (incluida la pedotransferencia de Saxton-Rawls) y compara el antes y el después. **Sigue faltando la validación contra la base de datos real**, que no hay en esta máquina.

---

## C1 — Integración del suelo por horizontes 🔴 CORREGIDO

`CalculosHidricos.cs`. `CapacidadCampo(root, list)` y `PuntoMarchitez(root, list)` pasan a integrar por **espesor real** de cada horizonte (`base − techo`), en vez de tratar la profundidad acumulada como espesor. Ambas comparten ahora un helper único, `IntegraPorHorizontes`, para que no puedan volver a divergir.

Antes/después con un perfil realista de tres horizontes (0-30 / 30-70 / 70-110 cm), agua útil TAW = CC − PM:

| Raíz | TAW antes | TAW después | Desviación |
|---|---|---|---|
| 25 cm | 2.620,7 | 2.620,7 | 0,0 % (un solo horizonte) |
| 50 cm | 5.298,0 | 5.298,0 | 0,0 % |
| 90 cm | 9.604,3 | 9.008,4 | **−6,2 %** |
| 110 cm | 11.459,5 | 10.565,7 | **−7,8 %** |

El signo depende del perfil; aquí la versión antigua sobreestimaba. Con un solo horizonte no hay cambio, como se esperaba.

## C2 — Respaldo climático 🔴 CORREGIDO

`DatosHidricos.cs`. Las cuatro variables (ETo, viento, humedad, temperatura) pasan por un único helper `ValorClimaticoConRespaldo(datos, fecha, selector)`, que promedia los tres días anteriores **de la misma variable**. Se elimina la duplicación que había hecho que tres de ellas cayeran por error en la temperatura.

Día sin registro, con ETo real ≈5 y temperatura ≈22:

| | ETo del día |
|---|---|
| Antes | 22,20 mm (¡la temperatura!) |
| Después | 4,97 mm |

Un factor 4,5× de diferencia en el ETo, que se propagaba a la ETc y a la recomendación de riego. Igual para viento y humedad.

## C3 — Umbrales de riego por Id 🟠 CORREGIDO

`DatosHidricos.cs`. `UmbralSuperiorRiego` y `UmbralOptimoRiego` localizan el umbral con `Find(x => x.IdUmbral == id)` —igual que `ClaseEstresUmbralInferiorYSuperior`— en lugar de indexar la lista por el valor del Id. Unificados en un helper `UmbralRiegoPorId`.

## C4 — Orden de los umbrales de estrés 🟠 CORREGIDO

`DB.cs`. `ListaEstresUmbral()` ordena ahora cada lista por `UmbralMaximo` ascendente, que es lo que la clasificación del estrés da por supuesto al recorrerla.

## C6 — Precipitación efectiva no negativa 🟡 CORREGIDO

`CalculosHidricos.cs`. `PrecipitacionEfectiva` se acota a `≥ 0`. Antes, `precip=2,1` con `eto=12` daba −0,30 (una lluvia que secaba el suelo); ahora da 0.

## C7 — Guardas de índice y divisiones 🟡 CORREGIDO (parcial)

`DatosHidricos.cs` / `CalculosHidricos.cs`:
- `ParamGet`: la guarda pasa de `Count >= etapaBase0` a `Count > etapaBase0` (el índice válido llega a `Count−1`). Para índices válidos no cambia nada; solo evita la excepción en el caso fuera de rango.
- `Kc` por integral térmica: si `cobFin == cobIni` se devuelve `kcIni` en vez de dividir por cero (evita el `NaN` que contaminaba la ETc).
- `RecomendacionRiegoMm`: se acota a `≥ 0` (evita recomendaciones —y tiempos— de riego negativos).

Queda dentro de C7 sin tocar el cast `(double)lb.IntegralTermica` en las fórmulas de altura/cobertura, porque exige decidir qué hacer cuando un cultivo sin temperatura base usa una fórmula por integral térmica (¿error explícito? ¿tratar la integral como 0?). Es una decisión de modelo, no un arreglo mecánico.

---

## Pendientes a propósito

**C5 — Parámetros ausentes enmascarados con `?? double.MaxValue`.** No se toca porque cambiar el valor por defecto altera el resultado numérico de cultivos que hoy "funcionan" apoyándose en el `clamp`. Decidir si un parámetro que falta debe dar error, valer 0, o mantenerse como está es una decisión agronómica que conviene tomar con los datos reales delante, no a ciegas.

**C8 — Inicialización del primer día.** El aporte de agua por crecimiento de raíz del día 1 (`0,8 · TAW`) y el arranque del depósito son decisiones de modelo. Cambiarlas mueve todo el balance del primer tramo y necesita validación agronómica, no un parche.

Ambos siguen documentados en `AUDITORIA-CALCULO.md`.

---

## Verificación pendiente

1. **Recalcular contra la base de datos real** y comparar un puñado de unidades de cultivo con multi-horizonte antes/después de C1 — es el cambio de mayor efecto y el que conviene confirmar con datos.
2. Revisar que C3 y C4 no alteren clasificaciones que hoy se dan por buenas (si en producción los `IdUmbral` ya coincidían con la posición y el orden, no habrá cambio; si no, las salidas cambiarán, que es justo la corrección).
3. Estas correcciones están **solo en `OptiAqua.Api`**. El proyecto original `WebApi/` mantiene los fallos; si se sigue usando en producción mientras dure la migración, habría que portarlas también.
