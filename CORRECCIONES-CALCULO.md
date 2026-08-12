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

---

## Validación contra la base de datos real (12/08/2026)

Ejecutado el balance real (código del proyecto, no réplica) sobre cuatro unidades de cultivo reales con suelo multi-horizonte. **Corre sin fallar y produce valores con sentido físico** (viña 2766_1V, raíz 1,5 → CC 390 mm, TAW 202 mm; remolacha, raíz 0,5 → CC ~110 mm, TAW ~50 mm). La clasificación de estrés (C4) sale coherente y ordenada ("Estrés severo", "Exceso de agua", "sin estrés…"). No hay NaN ni excepciones. Las correcciones no rompen nada.

### C9 — Unidades incoherentes raíz (m) vs suelo (cm): solo se usa el primer horizonte 🔴 NUEVO

Al mirar los números salió a la luz un fallo mayor que C1. La profundidad de raíz sale en **metros** (`ProfRaizMax` = 0,5 remolacha / 1,5 viña, y `LongitudRaiz` no pasa de ahí), pero `DatosSuelo.ProfundidadCM` está en **centímetros** (30–100). La integración compara ambas directamente (`root` contra `ProfundidadCM`), así que como la raíz (0,5–1,5) nunca alcanza el primer límite de horizonte (≈30), **el recorrido se detiene siempre en el primer horizonte**:

`CapacidadCampo = 109,95` con `LongitudRaiz = 0,5` = exactamente `0,5 × 1000 × 0,2199` (contenido del primer horizonte). Todos los horizontes por debajo del primero (hasta 10 en algunas unidades) **no intervienen nunca** en el cálculo.

Dos efectos encadenados:
- El factor `× 1000` de la integración es correcto para raíz en **metros**; por eso los mm salen en un rango plausible pese a todo. Es decir, la magnitud "cuadra" por casualidad, integrando solo la textura del horizonte superior en toda la zona radicular.
- **C1 queda inerte con los datos actuales**: la corrección de espesor vs profundidad acumulada es correcta, pero no cambia el resultado porque ni el bucle viejo ni el nuevo llegan al segundo horizonte. C1 volverá a importar en cuanto se arregle C9.

C9 no se corrige aquí porque es una decisión de modelo/datos: hay que definir en qué unidad se guardan `ProfRaizInicial`/`ProfRaizMax` (parecen metros) y `ProfundidadCM` (cm), y ajustar la conversión (¿raíz × 100 para pasar a cm y usar `× 10` en la integración? ¿o dejar todo en metros?). Cambiarlo mueve **todas** las recomendaciones de riego, así que necesita el criterio del agrónomo y validación, no un parche. Es, con diferencia, el hallazgo de mayor impacto del cálculo: hoy la recomendación de riego depende **solo de la textura del suelo superficial**.

> Nota: para esta prueba se fijaron en la tabla `Configuracion` las marcas `FechaUltimaActualizacionSiar` y `FechaUltimaActualizacionApiRiegos` a 2026-08-12, para que el balance no dispare llamadas de red al SIAR ni a la API de riegos (la BD es de 2025). Son inocuas; se pueden borrar si se quiere que el dev vuelva a refrescar del SIAR.

---

## C9 — corrección preparada (rama `fix-c9`)

A petición, preparado el arreglo bajo el supuesto **raíz en metros → cm**. Un solo cambio, en `IntegraPorHorizontes`: la profundidad de raíz se pasa a cm (`× 100`) para compararla con `ProfundidadCM` (cm), y el factor de agua pasa de `× 1000` (correcto para metros) a `× 10` (correcto para cm). `LongitudRaiz` sigue publicándose en metros en la API; solo cambia la integración del suelo.

Nota sobre la magnitud: el coeficiente total de profundidad no cambia (50 cm × 10 = 0,5 m × 1000 = 500), así que **para raíces dentro del perfil de suelo el efecto es pequeño** — lo que cambia es que ahora cada horizonte aporta su propia textura en vez de extrapolar la superficial. Donde cambia mucho es cuando la raíz supera el suelo medido.

Antes/después sobre datos reales (balance completo, código del proyecto):

| Unidad (raíz) | CC antes | CC después | TAW antes | TAW después | Δ TAW |
|---|---|---|---|---|---|
| 3122_R1 remolacha (0,5 m, suelo→92 cm) | 109,95 | 111,04 | 48,57 | 51,11 | +5 % |
| 2733_R1 remolacha (0,5 m) | 116,45 | 114,94 | 57,15 | 56,26 | −2 % |
| 2747_R4 remolacha (0,5 m) | 127,15 | 124,50 | 64,90 | 63,61 | −2 % |
| 2766_1V viña (1,5 m, suelo→100 cm) | 390,34 | 268,90 | 201,56 | 143,75 | **−29 %** |

En remolacha (raíz 0,5 m = 50 cm, dentro del perfil que llega a ~92 cm) el cambio es pequeño: ahora se usan de verdad los horizontes hasta 50 cm con su textura. **Con C9 corregido, C1 vuelve a tener efecto** (por eso se mueven un poco).

El caso grande es la **viña**: raíz 1,5 m = 150 cm, pero el perfil de suelo solo llega a 100 cm. Antes se integraban 150 cm de textura superficial; ahora se integran los 100 cm reales (multi-horizonte) y se detiene ahí. CC y TAW bajan un 29 %.

### Decisión pendiente (sub-caso raíz > suelo medido)

Cuando la raíz es más profunda que el último horizonte con datos (viña), esta corrección **se detiene en el suelo medido** — no extrapola por debajo de la información disponible. La alternativa sería **prolongar la textura del último horizonte** hasta la profundidad de raíz. Son criterios agronómicos distintos con resultados distintos (la viña quedaría entre 269 mm —cap actual— y ~404 mm —extrapolando—). Hay que decidir cuál, e idealmente confirmar que `ProfRaizMax`/`ProfRaizInicial` están efectivamente en metros para todos los cultivos, no solo remolacha y viña.

**Estado:** en rama `fix-c9`, sin fusionar, para validación.

### Revisión de TODOS los valores de raíz (tabla Cultivo)

Los 20 cultivos tienen `ProfRaizInicial`/`ProfRaizMax` en el rango 0,05–1,5: **todos en metros, ninguno cargado en cm**. El arreglo de C9 (raíz m→cm) es seguro para toda la tabla.

Cruzado con la profundidad de suelo (perfiles de 60 a 140 cm; la mayoría 90–100), la sub-decisión "raíz > suelo medido" solo afecta materialmente a:

| Cultivo | ProfRaizMax | Parcelas (UCxTemp) | ¿Raíz supera el suelo? |
|---|---|---|---|
| VIÑA | 1,5 m | 269 | Sí casi siempre (suelo ≤1,4 m) — el caso −29% |
| Alfalfa ciclo largo | 1,0 m | 4 | A veces (perfiles <90 cm) |
| Maíz ciclo medio | 0,6 m | 2 | No (suelo mínimo 60 cm) |
| Resto (remolacha, patata, zanahoria, adormidera, guisante…) | ≤0,5 m | ~4.600 | No, cabe en cualquier perfil |

Conclusión: para la práctica totalidad de las parcelas (raíz ≤0,5 m) el arreglo solo mejora el reparto multi-horizonte (C1, cambios pequeños). La decisión cap-vs-extrapolar se reduce esencialmente a las **269 parcelas de viña**.
