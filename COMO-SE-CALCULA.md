# Cómo se calcula, cuándo se recalcula y qué pasa cuando el SIAR rectifica

Estado a 14/08/2026. Todo lo que dice este documento está en el código y medido
contra la base real (`OptiAquaV2`: 1.264 pares unidad-de-cultivo × temporada).

---

## 1. El cálculo

### 1.1 La recurrencia

El balance hídrico es una **recurrencia de primer orden**: cada día se calcula a
partir del día anterior. El bucle, en `BalanceHidrico.CalculaBalance`, es
literalmente esto:

```csharp
while (fecha <= fechaFinalEstudio && fecha <= DateTime.Today) {
    lineaBalance = CalculosHidricos.CalculaLineaBalance(unidadCultivoDatosHidricos, lbAnt, fecha);
    LineasBalance.Add(lineaBalance);
    lbAnt = lineaBalance;
    fecha = fecha.AddDays(1);
}
LineasBalance.RemoveAll(x => x.Fecha > DateTime.Today.AddDays(-1));
```

Empieza en la **fecha de siembra** y termina **ayer** (la última línea nunca es de
hoy) o antes, si el cultivo ya cerró su ciclo. Para una campaña típica son unos
230-340 días.

Cada día, en orden:

1. **Integral térmica**: se acumula el incremento de temperatura sobre la base
   del cultivo. Es lo que hace avanzar el desarrollo.
2. **Cobertura, altura y longitud de raíz**, por las fórmulas de la etapa.
3. **Cambio de etapa**, por días transcurridos o por cobertura alcanzada.
4. **Suelo a la profundidad de la raíz**: capacidad de campo y punto de marchitez
   integrando los horizontes (Saxton-Rawls), y de ahí el agua disponible total.
5. **Aportes**: lluvia (con su fracción efectiva) y riego (con la eficiencia del
   tipo de riego).
6. **Demanda**: `ETc = ETo × Kc_ajustado_por_clima × Ks`, donde Ks es el
   coeficiente de estrés que frena la transpiración cuando el suelo se seca.
7. **Cierre del día**: drenaje en profundidad, agotamiento final, contenido de
   agua en el suelo, índice de estrés y recomendación de riego.

### 1.2 De dónde salen los datos

`UnidadCultivoDatosHidricos` carga todo de una vez al construirse. Trece fuentes,
separadas por cómo invalidan:

**Estructurales** — si cambian, no vale nada de la campaña:
temporada, superficie de la unidad, etapas con sus parámetros, cultivo asignado,
tipo de riego, pluviometría, suelo (`SueloUnidadCultivoTemporada`), estación
climática asignada, catálogos de estrés y sus umbrales.

**Con fecha** — si cambian, solo dejan de valer de ese día en adelante:
clima diario (`DatoClimatico`), riegos (`Riego`) y datos extra
(`UnidadCultivoDatosExtra`).

### 1.3 La trampa del estado de arrastre

Parece que para reanudar en el día D bastaría con la línea del día D−1. **No
basta.** `NumeroEtapaDesarrollo` escribe hacia dentro de los datos cargados:

```csharp
dh.UnidadCultivoCultivoEtapasList[nEtapaBase0 + 1].FechaInicioEtapa = (DateTime)lb.Fecha;
```

Va fijando la fecha de inicio de cada etapa conforme avanza, y esa fecha la leen
los días siguientes. **El estado de arrastre es la línea anterior _más_ las fechas
de inicio de etapa tal y como estaban en ese momento.** Es la razón por la que hoy
no hay cálculo incremental (ver §3).

### 1.4 Lo que cuesta

Medido:

| | |
|---|---|
| Cargar `UnidadCultivoDatosHidricos` | **7,7 ms** |
| Calcular el balance entero (~340 días) | **5,1 ms** |
| Componer `DatosEstadoHidrico` | ~2 ms |
| **Las 1.264 unidades, de cero** | **13,6 s** |
| Los suelos de las 1.264 (consulta espacial contra el mapa) | **63 s** |

El balance no es lo caro. Lo caro son los suelos y la descarga del SIAR.

---

## 2. Qué se guarda y cuándo deja de valer

### 2.1 Tres niveles

1. **`SueloUnidadCultivoTemporada`** — el suelo por unidad de cultivo, ya
   resuelto contra el mapa. Se rehace en la pasada diaria.
