# Persistir los balances en disco, marcarlos sucios y recalcular por fechas

Propuesta de diseño. Nada de esto está implementado todavía.

---

## 1. Lo primero: qué cuesta de verdad

Antes de diseñar nada he medido el coste real contra la base local (`OptiAquaV2`,
1.264 pares unidad-de-cultivo × temporada, 17 temporadas, 435.048 días de balance
en total). Los números cambian la conversación:

| | medido |
|---|---|
| Un balance completo (cargar datos + calcular ~344 días) | **10,8 ms** — 7 ms de carga, 3 ms de cálculo |
| **Los 1.264 balances, todos, de cero** | **13,6 segundos** |
| Los 1.264 balances vivos en memoria | **211 MB** |

El recálculo de balances **no es el proceso largo**. `RecreateAll` tarda lo que
tarda porque antes de los balances hace dos cosas caras:

- `DB.DatosClimaticosSiarForceRefresh()` — una petición HTTP al SIAR por cada una
  de las 23 estaciones, contra un servicio externo.
- `RecalculaSuelos()` — por cada unidad de cultivo y cada parcela, una consulta
  espacial `geom.STContains(...)` contra `MapaSuelo` (13.576 recintos). **Hoy
  está roto**: falta el ensamblado `Microsoft.SqlServer.Types` y revienta en
  cuanto la consulta devuelve una columna `geometry`.

**Consecuencia para esta propuesta**: persistir los balances en disco no se
justifica por ahorrar tiempo de cálculo —son 13 segundos—, sino por otras tres
razones que sí son reales:

1. **Los 211 MB de RAM** que el proceso web mantiene vivos, y que crecen con los
   datos. Hoy hay 1.264 unidades de cultivo configuradas; en la base hay 7.926
   parcelas y 4.667 regantes. Una temporada configurada del todo puede rondar las
   8.000 unidades: **~90 s de recálculo y ~1,3 GB de caché**. Ahí sí duele.
2. **Se pierde en cada reinicio.** Tras publicar una versión, el primer usuario que
   entra paga el cálculo, y el panel de progreso no muestra nada porque el estado
   es por proceso.
3. **Es por proceso.** Dos instancias = dos cachés que no se hablan, y un recálculo
   lanzado en una no lo ve la otra (ya documentado como *gotcha*).

Y hay un precedente en el propio proyecto que conviene copiar: la tabla
**`SueloUnidadCultivoTemporada`** ya es exactamente esto —el resultado caro
(`UnidadCultivoSueloListNew`, con su consulta espacial) materializado en una tabla
que `RecalculaSuelos` borra y rehace, y que `UnidadCultivoDatosHidricos` lee sin
enterarse de cómo se calculó—. El balance sería su hermano.

---

## 2. Qué entra en un balance (inventario de entradas)

`UnidadCultivoDatosHidricos` carga trece cosas. Separadas por cómo invalidan:

### Estructurales — cambian, y el balance entero deja de valer

| Entrada | De dónde |
|---|---|
| Temporada (fecha inicial y final) | `Temporada` |
| Superficie de la unidad de cultivo | `UnidadCultivoExtensionM2` → parcelas |
| Etapas: duración, coberturas, alturas, `DefinicionPorDias`, `SeAplicaRiego`, `ParametrosJson` | `UnidadCultivoCultivoEtapas` |
| Cultivo asignado, regante, tipo de riego, fecha de siembra, pluviometría | `UnidadCultivoCultivo` |
| Cultivo: TBase, profundidad de raíz inicial y máxima, integral de emergencia | `Cultivo` |
| Eficiencia del riego | `RiegoTipo` |
| Suelo: horizontes con textura, elementos gruesos y materia orgánica | `SueloUnidadCultivoTemporada` |
| Estación climática asignada | `EstacionDeUC` |
| Catálogos de estrés y sus umbrales | `TipoEstres`, `TipoEstresUmbral` |
| Parámetros globales (`DrenajeUmbral`…) | `Config` |
| **La versión del algoritmo** | constante en el código |

### Temporales — cambian para un día, y solo invalidan de ese día en adelante

| Entrada | De dónde |
|---|---|
| Clima diario: ETo, temperatura, lluvia, viento, humedad | `DatoClimatico` (por estación) |
| Riegos | `Riego` (por unidad de cultivo) |
| Datos extra: riego y lluvia a mano, y las fechas de etapa confirmadas | `UnidadCultivoDatosExtra` |

Ojo: `FechaInicioEtapaConfirmada` vive en las etapas y lleva fecha, pero mueve el
calendario entero de la unidad de cultivo. **Va con las estructurales.**

---

## 3. La trampa: el estado de arrastre no es solo el día anterior

El balance es una recurrencia de primer orden. El bucle es literalmente:

```csharp
lineaBalance = CalculosHidricos.CalculaLineaBalance(unidadCultivoDatosHidricos, lbAnt, fecha);
```

