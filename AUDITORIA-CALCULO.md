# Auditoría en profundidad — el cálculo hídrico

> **Estado:** seis de los ocho hallazgos (C1-C4, C6, C7) están corregidos; C5 y C8 quedan pendientes a propósito. La validación contra la BD real (12/08/2026) destapó **C9**, más grave que todos: la profundidad de raíz está en metros y el suelo en cm, así que **solo se usa el primer horizonte** y la recomendación de riego depende solo de la textura superficial. C1 queda inerte hasta arreglar C9. Detalle y validación en `CORRECCIONES-CALCULO.md`.

Fecha: 2026-08-12
Ámbito: `LogicaAgronoma/BalanceHidrico.cs`, `LogicaAgronoma/CalculosHidricos.cs`, `Datos/DatosHidricos.cs`, `Utiles/ParametrosCalculos.cs` y los modelos implicados (`LineaBalance`, `DatosSuelo`, `UnidadCultivoCultivoEtapas`, `Cultivo`, `TipoEstresUmbral`).

El modelo es un balance de agua en el suelo tipo **FAO-56**: pedotransferencia de Saxton-Rawls (2006) para capacidad de campo y punto de marchitez, curvas de coeficiente de cultivo Kc, factor de agotamiento *p*, coeficiente de estrés hídrico Ks, ETc = ETo·Kc·Ks, drenaje en profundidad, precipitación efectiva y recomendación de riego. Se calcula una línea por día desde la siembra, arrastrando el estado del día anterior.

Las fórmulas *base* (Saxton-Rawls, el ajuste climático de Kc, el factor de agotamiento, Ks) **están bien transcritas** respecto a FAO-56. Los problemas están en cómo se alimentan y cómo se combinan, no en las ecuaciones en sí.

> **Sin datos reales.** No hay base de datos accesible en esta máquina, así que estas conclusiones salen de leer el código y del modelo, no de comparar salidas contra casos reales. El hallazgo C1 se ha reproducido con un banco de pruebas numérico aislado; el resto está razonado sobre el código y marcado como tal. **Conviene confirmarlo con datos de producción antes de corregir.**

---

## C1 — La geometría del suelo por horizontes está mal integrada 🔴

`CalculosHidricos.CapacidadCampo(double root, List<DatosSuelo>)` y su gemela `PuntoMarchitez(...)` (`CalculosHidricos.cs:419` y `:468`) recorren los horizontes del suelo así:

```csharp
if (profRestante > pParcelaSuelo[i].ProfundidadCM)
    ret += pParcelaSuelo[i].ProfundidadCM * 1000 * c;   // usa ProfundidadCM como ESPESOR
else
    ret += profRestante * 1000 * c;
profRestante -= pParcelaSuelo[i].ProfundidadCM;          // resta ProfundidadCM como ESPESOR
```

Tratan `ProfundidadCM` como el **espesor** de cada horizonte. Pero ese campo es la **profundidad acumulada desde la superficie** — el propio modelo lo documenta: `DatosSuelo.ProfundidadCM` → *"distancia desde superficie (se acumulan la profundidad de los horizontes)"* (`Models.cs:127`), y así lo rellenan los dos productores (`UnidadCultivoSueloListNew` y `DatosSueloComunidadRegantes` en `DB.cs`, que asignan `ProfundidadCM = hEstudio`, el límite inferior del horizonte).

Consecuencia: para un perfil de horizontes 0-30 / 30-70 / 70-100 cm (almacenados como 30, 70, 100):

- El primer horizonte sale bien (acumulado = espesor).
- El segundo se cuenta con 70 cm de espesor en vez de 40.
- El tercero **no se alcanza nunca**, porque al restar 30 y luego 70 el `profRestante` llega a 0.

Reproducido con un banco de pruebas aislado (contenidos 0,30 / 0,20 / 0,10 m³/m³):

