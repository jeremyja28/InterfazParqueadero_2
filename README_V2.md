# 🚗 Sistema de Control de Parqueadero PUCESA
## Versión 2.0 — FUNCIONAL (Backup: 2026-02-27)

---

## ✅ ESTADO: FUNCIONA TOTALMENTE

Entrada y salida probadas con hardware real InBIO 206.

---

## 🔧 CONFIGURACIÓN DE HARDWARE

### Dispositivo
- **Panel:** ZKTeco InBIO 206 (2 puertas)
- **IP:** 192.168.1.201 | **Puerto:** 4370
- **SDK:** plcommpro.dll (Pull Communication)

### Readers
| Reader | Puerta InBIO | Función | Cables |
|--------|-------------|---------|--------|
| Reader 1 | Door 1 (P1) | ENTRADA | WD1, WD0 |
| Reader 4 | Door 2 (P2) | SALIDA  | GLED, WD1 |

### IMPORTANTE: SIN PUENTE FÍSICO
- **NO hay puente** entre LOCK 1 y LOCK 2
- Solo existe **1 barrera física** controlada por LOCK 1
- Tanto Reader 1 como Reader 4 activan **LOCK 1** (UP = subir brazo)
- LOCK 2 = señal de bajada automática (solo el motor la usa)

### Sensores de Piso
- El sistema requiere **metal en el piso** para activar la lectura del tag
- 3 detectores: entrada, centro, salida

---

## 🎯 CAMBIOS CLAVE QUE HICIERON FUNCIONAR EL SISTEMA

### 1. Reader 4 apuntaba a LOCK 2 (inexistente) → corregido a LOCK 1
**Archivo:** `Form1.cs`
```csharp
// ANTES (no abría):
zkManager.LevantarBrazo(puerta: 2); // LOCK 2 no existe físicamente

// AHORA (funciona):
zkManager.LevantarBrazo(puerta: 1, cancelarLock2: true);
```

### 2. Conflicto SUBIR/BAJAR en salida → resuelto con cancelarLock2
**Archivo:** `ZKTecoManager.cs` — función `LevantarBrazo()`

El InBIO auto-activa LOCK 2 (bajar) cuando Reader 4 concede acceso.
Si LOCK 1 (subir) se manda al mismo tiempo → motor se bloquea por seguridad.

**Solución:** Antes de activar LOCK 1, se cancela LOCK 2:
```csharp
public bool LevantarBrazo(int puerta = 1, bool cancelarLock2 = false)
{
    if (cancelarLock2)
    {
        TryControlDevice(1, 2, 1, 0, 0, "Cancelar LOCK 2 auto");
        Thread.Sleep(250); // Esperar reposo
    }
    TryControlDevice(1, 1, 1, 1, 0, "Comando Subir (Lock 1)");
}
```

### 3. Evento E27 ignorado bloqueaba salida con tags del JSON → corregido
**Archivo:** `Form1.cs`

El Reader 4 manda **E27** (no E0/E20) para tags que el InBIO no tiene en su memoria interna. Antes se descartaba todo E27. Ahora:

```
E27 con número de tarjeta
  ├─ Si está en JSON local → AUTORIZAR SALIDA → LevantarBrazo ✅
  └─ Si NO está en JSON → ignorar
```

### 4. Doble autorización: memoria interna InBIO + JSON local
**Ambas fuentes autorizan** la apertura de la barrera:

| Fuente | Evento que genera | Resultado |
|--------|------------------|-----------|
| Memoria interna InBIO | E0 (Normal Open) | Abre sin importar JSON |
| Memoria interna InBIO | E20 (Extended) | Abre sin importar JSON |
| Solo en JSON (no en InBIO) | E27 | Abre si está en JSON |
| Ninguna | E27 sin JSON | Ignorado |

### 5. Anti-rebote (5 segundos)
Evita que múltiples lecturas rápidas del mismo tag activen el relay varias veces:
```csharp
if (numeroTarjeta == ultimaTarjetaLeida && 
    puertaID == ultimoPuertaID && 
    tiempoTranscurrido.TotalSeconds < 5)
    return; // Ignorar duplicado
```

---

## 🚨 FUNCIONES DE EMERGENCIA (Tab Configuración)

### 🛑 STOP (sin confirmación — inmediato)
```
DetenerTodasSalidas()
→ Fuerza LOCK 1 = OFF
→ Fuerza LOCK 2 = OFF
```
Usar cuando el brazo quedó activo/sonando.

### 🚨 RESETEAR SISTEMA (con confirmación)
```
ResetearSistemaEmergencia()
→ LOCK 1 OFF → espera 300ms
→ LOCK 2 OFF → espera 300ms
→ Puerta 1 modo automático
→ Puerta 2 modo automático
```
Usar cuando hubo un comando del programa antiguo que quedó en memoria.

---

## 📋 EVENTOS CLAVE DEL InBIO 206

| Evento | Significado | Acción del programa |
|--------|-------------|---------------------|
| E0 | Normal Open (acceso concedido) | Abre barrera |
| E1 | Acceso denegado por InBIO | Solo log |
| E8 | Relay activado (confirmación) | Log "LOCK activado" |
| E20 | Access Granted Extended | Abre barrera (igual que E0) |
| E27 | Exit Button Variant (Reader 4 con tag no en InBIO) | Abrir si está en JSON |
| E220 | Sensor límite alcanzado | Actualiza indicador Arriba/Abajo |
| E221 | Sensor salió de límite | Log "brazo en tránsito" |
| E255 | Panel idle spam | Filtrado completamente |

---

## 💾 BASE DE DATOS DE TARJETAS

**Archivo:** `tarjetas_autorizadas.json`

Estructura:
```json
[
  {
    "NumeroTarjeta": "3846766",
    "NombreUsuario": "Jeremy"
  }
]
```

La clase `TarjetasDB.cs` lee este JSON e independientemente de la memoria del InBIO decide si mostrar el nombre del usuario en los logs.

**Importante:** El InBIO tiene su propia memoria independiente. Tags registrados en el programa anterior siguen en la memoria del InBIO y el sistema los autoriza vía E0/E20.

---

## 🖥️ DISEÑO DE LA INTERFAZ

- **Ventana:** Maximizada, MinimumSize (1200×800)
- **4 Tabs:** 🚗 Barrera | 🔖 Lectores | 🎫 Gestión | ⚙️ Configuración
- **Distribución vertical:**
  - Panel superior (header): 90px
  - TabControl: 380px
  - Panel de Logs: 360px (270px de listbox visible ≈ 18 líneas)
- **Botón Salir:** esquina superior derecha (Anchor=Top|Right)

---

## ▶️ CÓMO EJECUTAR

```bash
dotnet run
```
O abrir `InterfazParqueadero.sln` en Visual Studio y presionar F5.

**Requisito:** Los archivos `.dll` del SDK ZKTeco deben estar en la carpeta de ejecución:
- `plcommpro.dll`
- `pltcpcomm.dll`
- `tcpcomm.dll`

---

## 📁 ARCHIVOS PRINCIPALES

| Archivo | Propósito |
|---------|-----------|
| `Form1.cs` | Lógica principal, manejo de eventos, control de barrera |
| `Form1.Designer.cs` | Diseño visual de la interfaz (generado) |
| `ZKTecoManager.cs` | Comunicación con SDK ZKTeco, control de relays |
| `TarjetasDB.cs` | Base de datos local de tarjetas (JSON) |
| `tarjetas_autorizadas.json` | Lista de tags autorizados |
| `Program.cs` | Punto de entrada de la aplicación |