2. **`EstadoHidricoUC`** — la RESPUESTA (`DatosEstadoHidrico`) del último día
   calculado, en JSON, una fila por unidad de cultivo. Es lo que abren el regante
   y el panel: una lectura indexada en vez de 13 ms de cálculo. Las 1.264 ocupan
   3 MB.
3. **Caché en memoria** — hasta 300 balances completos, descartando por uso más
   antiguo. Antes se guardaban los 1.264 (211 MB); ahora el proceso se queda en
   35 MB.

No se guarda la serie del balance día a día, y es a propósito: `DatosEstadoHidrico`
lee de `UnidadCultivoDatosHidricos` —alias, regante, superficie, estación,
textura—, así que guardarla no ahorraría los 7,7 ms de cargarlo, y deserializarla
cuesta más que recalcular.

### 2.2 Cómo se detecta que hay que recalcular

Una fila de `EstadoHidricoUC` deja de servir por cuatro caminos:

| Camino | Quién lo dispara | Alcance |
|---|---|---|
| **Cambia el día pedido** | el reloj | la fila solo vale para su `FechaPedida` |
| **Sube `VersionAlgoritmo`** | una constante en el código, a mano al tocar una fórmula | todas |
| **Invalidación explícita** | `SetDirtyUC`, `SetDirtyParcela`, `SetDirtyEstacion`, `SetDirtyTodo` | la unidad, las de la parcela, las de la estación, o todas |
| **La huella** | la pasada de control, comparando el SHA-256 de las entradas estructurales | la unidad |

**Ojo con las dos fechas.** La pantalla pide un día —hoy, o el fin de la temporada
si ya pasó— pero el estado se refiere a otro: el balance termina ayer y, si el
cultivo cerró su ciclo, mucho antes. Por eso la tabla lleva `FechaPedida` (por la
que se BUSCA) y `FechaEstado` (informativa). Buscar por la segunda hacía que solo
acertaran 250 filas de 1.262.

La **huella** no se comprueba al leer: costaría lo mismo que recalcular. La
escribe el cálculo, que ya tiene los datos delante, y sirve para que una pasada de
control cace lo que se haya modificado sin avisar por ningún camino.

---

## 3. Cómo se añaden días

**Con franqueza: no se añaden. Se rehace la serie entera.**

Cuando avanza el día, la fila guardada deja de valer porque su `FechaPedida` ya no
es la de hoy, y el balance se recalcula **desde la siembra**. Cuesta 13 ms por
unidad, así que la complejidad de un cálculo incremental no se paga: 1.264
unidades enteras son 13,6 segundos.

Lo que sí es incremental es **la respuesta guardada**: se rehace una fila por
unidad y se sustituye la anterior, sin acumular histórico. Probado de punta a
punta: se pide el día 30/06 y la segunda vez sale de la tabla (1.154 → 33 ms);
avanza al 01/07 y vuelve a calcularse (894 ms) para quedar servida en 7 ms; queda
UNA fila por unidad, y el estado del día nuevo es distinto del anterior.

Un cálculo incremental de verdad —reanudar en mitad de la campaña— exigiría
guardar el estado de arrastre de §1.3, y la salida limpia sería reanudar **al
principio de la etapa** que contiene el día sucio, no en un día cualquiera. Está
diseñado en `PERSISTENCIA-BALANCES.md` y **no implementado**: con el volumen
actual ahorraría 5 ms por unidad.

---

## 4. La pasada diaria

Una sola tarea, a las **9:00 hora de España** (zona fijada a `Europe/Madrid`; sin
eso un servidor en UTC dispararía a las 11:00 en verano). Tres fases **en este
orden, que no es negociable**:

```
1. clima del SIAR      -> descarga y guarda lo que haya cambiado
2. suelos              -> rehace SueloUnidadCultivoTemporada       (~63 s)
3. estado hídrico      -> recalcula y reescribe las 1.264 filas    (~14 s)
```

El orden importa porque el balance **lee** el suelo y el clima: rehacerlo antes
que ellos deja el resultado de ayer con fecha de hoy.

**No se fía de estar viva a esa hora.** El planificador es en memoria y muere con
el proceso; en IIS el grupo de aplicaciones se apaga por inactividad a los 20
minutos y de madrugada no entra nadie. Un segundo disparador, un minuto después
de arrancar, compara la última hora prevista por el cron con la marca
`FechaUltimaPasadaDiaria` y recupera la pasada si falta.