| Profundidad de raíz | Código | Correcto | Desviación |
|---|---|---|---|
| 25 cm | 7.500 | 7.500 | 0,0 % |
| 50 cm | 13.000 | 13.000 | 0,0 % |
| 80 cm | 19.000 | 18.000 | +5,6 % |
| 100 cm | 23.000 | 20.000 | **+15,0 %** |

El signo del error depende del perfil (si los horizontes profundos fueran más húmedos, subestimaría en vez de sobreestimar), pero siempre está mal en cuanto hay **dos o más horizontes**. Con suelo de un solo horizonte —incluido el valor por defecto de La Rioja, `ValoresDeSueloPorDefectoLaRioja`, que devuelve una sola capa de 100 cm— coinciden acumulado y espesor y no hay error. Es decir: **el fallo afecta justo a los suelos "buenos", los derivados del mapa de suelos con varios horizontes, y no a los de relleno.**

Capacidad de campo y punto de marchitez son la base de todo: TAW = CC − PM alimenta el estrés, el agotamiento, el drenaje y la recomendación de riego. Un error aquí se propaga a todas las salidas.

Corrección: recorrer por tramos `[techo, min(base, raíz)]` usando el espesor real `base − techo`. Verificable en producción comprobando si en `SueloUnidadCultivoTemporada` los `ProfundidadCM` de una misma UC crecen entre filas (si crecen, son acumulados y el fallo es real).

---

## C2 — Cuando falta un dato climático, se sustituye por la temperatura 🔴

En `DatosHidricos.cs`, cuando un día no tiene ETo, viento o humedad, se rellenan con el promedio de los tres días anteriores… **de temperatura media**:

- `Eto(fecha)` (`:516`): si falta ETo → promedia `TempMedia` de los días anteriores. ETo va en mm/día (≈2-8); la temperatura en °C (≈15-30). Se mete un ETo de ≈20 donde debería haber ≈5.
- `VelocidadViento(fecha)` (`:440`): si falta viento → promedia `TempMedia`. Un viento de ≈20 m/s.
- `HumedadMedia(fecha)` (`:461`): si falta humedad → promedia `TempMedia`. Una humedad de ≈20 %.

`Temperatura(fecha)` (`:482`) sí usa `TempMedia` en su respaldo —que para ella es lo correcto—. Las otras tres son copias de esa función a las que **no se les cambió el campo**: el respaldo debería tomar los ETo / viento / humedad de días anteriores, no la temperatura.

Efecto: un ETo inflado 3-4× dispara ETc y, con ella, la recomendación de riego; un viento de 20 m/s infla el Kc ajustado por clima (`KcAdjClima`: `0,04·(20−2)` = +0,72 al Kc). Y todo esto se activa **precisamente cuando el SIAR tiene huecos** — que es la situación que el propio sistema vigila con avisos por correo y que el cuadro de mando marca en ámbar/rojo. No es un caso raro.

---

## C3 — Umbrales de riego indexados por Id en vez de buscados por Id 🟠

`UnidadCultivoDatosHidricos.UmbralSuperiorRiego` y `UmbralOptimoRiego` (`DatosHidricos.cs:556` y `:573`) hacen:

```csharp
var indiceEstres = lTiposEstres[idTipoEstres].IdUmbralSuperiorRiego; // esto es un ID
var lEstresUmbral = lTipoEstresUmbralList[idTipoEstres];
var ret = lEstresUmbral[(int)indiceEstres].UmbralMaximo;             // se usa como POSICIÓN
```

Usan `IdUmbralSuperiorRiego` (un identificador) como **índice** de la lista. Solo funciona si los `IdUmbral` empiezan en 0 y coinciden exactamente con la posición en la lista. La otra función que resuelve lo mismo, `ClaseEstresUmbralInferiorYSuperior` (`:243`), lo hace **bien**: `lUmbrales.Find(x => x.IdUmbral == idInferior)?.UmbralMaximo`. Dos métodos, dos criterios distintos para la misma búsqueda. Si el Id no coincide con la posición, `UmbralSuperiorRiego`/`UmbralOptimoRiego` devuelven el umbral equivocado o `double.MinValue`, y eso alimenta las salidas `UmbralSuperiorRiegoOptimoRefPM` / `UmbralInferiorRiegoOptimoRefPM`.