Eso invita a pensar que para reanudar en el día D basta con la línea del día D−1.
**No basta.** `NumeroEtapaDesarrollo` escribe hacia dentro de los datos cargados:

```csharp
dh.UnidadCultivoCultivoEtapasList[nEtapaBase0 + 1].FechaInicioEtapa = (DateTime)lb.Fecha;
```

Es decir: conforme avanza el bucle va fijando la fecha de inicio de cada etapa, y
esa fecha la leen los días siguientes. **El estado de arrastre es la línea del día
D−1 _más_ las `FechaInicioEtapa` tal y como estaban al terminar D−1.**

Hoy esas fechas se guardan (`DB.FechasEtapasSave`, cuando `actualizaFechasEtapas`
es cierto), pero se sobrescriben en cada pasada, así que no sirven como punto de
reanudación fiable.

**Salida propuesta, y es la que hace todo lo demás sencillo: no reanudar en un día
cualquiera, sino en el primer día de la etapa que contiene el día sucio.** Al
principio de una etapa las fechas de inicio de las etapas anteriores ya están
fijadas y no se vuelven a tocar; el arrastre se reduce a la línea del día anterior.
Se recalcula una etapa entera en vez de la temporada entera, que es donde está casi
toda la ganancia, y no hay que persistir estado interno de ningún tipo.

---

## 4. Diseño propuesto

### 4.1 Dos tablas

```sql
CREATE TABLE BalanceDia (
    IdTemporada      varchar(20)  NOT NULL,
    IdUnidadCultivo  varchar(20)  NOT NULL,
    Fecha            date         NOT NULL,
    -- las 47 columnas de LineaBalance
    ...
    CONSTRAINT PK_BalanceDia PRIMARY KEY CLUSTERED (IdTemporada, IdUnidadCultivo, Fecha)
);

CREATE TABLE BalanceEstado (
    IdTemporada         varchar(20) NOT NULL,
    IdUnidadCultivo     varchar(20) NOT NULL,
    HashEntradas        binary(32)  NOT NULL,  -- SHA-256 de las entradas estructurales
    VersionAlgoritmo    int         NOT NULL,
    PrimerDiaSucio      date        NULL,      -- null = al día
    UltimoDiaCalculado  date         NOT NULL,
    FechaCalculo        datetime2   NOT NULL,
    Estado              varchar(20) NOT NULL,  -- OK | ERROR
    Motivo              nvarchar(500) NULL,    -- el mensaje, cuando Estado = ERROR
    CONSTRAINT PK_BalanceEstado PRIMARY KEY (IdTemporada, IdUnidadCultivo)
);
```

Tamaño con los datos de hoy: 435.048 filas × ~47 columnas ≈ **160-200 MB**. Con
8.000 unidades de cultivo por temporada, del orden de 1 GB por temporada activa.
Conviene decidir desde el principio **cuántas temporadas se conservan**: lo normal
es que solo la activa y la anterior se consulten.

`Estado = ERROR` no es decoración: hoy, cuando una unidad de cultivo está mal
configurada, el fallo se traga un `catch` y solo queda en el log. Guardando el
motivo, el panel puede listar "las 37 unidades de cultivo que no calculan y por
qué", que es información que hoy no tiene nadie.

### 4.2 El hash

Se compone concatenando, **en orden determinista**, los valores de las entradas
estructurales de la sección 2, y se le pasa SHA-256. Tres cuidados que si no se
respetan hacen que el hash cambie solo:

- **Los `double` con formato invariante y redondeados** (`ToString("R", CultureInfo.InvariantCulture)`
  o, mejor, redondeados a los decimales que de verdad importan). Si no, el mismo
  dato leído dos veces puede dar cadenas distintas.
- **Las listas con `ORDER BY` explícito** en la consulta. El orden de un `SELECT`
  sin `ORDER BY` no está garantizado.
- **Las fechas en formato fijo** (`yyyyMMdd`), no la representación local.

`VersionAlgoritmo` es una constante en el código que se sube a mano cuando se toca
una fórmula. Va aparte del hash, en su propia columna, para poder responder
"invalidar todo porque ha cambiado el cálculo" con un `UPDATE` en vez de recalcular
1.264 hashes.

El hash se calcula con los datos que `UnidadCultivoDatosHidricos` ya carga: no hay
que ir a la base dos veces.

### 4.3 Las marcas de sucio

`PrimerDiaSucio` se rebaja (nunca se sube) desde los pocos sitios que escriben:

| Quién escribe | A quién marca | Desde qué día |
|---|---|---|
| `DB.DatosClimaticosSave` (SIAR) | todas las unidades de cultivo de esa estación | la fecha mínima que haya cambiado |
| `DB.RefreshDBRiegos` / API de riegos | las unidades de cultivo de esos riegos | la fecha mínima |
| `DB.DatosExtraSave` | esa unidad de cultivo | la fecha del dato |
| `Panel/ParcelaGuardar`, `UnidadCultivoCultivoTemporadaSave`, `FechasEtapasSave`, `RecalculaSuelos` | esa unidad de cultivo | **estructural**: no marca fecha, cambia el hash |

