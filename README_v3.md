# Sistema de Control de Parqueadero PUCESA
**Interfaz WinForms — InBIO 206 + Barrera motorizada — Autenticación por JSON**

---

## Tabla de contenido

1. [Arquitectura general](#1-arquitectura-general)
2. [Conexión de hardware](#2-conexión-de-hardware)
3. [Cómo se conecta el software al panel](#3-cómo-se-conecta-el-software-al-panel)
4. [Flujo completo de una tarjeta](#4-flujo-completo-de-una-tarjeta)
5. [Combinaciones subir y bajar barrera](#5-combinaciones-subir-y-bajar-barrera)
6. [Tabla completa de eventos](#6-tabla-completa-de-eventos)
7. [Formato del RTLog](#7-formato-del-rtlog)
8. [Autenticación por JSON](#8-autenticación-por-json)
9. [Registrar un nuevo TAG](#9-registrar-un-nuevo-tag)
10. [Archivos del proyecto](#10-archivos-del-proyecto)
11. [Cómo ejecutar](#11-cómo-ejecutar)
12. [Solución a problemas comunes](#12-solución-a-problemas-comunes)

---

## 1. Arquitectura general

```
┌─────────────────────────────────────────────────────────┐
│                    SOFTWARE (C# WinForms)                │
│                                                          │
│  Form1.cs          ──► Interfaz visual + decisión JSON   │
│  ZKTecoManager.cs  ──► Comunicación SDK <-> Panel InBIO  │
│  TagRepositorio.cs ──► CRUD sobre tags.json              │
│  FormRegistrarTag  ──► Captura y registro de TAGs        │
└────────────────────┬────────────────────────────────────┘
                     │ TCP/IP  192.168.1.201:4370
                     │ plcommpro.dll (Pull SDK)
┌────────────────────▼────────────────────────────────────┐
│               PANEL ZKTeco InBIO 206                     │
│                                                          │
│  Reader 1 (P1) ──► lector de entrada                     │
│  Reader 2 (P2) ──► lector de salida                      │
│  LOCK 1        ──► terminal UP   (sube barrera)          │
│  LOCK 2        ──► terminal DOWN (baja barrera forzado)  │
│  AUX IN 1      ──► sensor UP-OK  (brazo arriba)          │
│  AUX IN 2      ──► sensor DOWN-OK (brazo abajo)          │
│  Button1       ──► botón físico apertura manual          │
└────────────────────┬────────────────────────────────────┘
                     │
┌────────────────────▼────────────────────────────────────┐
│               BARRERA MOTORIZADA                         │
│                                                          │
│  Motor SUBIR  <── LOCK 1 (pulso -> brazo sube)           │
│  Motor BAJAR  <── LOCK 2 (pulso -> brazo baja forzado)   │
│  Sensor UP-OK  ──► InBIO AUX1 (NC)                       │
│  Sensor DOWN-OK ──► InBIO AUX2 (NC)                      │
└─────────────────────────────────────────────────────────┘
```

**Regla de acceso:** el panel InBIO actúa únicamente como sensor y actuador de relés.
La decisión de abrir o denegar la barrera la toma **exclusivamente** el archivo `tags.json`.
Si un TAG está en la memoria interna del InBIO pero NO en `tags.json`, la barrera no se abre.

---

## 2. Conexión de hardware

### Terminales del InBIO 206 usados

| Terminal InBIO        | Función                   | Conectado a                    |
|-----------------------|---------------------------|--------------------------------|
| LOCK 1 +/-            | Relay salida (pulso)      | Motor SUBIR de barrera         |
| LOCK 2 +/-            | Relay salida (pulso)      | Motor BAJAR de barrera         |
| AUX IN 1 (SEN + GND)  | Entrada sensor NC         | Sensor UP-OK (limite arriba)   |
| AUX IN 2 (SEN + GND)  | Entrada sensor NC         | Sensor DOWN-OK (limite abajo)  |
| READER 1              | Wiegand/RS485 lector      | Lector RFID entrada            |
| READER 2              | Wiegand/RS485 lector      | Lector RFID salida             |
| BUTTON 1              | Entrada digital           | Boton apertura manual          |
| LAN (RJ45)            | Ethernet                  | Red local (192.168.1.x)        |

### Sensores en configuracion NC (Normalmente Cerrado)

Los sensores de limite (UP-OK y DOWN-OK) estan conectados en NC:
- **Reposo (brazo en limite):** sensor CERRADO → InBIO detecta apertura del circuito → genera **E220**
- **Brazo en movimiento:** sensor ABIERTO → si sale de limite → genera **E221**

---

## 3. Como se conecta el software al panel

### Parametros de conexion (Form1.cs)

```
IP:      192.168.1.201
Puerto:  4370
Timeout: 4000 ms
SDK:     plcommpro.dll (Pull Communication Pro)
```

### Cadena de conexion enviada al SDK

```
protocol=TCP,ipaddress=192.168.1.201,port=4370,timeout=4000,passwd=
```

### Monitoreo en tiempo real

Una vez conectado arranca un timer cada **500 ms** que llama a:

```csharp
zkManager.EscucharSensoresYBotones();
// Internamente: GetRTLog(handle, buffer, 2048)
```

El panel devuelve todas las lineas de eventos ocurridos desde la ultima consulta.

---

## 4. Flujo completo de una tarjeta

```
1. Vehiculo llega y acerca tarjeta al lector (P1=entrada / P2=salida)
         |
2. InBIO 206 reporta el evento en RTLog:
   "2026-02-27 11:19:32,0,3846766,1,27,0,0"
   └── Campo [2] = 3846766   → numero de tarjeta
   └── Campo [3] = 1         → puertaId (P1=entrada)
   └── Campo [4] = 27        → eventoId (Exit Button Variant)
         |
3. ZKTecoManager.ProcesarLineaDeLog() detecta tarjeta != "0"
   → dispara OnAccesoIntentado("3846766", puertaId=1, eventoId=27)
         |
4. Form1.ZkManager_OnAccesoIntentado() recibe el evento
         |
5. Debounce: ¿este TAG ya se proceso hace menos de 10 segundos?
   ├── SI  → ignorar (rafaga duplicada del log)
   └── NO  → continuar
         |
6. Consultar tags.json:
   _repositorio.BuscarTarjeta("3846766")
   ├── NULL  → log "⛔ TAG no registrado" → NO abre barrera → FIN
   └── FOUND → log "✅ Acceso autorizado: [Nombre]"
         |
7. Activar relay segun puerta:
   ├── puertaId == 1 (entrada) → LevantarBrazo() → LOCK 1 → barrera SUBE
   └── puertaId == 2 (salida)  → LevantarBrazo() → LOCK 1 → barrera SUBE
         |
8. Panel confirma con eventos hardware:
   P1/P2 E8   → "Relay Activado" → brazo comienza a subir
   P1    E220 → "Sensor UP-OK"   → brazo llego arriba
   P2    E220 → "Sensor DOWN-OK" → brazo llego abajo
```

---

## 5. Combinaciones subir y bajar barrera

### Desde software (codigo C#)

| Accion | Funcion C# | Parametros ControlDevice | Relay | Terminal |
|--------|-----------|--------------------------|-------|----------|
| **Subir brazo** | `zkManager.LevantarBrazo()` | op=1, p1=1, p2=1, p3=1, p4=0 | LOCK 1 | UP |
| **Bajar brazo** | `zkManager.BajarBrazo()` | op=1, p1=2, p2=1, p3=1, p4=0 | LOCK 2 | DOWN |
| **Modo automatico** | `zkManager.ModoAutomatico()` | op=1, p1=X, p2=1, p3=4, p4=0 | — | Restaurar |

> **p3=1** = dar pulso (accion estandar)
> **p3=4** = restaurar modo automatico del panel

### Desde boton Button1 fisico (InBIO)

| Evento | Significado | Accion del software |
|--------|------------|-------------------|
| E202 en P1 | Button1 presionado | `LevantarBrazo()` se ejecuta automaticamente |

### Desde tarjeta (control de acceso JSON)

| Puerta leida | Relay activado | Accion fisica |
|-------------|----------------|---------------|
| P1 (entrada) | LOCK 1 | Brazo sube |
| P2 (salida)  | LOCK 1 | Brazo sube |

> La barrera se eleva en ambos casos. El brazo baja por gravedad/resorte una vez que el vehiculo pasa.

### Modo diagnostico (boton "AUTOMATICO" en pantalla)

Prueba los 4 outputs del panel en secuencia con pausa de 2 segundos entre cada uno:

| Paso | Descripcion | op | p1 | p2 | p3 | p4 |
|------|------------|----|----|----|----|-----|
| 1 | Lock 1: ABRIR | 1 | 1 | 1 | 1 | 0 |
| 2 | Lock 2: ABRIR | 1 | 2 | 1 | 1 | 0 |
| 3 | Auxiliar 1: ABRIR | 1 | 1 | 2 | 1 | 0 |
| 4 | Auxiliar 2: ABRIR | 1 | 2 | 2 | 1 | 0 |

---

## 6. Tabla completa de eventos

Todos los eventos que el sistema conoce y como los maneja:

| EventoID | Nombre                             | Puerta | Accion en software |
|----------|-------------------------------------|--------|--------------------|
| **0**    | Normal Open                         | P1/P2  | InBIO aprobo el TAG (en su BD interna). Si el TAG no esta en JSON, no abre |
| **1**    | Normal Close                        | P1/P2  | Cierre normal. Sin accion |
| **2**    | Sensor NC Abrio (Limite Alcanzado)  | P1     | `MostrarBarreraArriba()` |
| **2**    | Sensor NC Abrio (Limite Alcanzado)  | P2     | `MostrarBarreraAbajo()` |
| **3**    | Sensor NC Cerro (Brazo Salio)       | P1/P2  | Log "brazo en transito" |
| **5**    | Exit Button                         | —      | No manejado |
| **8**    | Relay Activado                      | P1     | `MostrarBarreraArriba()` (LOCK 1 pulsado) |
| **8**    | Relay Activado                      | P2     | `MostrarBarreraAbajo()` (LOCK 2 pulsado) |
| **9**    | Exit Button Pressed                 | —      | No manejado |
| **12**   | Relay en Reposo                     | P1     | `MostrarBarreraArriba()` (confirmacion) |
| **12**   | Relay en Reposo                     | P2     | `MostrarBarreraAbajo()` (confirmacion) |
| **20**   | Desconocido                         | —      | Log "EVENTO NO MANEJADO" |
| **27**   | Exit Button Variant                 | P1/P2  | Log "EVENTO NO MANEJADO" — es el codigo que genera el panel al leer un TAG que NO esta en su BD interna. Normal y esperado. |
| **28**   | Exit Button Long Press              | —      | No manejado |
| **34**   | Duress Alarm                        | —      | No manejado |
| **101**  | AUX Input Alarm 1                   | P1     | `MostrarBarreraArriba()` (sensor UP-OK en AUX1) |
| **102**  | AUX Input Alarm 2                   | P2     | `MostrarBarreraAbajo()` (sensor DOWN-OK en AUX2) |
| **202**  | Button1 Presionado (InBIO)          | P1     | `LevantarBrazo()` automatico |
| **220**  | Sensor Activado (Limite Alcanzado)  | P1     | `MostrarBarreraArriba()` — **evento principal UP-OK** |
| **220**  | Sensor Activado (Limite Alcanzado)  | P2     | `MostrarBarreraAbajo()` — **evento principal DOWN-OK** |
| **221**  | Sensor Desactivado (Salio de Limite)| P1/P2  | Log "brazo en transito" |
| **255**  | Panel Idle                          | —      | **FILTRADO siempre** — spam que el panel envia cada segundo, se descarta |

### Resumen por puerta

```
P1 (LOCK 1 / Entrada / Sensor UP-OK)
  Eventos BARRERA ARRIBA: E2, E8, E12, E101, E220
  Eventos de transito:    E3, E221
  Evento especial:        E202 (Button1) → dispara LevantarBrazo()

P2 (LOCK 2 / Salida / Sensor DOWN-OK)
  Eventos BARRERA ABAJO:  E2, E8, E12, E102, E220
  Eventos de transito:    E3, E221
```

---

## 7. Formato del RTLog

El panel InBIO 206 reporta eventos en este formato CSV:

```
DateTime, PIN, Card, DoorId, EventId, State, Verify
```

| Indice | Campo    | Ejemplo              | Descripcion |
|--------|----------|----------------------|-------------|
| [0]    | DateTime | `2026-02-27 11:19:32`| Fecha y hora del evento |
| [1]    | PIN      | `0` o `5`            | Pin del usuario en BD del panel (0 = sin usuario) |
| [2]    | Card     | `3846766` o `0`      | Numero RFID de la tarjeta (0 = sin tarjeta) |
| [3]    | DoorId   | `1` o `2`            | Puerta (1=entrada, 2=salida) |
| [4]    | EventId  | `27` o `8`           | Codigo del evento (ver tabla de eventos) |
| [5]    | State    | `0`, `1`, `2`        | Estado del relay/sensor |
| [6]    | Verify   | `0`, `200`           | Metodo de verificacion |

### Ejemplos reales de logs

```
# TAG 3846766 leido en lector de entrada (P1)
2026-02-27 11:19:32,0,3846766,1,27,0,0

# LOCK 1 activado (brazo subiendo)
2026-02-27 11:19:32,0,0,1,8,2,200

# Sensor UP-OK alcanzado (brazo arriba)
2026-02-27 11:19:32,0,0,1,220,2,200

# Brazo salio del limite superior (en transito)
2026-02-27 11:19:44,0,0,1,221,2,200

# Panel idle (filtrado, no se procesa)
2026-02-27 11:19:00,0,0,0,255,0,0

# Button1 fisico presionado
2026-02-27 11:19:14,0,0,1,202,2,200

# TAG leido en lector de salida (P2)
2026-02-27 11:19:53,0,3846766,2,27,1,0
```

---

## 8. Autenticacion por JSON

### Archivo tags.json

Ubicacion: mismo directorio que el ejecutable (`Application.StartupPath`).

```json
[
  {
    "NumeroTarjeta": "3846766",
    "Nombre": "Toyota Corolla — Juan Perez",
    "FechaRegistro": "2026-02-27T10:00:00"
  },
  {
    "NumeroTarjeta": "1234567",
    "Nombre": "Honda Civic — Maria Garcia",
    "FechaRegistro": "2026-02-27T11:00:00"
  }
]
```

### Reglas de acceso

- **Solo los TAGs en `tags.json` pueden abrir la barrera**
- Los TAGs en la memoria interna del InBIO que NO esten en `tags.json` NO abren
- Un TAG en `tags.json` abre tanto en P1 (entrada) como en P2 (salida)
- El debounce impide que el mismo TAG abra dos veces en menos de **10 segundos**
- Si el archivo `tags.json` no existe, ningun TAG puede entrar (lista vacia)

### Codigo de validacion (Form1.cs)

```csharp
// 1. Debounce: ignorar rafagas repetidas del mismo TAG
if (_ultimoAcceso.TryGetValue(numeroTarjeta, out DateTime ultimaVez) &&
    (DateTime.Now - ultimaVez).TotalSeconds < 10)
    return;

_ultimoAcceso[numeroTarjeta] = DateTime.Now;

// 2. Consultar JSON — unica fuente de autorizacion
var tag = _repositorio.BuscarTarjeta(numeroTarjeta);

if (tag == null)
{
    // No esta en JSON → denegar
    ManejarLog($"⛔ TAG no registrado: {numeroTarjeta}", Advertencia);
    return;
}

// 3. Esta en JSON → abrir barrera
ManejarLog($"✅ Acceso autorizado: {tag.Nombre}", Exito);

if (puertaId == 1 || puertaId == 2)
{
    if (zkManager.LevantarBrazo())
        MostrarBarreraArriba();
}
```

---

## 9. Registrar un nuevo TAG

### Pasos para registrar

1. Pantalla principal → boton **"GESTIONAR TAGs"** (morado)
2. En la ventana que abre → **"ACTIVAR CAPTURA"** (boton verde)
3. Acercar la tarjeta al lector fisico (cualquier lector, P1 o P2)
4. El numero de TAG aparece automaticamente en pantalla
5. Escribir el nombre del propietario/vehiculo (max. 24 caracteres)
6. Clic en **"REGISTRAR TAG"**
7. El TAG queda guardado en `tags.json` y puede usarse de inmediato

### Como funciona la captura

- El timer de monitoreo (500 ms) lee el RTLog del panel
- Cuando detecta `partes[2] != "0"` (hay tarjeta en el log) y `ModoCaptura == true`:
  - Dispara el evento `OnTarjetaDetectada`
  - `FormRegistrarTag` muestra el numero en pantalla
  - La captura se desactiva automaticamente

### Eliminar un TAG

1. **"GESTIONAR TAGs"** → seleccionar fila en la tabla
2. Clic **"Eliminar seleccionado"** → confirmar
3. El TAG se elimina del JSON y ya no puede abrir la barrera

---

## 10. Archivos del proyecto

| Archivo | Funcion |
|---------|---------|
| `Form1.cs` | Interfaz principal. Maneja: conexion, logs, botones manuales, evento de acceso JSON |
| `ZKTecoManager.cs` | Toda la comunicacion con el SDK de ZKTeco. Conexion, ControlDevice, GetRTLog, parseo de logs |
| `TagRepositorio.cs` | Lectura/escritura de `tags.json`. Cache en memoria. BuscarTarjeta, AgregarTag, EliminarTag |
| `FormRegistrarTag.cs` | Ventana de registro de TAGs. Captura por lector fisico y guarda en JSON |
| `tags.json` | Base de datos de TAGs autorizados (se crea al registrar el primer TAG) |
| `plcommpro.dll` | SDK oficial ZKTeco (Pull Communication Pro) — debe estar junto al .exe |
| `pltcpcomm.dll` | SDK alternativo TCP ZKTeco — debe estar junto al .exe |
| `tcpcomm.dll` | SDK legacy ZKTeco — debe estar junto al .exe |
| `logo.png` | Logo de la institucion (opcional, cambiable desde la interfaz) |

---

## 11. Como ejecutar

### Requisitos

- Windows 10/11
- .NET 10 SDK instalado
- Panel ZKTeco InBIO 206 en red local en `192.168.1.201:4370`
- DLLs de ZKTeco en la carpeta del proyecto

### Compilar y ejecutar

```bash
cd "InterfazParqueadero Funcional V2.0-Respaldo"
dotnet build
dotnet run
```

### Si el build falla por proceso bloqueado

```bash
# Cerrar la app primero desde Windows, o forzar por consola:
cmd /c "taskkill /IM InterfazParqueadero.exe /F"
# Luego compilar de nuevo
dotnet build
```

### Paso a paso al iniciar

1. La app abre → indicador "DESCONECTADO" (gris)
2. Verificar que la IP sea `192.168.1.201` y el puerto `4370`
3. Clic **"CONECTAR"** → si la conexion es exitosa el indicador se pone verde
4. El sistema empieza a monitorear en tiempo real cada 500 ms
5. Cualquier tarjeta registrada en `tags.json` que se acerque al lector abrira la barrera

---

## 12. Solucion a problemas comunes

| Problema | Causa | Solucion |
|----------|-------|---------|
| `dotnet build` falla (MSB3026/MSB3027) | La app anterior sigue ejecutandose y bloquea el `.exe` | Cerrar la ventana de la app, luego `dotnet build` |
| Barrera no sube al pasar tarjeta | TAG no esta en `tags.json` | Registrar el TAG desde "GESTIONAR TAGs" |
| Log muestra "⛔ TAG no registrado" | El numero no coincide con el guardado en JSON | Volver a registrar el TAG con captura automatica |
| Log muestra "EVENTO NO MANEJADO E27" | El panel reporta E27 cuando lee un TAG que no esta en su BD interna | Normal y esperado. El sistema lo ignora y usa solo el JSON |
| Barrera no sube (TAG si esta en JSON) | Posible perdida de conexion con el panel | Reconectar: clic DESCONECTAR → CONECTAR |
| Conexion falla (Handle=0) | Panel apagado, IP incorrecta o firewall bloqueando puerto 4370 | Verificar IP, hacer `ping 192.168.1.201`, revisar firewall |
| Los sensores no reportan E220 | Los sensores AUX no estan conectados al InBIO | Verificar cableado SEN+GND en AUX IN 1 y AUX IN 2 |
| El brazo no responde al comando software | Cableado LOCK 1 o LOCK 2 desconectado | Verificar terminales LOCK 1/2 en el panel y motor |

---

## Informacion del proyecto

- **Hardware:** ZKTeco InBIO 206 (2 puertas)
- **SDK:** plcommpro.dll (Pull Communication Pro)
- **Framework:** .NET 10.0 Windows
- **Autorizacion:** JSON local (tags.json) — el panel solo actua como sensor/actuador
- **Institucion:** Pontificia Universidad Católica del Ecuador Sede Ambato (PUCESA)
- **Version:** 2.0 — 27/02/2026