Y hay una segunda vía, perezosa: `DatosClimaticosList` llama a
`DatosClimaticosSiarRefresh`, que baja el SIAR si no se ha bajado hoy. Es la red
de seguridad por si la pasada no llegó a correr.

---

## 5. El caso que preguntas: el SIAR rectifica los últimos días

Es el caso común y conviene tenerlo claro.

### 5.1 Qué se pide

Cada pasada se piden **`ActualizarDatosClimaticosNDias` días hacia atrás**
(configurable, hoy **4**) contando desde el último día que hay en la tabla, y
hasta hoy. Una petición HTTP por estación, con el rango dentro de la URL. Se pide
esa ventana precisamente porque el SIAR **corrige y completa días ya publicados**:
una ETo provisional que se ajusta, una lluvia que faltaba.

### 5.2 Qué se escribe

Solo lo que de verdad ha cambiado. `DatosClimaticosSave` lee el tramo de una vez,
compara **campo a campo** (temperatura, humedad, viento, precipitación y ETo) y
únicamente da de alta los días nuevos y actualiza los que difieren. Devuelve las
fechas tocadas.

Esto no es cosmética. Antes se reescribían los ~5 días × 23 estaciones cada
mañana llegaran como llegaran, y con eso **cualquier invalidación por fecha sería
inútil**: todo aparecería tocado a diario.

### 5.3 Qué se invalida

Si una estación trae algún cambio real, `SetDirtyEstacion` **borra las filas de
`EstadoHidricoUC` de todas las unidades de cultivo de esa estación** y vacía la
caché en memoria. Si no ha cambiado nada, no se toca nada.

### 5.4 Y aquí está lo importante: la rectificación se propaga entera

Como el balance **se recalcula desde la siembra**, corregir la ETo del día D−3
no arregla solo ese día: la recurrencia se vuelve a recorrer desde el principio
con el valor bueno, así que **el agotamiento, el drenaje, el índice de estrés y la
recomendación de riego de D−3, D−2, D−1 y de hoy salen todos corregidos**. No hay
forma de que quede un residuo del valor viejo.

Dicho de otro modo: no ser incremental, que suena a defecto, es justo lo que hace
que este caso sea correcto sin esfuerzo.

**Si algún día se hace incremental, este es el caso que hay que cuidar.** Añadir
solo el día nuevo dejaría el error de D−3 congelado en el arrastre. La reanudación
tendría que ir al **primer día tocado por la rectificación**, no al último día
calculado —y, por lo de §1.3, al principio de su etapa—.

### 5.5 Lo que la ventana de 4 días no cubre

Si el SIAR corrige un día **anterior** a la ventana, no nos enteramos: no se pide
y por tanto no se compara. Se puede subir `ActualizarDatosClimaticosNDias` a costa
de descargar más. Hoy nadie vigila eso.

### 5.6 Y si el SIAR no da el dato

Si falta el día y tampoco hay ninguno de los tres anteriores, se usa la **media
del mes** de `ClimaPorDefecto` —calculada de la propia tabla: 21 años, 23
estaciones, 57.727 días— y el estado sale marcado con
`Status = "AVISO: N día(s) … estimados con las medias del mes"`, que las pantallas
pintan en ámbar. La **lluvia no se estima nunca**: se deja en 0, porque inventar
lluvia rebajaría el riego recomendado.

Antes de esto se devolvía 0 y el balance corría a ciegas: el suelo no se secaba
nunca y salía una ficha impecable que no significaba nada. Le pasaba a 151 de las
1.264 unidades.

---

## 6. Resumen en cuatro líneas

- El balance se calcula **siempre entero**, de la siembra a ayer, en 13 ms.
- Lo que se guarda es **la respuesta**, no el cálculo, y se invalida por día
  pedido, versión del algoritmo, marca explícita o huella de las entradas.
- La pasada de las 9:00 rehace clima → suelos → estados, en ese orden, y se
  recupera sola si el proceso no estaba vivo.
- Una rectificación del SIAR se propaga a toda la campaña porque no hay cálculo
  incremental. Cuando lo haya, habrá que reanudar desde el primer día corregido.
