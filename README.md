# Sistema de Control de Parqueadero PUCESA

## 📋 Descripción

Sistema integral de control de acceso vehicular para la **Pontificia Universidad Católica del Ecuador Sede Ambato (PUCESA)**, desarrollado en **C# WinForms** con integración de hardware **ZKTeco InBIO 206** para gestión de barreras mediante RFID.

El aplicativo permite gestionar dos parqueaderos (Garita Principal y Garita Secundaria) con control de entrada/salida por tarjetas RFID, visualización gráfica de disponibilidad, registro de vehículos, tickets de visitantes, auditoría completa e incidencias.

**Versión:** 2.0  
**Framework:** .NET 10.0-windows (x86)  
**Fecha:** Marzo 2026

---

## ✅ Módulos del Sistema

### 1. Dashboard (Pantalla Principal)
- Panel de configuración de conexión al InBIO 206 (IP, puerto, timeout)
- Estado de la barrera en tiempo real con indicadores LED (Arriba / Abajo)
- Controles de la barrera: Levantar, Bajar, Modo Automático, Emergencia
- Panel visual de último acceso con foto placeholder, datos del usuario y dirección (Entrada/Salida)
- Historial de accesos recientes con color por tipo de usuario
- Snapshot de cámara (placeholder)
- Anti-rebote para evitar lecturas duplicadas de tarjetas RFID

### 2. Mapa del Parking (ParkingSlotForm)
- Vista gráfica de disponibilidad por tipo de espacio (Normal, Discapacidad, Moto, Administrativo, Visitante)
- Tarjetas visuales por tipo con total, ocupados, en mantenimiento y disponibles
- Barras de progreso de ocupación por tipo y resumen general del piso
- Navegación por pisos (4 pisos Garita Principal / 3 pisos Garita Secundaria, 10 espacios c/u)
- Gestión de mantenimiento: poner/quitar puestos en mantenimiento por piso
- Conteo de vehículos actualmente dentro del parqueadero
- Exportación del estado completo a CSV

### 3. Tags (TagRegistroForm)
- CRUD completo de vehículos: Agregar, Editar, Eliminar, Activar/Desactivar
- Campos: Tag RFID, Cédula, Nombres, Apellidos, Placa, Tipo de Usuario, Facultad, Lugar Asignado
- Tipos de usuario: Estudiante, Docente, Administrativo, Visitante, VIP
- Importación masiva desde CSV
- Exportación de datos a CSV
- Búsqueda y filtrado en tabla
- Asignación automática de lugar disponible

### 4. Incidencias
- Registro de eventos del día (apertura manual, errores de hardware, accesos denegados)
- Código de evento, tipo, descripción, puerta y fecha/hora

### 5. Registros (Auditoría)
- Log de todas las acciones realizadas por operadores
- Campos: Fecha, Acción, Motivo, Operador
- Exportación de registros a CSV

### 6. Tickets de Visitantes (TicketVisitanteForm)
- Generación de tickets de entrada con código único y placa
- Registro de salida con cálculo de tiempo y cobro
- Lista de tickets activos
- Acción de abrir barrera directamente desde el formulario

---

## 🔐 Sistema de Login

El sistema requiere autenticación para acceder. Cada usuario tiene un rol y una garita asignada.

| Usuario | Contraseña | Rol | Garita |
|---------|------------|-----|--------|
| `operador1` | `pucesa2026` | Operador | Garita Principal |
| `operador2` | `pucesa2026` | Operador | Garita Secundaria |
| `admin` | `admin2026` | Administrador | Garita Principal |

> **Archivo de credenciales:** Las credenciales están definidas en el archivo `LoginForm.cs`, dentro del diccionario estático `Credenciales` (línea 33).

**Intentos fallidos:** El sistema permite un máximo de **5 intentos** de inicio de sesión. Tras cada intento incorrecto se muestra un mensaje con la cantidad de intentos restantes. Si se agotan los 5 intentos, el botón de login se deshabilita y se muestra el mensaje *"Demasiados intentos fallidos. Reinicie la aplicación."* — es necesario cerrar y volver a abrir la aplicación para poder intentar de nuevo.

**Sidebar:** Muestra el nombre del usuario logueado y la garita asignada.

---

## 🎨 Paleta de Colores PUCESA

| Color | Hex | Uso |
|-------|-----|-----|
| Deep Sapphire | `#0A2874` | Sidebar, headers, botones primarios |
| Wedgewood | `#517FA4` | Botón activo sidebar, acentos |
| Downy / Turquesa | `#73BFD5` | Decoraciones, gradientes |
| Rojo PUCE | `#E73137` | Alertas, accesos denegados |
| Blanco Puro | `#FFFFFF` | Fondo de tarjetas/cards |
| Gris Neutro | `#F2F2F2` | Fondo general de la aplicación |

---

## 🔌 Configuración de Hardware

### ZKTeco InBIO 206

**Conexión predeterminada:**
- **IP:** 192.168.1.201
- **Puerto TCP:** 4370
- **Timeout:** 4000ms
- **Monitoreo:** cada 500ms

**RELAYS (Salidas):**
- **LOCK 1** → Terminal UP del motor (sube brazo)
- **LOCK 2** → Terminal DOWN del motor (baja brazo)