Esto es lo mismo que hoy hace `CacheDatosHidricos.SetDirtyUC`, pero con fecha y
sobreviviendo al reinicio. La familia `SetDirty*` que ya existe se queda: la caché
en memoria sigue siendo útil por delante de la tabla.

Nota sobre el SIAR: hoy `DatosClimaticosSiarForceRefresh` baja siempre los últimos
`ActualizarDatosClimaticosNDias` (4 por defecto) **y los reescribe todos**. Si se
marca sucio por "fila guardada" se ensuciarán 5 días cada mañana aunque no haya
cambiado ni un valor. Hay que **comparar antes de guardar** y marcar solo lo que de
verdad cambie; si no, el escalón incremental no sirve de nada.

### 4.4 Los tres caminos al pedir un balance

```
                 ¿hay fila en BalanceEstado?
                     no → calcular entero, guardar
                     sí ↓
       ¿HashEntradas o VersionAlgoritmo distintos?
                     sí → borrar días, calcular entero, guardar
                     no ↓
              ¿PrimerDiaSucio != null?
                     sí → recalcular desde el inicio de la etapa
                          que contiene PrimerDiaSucio  (§3)
                     no ↓
        ¿UltimoDiaCalculado < min(ayer, fin de temporada)?
                     sí → extender día a día desde el siguiente
                     no ↓
                  servir de disco
```

El cuarto caso es **el habitual**: cada mañana entra el clima de ayer y hay que
añadir un día. Con el estado de arrastre en la última fila persistida, añadir un día
es una lectura y un `CalculaLineaBalance`.

---

## 5. Dónde ponerlo

| | a favor | en contra |
|---|---|---|
| **Tabla en `OptiAquaV2`** (recomendado) | ya está NPoco montado; transaccional con las escrituras que invalidan; compartido entre procesos; el precedente de `SueloUnidadCultivoTemporada` | engorda la base y sus copias en ~200 MB (hoy) |
| Base aparte en el mismo SQL Server | se puede tirar y rehacer; copia de seguridad propia | una conexión más que configurar |
| SQLite local | fichero único, cero configuración (el paquete ya está en el csproj) | vuelve a ser por máquina; adiós a compartir entre instancias |
| Ficheros por unidad de cultivo | lectura completa muy rápida | no consultable, concurrencia a mano |

**Recomiendo la tabla en la propia base**, y si preocupa el tamaño de las copias,
moverla a una base aparte más adelante: el código no cambia, solo la cadena de
conexión.

---

## 6. Por dónde empezar

**Fase 1 — persistir y hash estructural.** `BalanceDia` + `BalanceEstado`, el hash,
y al invalidar **recalcular la unidad de cultivo entera** (10,8 ms). Sin marcas por
fecha todavía. Esto ya resuelve los tres problemas reales: la RAM, el arranque en
frío y el estado compartido entre procesos. Es la fase que más da por lo que cuesta.

**Fase 2 — el día que avanza.** `UltimoDiaCalculado` y extender un día. Con el
requisito previo de que el guardado del SIAR compare antes de escribir (§4.3).

**Fase 3 — reanudar dentro de la temporada.** `PrimerDiaSucio` y la reanudación por
etapa (§3). **Solo cuando el volumen lo pida**: con 1.264 unidades de cultivo ahorra
9 ms por unidad y añade la parte más delicada del diseño. Con 8.000 la cosa cambia.

Y antes que todo esto, si el objetivo es que "el recálculo total" deje de ser largo,
lo que hay que mirar es `RecalculaSuelos` y la descarga del SIAR, que es donde está
el tiempo. Empezando por arreglar `Microsoft.SqlServer.Types`, porque hoy la mitad
de los suelos que salen del mapa ni siquiera se calculan.

---

## 7. Cabos sueltos

- **Concurrencia**: dos peticiones simultáneas de la misma unidad de cultivo sucia
  calcularían las dos. Con `Interlocked` por clave, o aceptándolo (cuesta 10 ms).
- **Transacción**: el borrado de los días viejos y la inserción de los nuevos tienen
  que ir juntos, o una lectura intermedia ve el balance a medias.
- **Borrado de temporadas y de unidades de cultivo**: hace falta limpieza en cascada,
  o quedan filas huérfanas para siempre.
- **`actualizaFechasEtapas`**: hoy el cálculo escribe en `UnidadCultivoCultivoEtapas`
  como efecto secundario. Con balances persistidos hay que decidir si eso sigue
  siendo un efecto del cálculo o pasa a ser un dato del propio balance.
- **El día de hoy no existe**: `CalculaBalance` termina con
  `LineasBalance.RemoveAll(x => x.Fecha > DateTime.Today.AddDays(-1))`. El balance
  llega hasta ayer, y eso hay que respetarlo al persistir.