---

## C4 — La lista de umbrales de estrés no se ordena, pero la clasificación asume que sí 🟠

`TipoEstresUmbral(idTipoEstres, indiceEstres)` (`DatosHidricos.cs:275`) clasifica recorriendo la lista y parando en el primer umbral que supera el índice de estrés:

```csharp
while (indiceEstres > ltu[i].UmbralMaximo && (i + 1 < ltu.Count)) ret = ltu[++i];
```

Esto **presupone la lista ordenada ascendentemente por `UmbralMaximo`**. Pero `DB.ListaEstresUmbral()` —la que se usa en el balance— hace `db.Fetch<TipoEstresUmbral>()` **sin `ORDER BY`**: el orden es el que devuelva SQL (normalmente el de la clave, `IdTipoEstres, IdUmbral`). Si `IdUmbral` no va en el mismo orden que `UmbralMaximo`, la clasificación del estrés —y con ella el mensaje, la descripción y el color que ve el regante— sale mal. Que el orden importa lo sabía el autor: la función hermana `TipoEstresUmbralOrderList` (`DB.cs:726`) sí hace `order by umbralMaximo`. La que entra en el cálculo no.

---

## C5 — Los parámetros de calibración que faltan se enmascaran con `?? double.MaxValue` 🟠

Por todo `CalculosHidricos.cs`, los coeficientes de las fórmulas de crecimiento se leen así:

```csharp
double modRaizCoefB = dh.ParamGet("ModRaizCoefB", nEtapaBase0) ?? double.MaxValue;
...
ret = antLongRaiz + modRaizCoefB * incT;
if (ret > profRaizMax) ret = profRaizMax;   // el clamp tapa el MaxValue
```

Si el parámetro no está en el JSON de la etapa, se usa `double.MaxValue`, el resultado se dispara y el `clamp` posterior lo deja en el máximo. Es decir: **un parámetro mal configurado no da error, simplemente lleva la variable (raíz, cobertura, altura) a su tope**, en silencio. Ocurre en `RaizLongitudDefPorFormulaLineal/Cuadratica`, `CoberturaDefPorFormulaCuadratica`, `AlturaDefPorFormulaLineal/Cuadratica`.

Además los valores por defecto son **inconsistentes**: unos coalescen a `0` y otros a `double.MaxValue` dentro de la misma familia de funciones (p. ej. en `CoberturaCrecimientoLineal`, `ModCobCoefA ?? 0` pero `ModCobCoefB ?? double.MaxValue`). Un mismo parámetro ausente cambia el resultado de forma distinta según la función.

---

## C6 — Precipitación efectiva sin acotar a cero 🟡

`PrecipitacionEfectiva(precip, eto)` (`CalculosHidricos.cs:491`):

```csharp
return precipitacion > 2 ? precipitacion - 0.2 * eto : 0;
```

Puede devolver **negativo**: basta `0,2·eto > precip` con `precip > 2` (p. ej. precip 2,1 y eto 12). Una lluvia efectiva negativa entra en el balance como `− pef` en el agotamiento y `+ pef` en el drenaje, es decir, **una lluvia pequeña acabaría secando el suelo**. Con ETo correcto (2-8) el caso es difícil, pero se vuelve fácil combinado con C2 (el respaldo de ETo devuelve ≈20). Debería acotarse a `≥ 0`.

---

## C7 — Guardas de índice y divisiones sin proteger 🟡