**SENSORES (Entradas):**
- **AUX1 IN/GND** ← Sensor UP-OK (brazo arriba)
- **AUX2 IN/GND** ← Sensor DOWN-OK (brazo abajo)

**BUTTON1:** Genera Evento 202 (apertura física)

### Eventos del Hardware

| Evento | Código | Significado |
|--------|--------|-------------|
| E202 | Button1 | Botón físico presionado |
| E8 | Relay | Software activó LOCK |
| E220 | Sensor ON | Brazo llegó al límite |
| E221 | Sensor OFF | Brazo en movimiento |
| E12 | Relay OFF | Relay en reposo |

### Control del Brazo (3 formas):
1. **Botones del Software** — Levantar/Bajar desde la interfaz
2. **Button1 del InBIO** — Botón físico en el panel
3. **Control Wireless** — Control remoto (bypass, detectado por sensores AUX)

---

## 📁 Estructura del Proyecto

```
InterfazParqueadero_2/
├── Program.cs                 → Punto de entrada de la aplicación
├── Form1.cs                   → Lógica principal (Dashboard, navegación, hardware)
├── Form1.Designer.cs          → Diseño visual del formulario principal (generado)
├── Form1.resx                 → Recursos del formulario
├── LoginForm.cs               → Pantalla de login con credenciales y roles
├── ParkingSlotForm.cs         → Mapa gráfico de disponibilidad del parqueadero
├── TagRegistroForm.cs         → CRUD de vehículos y tags RFID
├── TicketVisitanteForm.cs     → Gestión de tickets de visitantes (entrada/salida)
├── MotiveForm.cs              → Diálogo para registrar motivo de acciones manuales
├── DataService.cs             → Servicio de datos (persistencia JSON, modelos, accesos)
├── TarjetasDB.cs              → Base de datos local de tarjetas RFID autorizadas
├── ZKTecoManager.cs           → Comunicación con SDK ZKTeco InBIO 206
├── InterfazParqueadero.csproj → Configuración del proyecto (.NET 10.0, x86)
├── InterfazParqueadero.sln    → Solución de Visual Studio
├── Properties/
│   ├── Resources.Designer.cs  → Código generado de recursos
│   └── Resources.resx         → Definición de recursos embebidos
└── Resources/
    ├── Logo_PUCESD.png        → Logo de la universidad
    ├── ParkingLogo.ico        → Ícono de la aplicación
    └── ParkingLogo.png        → Logo del parqueadero
```

---

## 📦 Modelos de Datos

### VehicleInfo (DataService.cs)
- `TagID`, `Cedula`, `Nombres`, `Apellidos`, `Placa`
- `TipoUsuario` (Estudiante, Docente, Administrativo, Visitante, VIP)
- `Facultad`, `LugarAsignado`, `Activo`

### ParkingSlot (ParkingSlotForm.cs)
- `Numero`, `Zona`, `Piso`, `Estado` (Libre/Ocupado)
- `TipoEspacio` (Normal, Discapacidad, Moto, Administrativo, Visitante)
- `EnMantenimiento`, `VehiculoAsignado`, `Incidencias`

### AuditEntry / IncidenciaEntry / AccesoReciente (DataService.cs)
- Registros de auditoría, incidencias y accesos recientes en memoria

### TarjetaRFID (TarjetasDB.cs)
- `Numero`, `NombreUsuario`, `Observaciones`, `FechaRegistro`, `Habilitada`

### TicketVisitante (TicketVisitanteForm.cs)
- `Codigo`, `Placa`, `FechaEntrada`, `FechaSalida`, `TotalPagar`, `Activo`

---

## 🚀 Cómo Ejecutar

### Requisitos
- .NET 10.0 SDK
- Windows (32 bits — requerido para DLLs de ZKTeco)
- DLLs del SDK: `plcommpro.dll`, `pltcpcomm.dll` (opcional), `tcpcomm.dll` (opcional)

### Compilar y ejecutar
```bash
dotnet build
dotnet run
```

### Publicar
```bash
dotnet publish -c Release -r win-x86 --self-contained
```

---

## 🐛 Solución de Problemas

| Problema | Solución |
|----------|----------|
| No conecta al InBIO | Verificar IP 192.168.1.201, hacer ping, revisar firewall, puerto 4370 abierto |
| Wireless no actualiza UI | Verificar cables de sensores en AUX1/AUX2 (IN y GND, no COM) |
| Button1 no funciona | Verificar configuración del InBIO para generar Evento 202 |
| Lectura RFID duplicada | Sistema tiene anti-rebote automático (1 segundo por tarjeta) |

---

## 📚 Referencias

- **SDK:** ZKTeco Pull Communication Pro
- **Hardware:** InBIO 206 (2 puertas, 2 lectoras)
- **Framework:** .NET 10.0-windows
- **Arquitectura:** x86 (32 bits)
- **Persistencia:** JSON local (`vehiculos_registrados.json`, `tarjetas_autorizadas.json`)

---

## 👨‍💻 Desarrollado para

**Pontificia Universidad Católica del Ecuador Sede Ambato (PUCESA)**

Sistema de control de acceso vehicular con monitoreo en tiempo real, gestión de parqueadero y auditoría completa.

---

## 📄 Licencia

Proyecto educativo — PUCESA 2026
