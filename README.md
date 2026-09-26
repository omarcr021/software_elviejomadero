# Sistema de Gestión de Comandas y Delivery - El Viejo Madero

Sistema web desarrollado con arquitectura **ASP.NET Core MVC (.NET 10)** para la gestión integral de comandas, menú gastronómico y colaboradores del restaurante **El Viejo Madero**.

---

## 1. Descripción
Este proyecto corresponde al desarrollo del **Sprint 1** del sistema académico, enfocado en el núcleo de autenticación, administración de la carta gastronómica y registro de personal con asignación de roles y control de acceso.

---

## 2. Stack Tecnológico

- **Backend:** C# / .NET 10, ASP.NET Core MVC
- **Persistencia & ORM:** Entity Framework Core 10, SQLite (.db local)
- **Seguridad & Autenticación:** ASP.NET Core Identity (hashing seguro de contraseñas, cookies de sesión, roles y claims, tokens antiforgery)
- **Frontend:** Razor Views, Bootstrap 5, Bootstrap Icons, HTML5, CSS3, JavaScript Vanilla
- **Testing:** xUnit, Moq, EF Core InMemory

---

## 3. Requisitos Previos

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (versión 10.0.x o superior)
- Git (opcional para clonado)
- Navegador web moderno (Chrome, Edge, Firefox)
- Herramienta de Entity Framework Core CLI (opcional para migraciones manuales):
  ```bash
  dotnet tool install --global dotnet-ef
  ```

---

## 4. Instalación

1. Clonar el repositorio:
   ```bash
   git clone <url-del-repositorio>
   cd software_elviejomadero
   ```

2. Restaurar dependencias y paquetes NuGet:
   ```bash
   dotnet restore
   ```

---

## 5. Configuración

La cadena de conexión SQLite se encuentra preconfigurada en `appsettings.json` y `appsettings.Development.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Data Source=viejo_madero.db"
}
```

Al iniciar la aplicación, la base de datos `viejo_madero.db` se creará automáticamente en la raíz del proyecto si aún no existe.

---

## 6. Base de Datos y Migraciones

La aplicación cuenta con inicialización automática (`DbInitializer`) que aplica las migraciones pendientes y siembra los roles y datos iniciales en cada ejecución.

Si deseas gestionar las migraciones manualmente:

- **Crear una nueva migración:**
  ```bash
  dotnet ef migrations add <NombreMigracion>
  ```
- **Aplicar migraciones:**
  ```bash
  dotnet ef database update
  ```

---

## 7. Ejecución del Proyecto

Para iniciar el servidor de desarrollo:

```bash
dotnet run
```

Abre tu navegador en la URL mostrada en consola (típicamente `https://localhost:5001` o `http://localhost:5000` / `https://localhost:7124`).

Para ejecutar la suite de pruebas automatizadas:
```bash
dotnet test software_elviejomadero.Tests/software_elviejomadero.Tests.csproj
```

---

## 8. Usuario Administrador de Desarrollo (Seed)

Para propósitos de prueba y demostración del Sprint 1, el sistema crea automáticamente el usuario administrador inicial:

- **Usuario:** `admin`
- **Contraseña:** `Admin123*!`
- **Rol:** `Administrador`
- **Acceso:** Acceso irrestricto al Panel de Administración, Gestión de Carta y Empleados.

> [!NOTE]
> Las contraseñas de todos los usuarios se almacenan cifradas mediante el algoritmo de derivación de claves PBKDF2 provisto por ASP.NET Core Identity; nunca se persisten en texto plano.

---

## 9. Roles Disponibles en el Sistema

El sistema implementa un modelo de **un solo rol por colaborador**:

1. **Administrador:** Acceso completo al hub administrativo, gestión de la carta y registro de empleados.
2. **Mozo:** Acceso al panel operativo del mozo (toma de pedidos asignada a Sprints futuros).
3. **Cocinero:** Acceso al panel operativo de cocina (preparación asignada a Sprints futuros).
4. **Recepcionista:** Acceso al panel operativo de recepción (pedidos de salón y delivery para Sprints futuros).
5. **Repartidor:** Acceso al panel operativo de despacho (delivery asignado a Sprints futuros).

---

## 10. Estructura del Proyecto