- **`ParamGet` con off-by-one** (`DatosHidricos.cs:342`): la guarda es `ParametrosEtapas.Count >= etapaBase0`, cuando el índice válido llega hasta `Count − 1`; debería ser `>`. Con `etapaBase0 == Count` accede a `ParametrosEtapas[Count]` → excepción. La propia clase `ParametrosEtapasCalculos.Get` lo hace bien (`if (nEtapa >= this.Count) return null;`). Inconsistente y latente.
- **División sin proteger en `Kc`** (`:133`): `(cob − cobIni) * (kcFin − kcIni) / (cobFin − cobIni)`. Si `cobFin == cobIni` (con `kcIni ≠ kcFin`), es división por cero → `NaN`, que se propaga a ETc.
- **Cast que revienta** en `AlturaDefPorFormulaLineal`/`...Cuadratica`/`CoberturaDefPorFormulaCuadratica`: `double it = (double)lb.IntegralTermica;` lanza excepción si `IntegralTermica` es `null`, lo que ocurre si un cultivo **sin temperatura base** usa una fórmula de crecimiento por integral térmica (tipo 2 o 3). Combinación de datos improbable, pero no imposible.
- **Recomendaciones de riego sin acotar** (`RecomendacionRiegoMm`, `:696`): `ret = driEnd − drLimiteRiego` puede ser negativa y no se acota, arrastrando a `RecomendacionRiegoBruto` y `RecomendacionRiegoTiempo` negativos.

---

## C8 — Inicialización del primer día, ad hoc 🟡

En `CalculaLineaBalance` (`CalculosHidricos.cs:870`), el aporte de agua por crecimiento de raíz es:

```csharp
lb.AguaCrecRaiz = AguaAportadaCrecRaiz(0.8, lb.AguaDisponibleTotal, lbAnt.AguaDisponibleTotal);
```

El **primer día** `lbAnt` es un `LineaBalance` recién creado, con `AguaDisponibleTotal = 0`, así que `AguaCrecRaiz = 0,8 · TAW` — un aporte grande y espurio el día 1, provocado por el salto de TAW de 0 al valor real. Se combina con la inicialización `driStart = taw` ("el depósito está vacío"), de modo que el día 1 el agotamiento arranca en ≈0,2·TAW independientemente del estado real del suelo. Todo esto son decisiones de arranque razonables a medias, pero conviene revisarlas: el aporte por crecimiento de raíz asume además un suelo nuevo siempre al 80 % de TAW (`pSaturacion = 0.8` fijo), lo reconoce el propio comentario del método.

---

## Resumen

| # | Hallazgo | Efecto | Gravedad |
|---|---|---|---|
| C1 | Horizontes de suelo integrados como espesor cuando son profundidad acumulada | CC, PM y TAW mal con ≥2 horizontes; se propaga a todo | 🔴 |
| C2 | ETo, viento y humedad ausentes se sustituyen por la temperatura | ETc y recomendación de riego inflados cuando el SIAR tiene huecos | 🔴 |
| C3 | Umbrales de riego indexados por Id en vez de buscados por Id | Umbrales de riego erróneos si Id ≠ posición | 🟠 |
| C4 | La lista de umbrales de estrés no se ordena antes de clasificar | Nivel/mensaje/color de estrés mal si el orden de PK ≠ orden de umbral | 🟠 |
| C5 | Parámetros que faltan enmascarados con `?? double.MaxValue` + clamp | Calibración ausente no da error; resultados silenciosamente al tope | 🟠 |
| C6 | Precipitación efectiva puede ser negativa | Una lluvia pequeña seca el suelo (agravado por C2) | 🟡 |
| C7 | Guarda off-by-one, divisiones y casts sin proteger | Excepciones o NaN con datos límite | 🟡 |
| C8 | Inicialización del día 1 ad hoc (aporte de raíz espurio, 80 % fijo) | Primer día del balance poco fiable | 🟡 |

**Orden sugerido:** C2 primero (es el de mayor impacto y el más fácil de arreglar — cambiar tres campos en el respaldo), C1 después (es el más grave de fondo, pero conviene confirmarlo antes contra los datos de `SueloUnidadCultivoTemporada`), y luego C3-C6. C7-C8 son endurecimiento.

Ninguno se ha tocado: esto es solo la auditoría. Todos existen igual en el proyecto original `WebApi/` y en el migrado `OptiAqua.Api/` (la lógica se portó sin cambios).