```
software_elviejomadero/
│
├── Controllers/
│   ├── AccountController.cs            # HU-01: Login, Logout, AccessDenied
│   ├── AdministrationController.cs     # Panel general administrativo (Dashboard)
│   ├── DishController.cs               # HU-16: CRUD y visibilidad de Carta
│   ├── EmployeeController.cs           # HU-17: Listado y modal de registro de Empleados
│   └── HomeController.cs               # Vistas contextuales operativas
│
├── Data/
│   ├── ApplicationDbContext.cs         # IdentityDbContext + DbSets + Fluent API
│   ├── DbInitializer.cs                # Sembrado de roles, admin y platos iniciales
│   └── Migrations/                     # Migraciones EF Core
│
├── Models/
│   ├── ApplicationUser.cs              # IdentityUser + DNI, Teléfono, Activo, LastLoginAt
│   ├── Category.cs                     # Categorías del menú
│   └── Dish.cs                         # Platos (Precio > 0, tiempo prep, IsActive)
│
├── Services/
│   ├── Interfaces/
│   │   ├── IAuthenticationService.cs
│   │   ├── IDishService.cs
│   │   └── IEmployeeService.cs
│   └── Implementations/
│       ├── AuthenticationService.cs
│       ├── DishService.cs
│       └── EmployeeService.cs
│
├── ViewModels/
│   ├── LoginViewModel.cs
│   ├── EmployeeViewModels.cs
│   └── DishViewModels.cs
│
├── Views/
│   ├── Account/
│   │   ├── Login.cshtml                # Pantalla dividida con identidad El Viejo Madero
│   │   └── AccessDenied.cshtml
│   ├── Administration/
│   │   └── Index.cshtml                # Hub administrativo con tabs y métricas
│   ├── Dish/
│   │   ├── Index.cshtml                # Carta agrupada por categorías
│   │   ├── Create.cshtml               # Formulario de registro de plato
│   │   └── Edit.cshtml                 # Formulario de edición de plato
│   ├── Employee/
│   │   └── Index.cshtml                # Listado de colaboradores + modal HU-17
│   └── Shared/
│       ├── _Layout.cshtml              # Layout con branding temático y navbar de roles
│       ├── _Alerts.cshtml              # Alertas flotantes en español
│       └── _ValidationScriptsPartial.cshtml
│
├── wwwroot/
│   ├── css/site.css                    # Estilos temáticos de restaurante
│   └── js/site.js                      # Interacciones cliente y autodesvanecimiento
│
├── software_elviejomadero.Tests/       # Suite de pruebas xUnit (34 tests aprobados)
│   ├── AuthenticationTests.cs          # Pruebas de reglas 1 a 6 (HU-01)
│   ├── EmployeeTests.cs                # Pruebas de reglas 7 a 14 (HU-17)
│   └── DishTests.cs                    # Pruebas de reglas 15 a 23 (HU-16)
│
├── appsettings.json
├── Program.cs
└── README.md
```

---

## 11. Historias de Usuario Implementadas (Sprint 1)

### HU-01: Iniciar Sesión
- Pantalla `/Account/Login` con diseño responsivo en 2 columnas (panel de identidad y formulario).
- Autenticación segura mediante cookies de ASP.NET Core Identity.
- Mensaje genérico de credenciales incorrectas para evitar filtración de existencia de usuarios.
- Bloqueo inmediato para usuarios con `IsActive = false` con mensaje `"El usuario no está activo."`.
- Registro de auditoría `LastLoginAt` con fecha y hora del último acceso exitoso.
- Redirección según rol:
  - `Administrador` -> `/Administration`
  - Roles operativos -> Pantalla contextual de bienvenida.

### HU-16: Gestionar Carta
- Acceso exclusivo para el rol `Administrador` (`[Authorize(Roles = "Administrador")]`).
- Visualización de la carta agrupada por categorías (`Combos Broaster`, `Especialidades`, `Acompañamientos`) con contadores de platos totales y visibles.
- Registro de platos con validaciones: Nombre obligatorio, Precio > 0, Tiempo de preparación >= 0, Categoría obligatoria.
- Edición de platos con actualización inmediata de datos.
- Alternancia de estado (Ocultar / Reactivar) para disponibilidad en toma de pedidos.
- Eliminación con modal de confirmación.

### HU-17: Registrar Empleado
- Acceso exclusivo para el rol `Administrador`.
- Modal Bootstrap interactivo "Nuevo empleado" con botón cancelar (limpia sin guardar) y guardar.
- Algoritmo de normalización y generación automática de nombre de usuario: primera letra del nombre + primer apellido (minúsculas, sin tildes ni caracteres especiales).
- Garantía de unicidad automática agregando sufijo numérico si el nombre de usuario ya existe (`jperez`, `jperez2`, etc.).
- Validación estricta de DNI (exactamente 8 dígitos numéricos, único en el sistema).
- Validación de Teléfono (exactamente 9 dígitos numéricos).
- Asignación obligatoria de un solo rol de Identity.
- Empleado activo por defecto con opción de alternancia.

---

## 12. Funcionalidades Fuera del Alcance (Sprints Futuros)

En estricto apego al alcance del Sprint 1 y a la metodología Agile Scrum, las siguientes historias y módulos **no forman parte de esta entrega**:

- **HU-18: Editar Usuario** (los botones de edición y desactivación en la tabla de empleados están inhabilitados).
- HU-02 a HU-15 (Toma de pedidos en mesa, asignación de mesas, comanda digital de cocina, despacho de comandas, delivery, seguimiento de motorizados, facturación y reportes estadísticos).
- Módulos de clientes, inventarios avanzados y auditorías externas.