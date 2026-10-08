# Sprint 2 — HU-03 y HU-14 (El Viejo Madero)

Implementación preparada sobre la rama `develop` incluida en el ZIP entregado.

## HU-03: Registrar pedido para salón

- Rol permitido: `Mozo` o `Administrador`.
- URL: `/SalonOrder` → escoger una mesa `Libre` → `Registrar pedido`.
- Se muestra la mesa, etiqueta `Salón`, nombre del cliente y carta agrupada.
- Se pueden agregar platos, aumentar/disminuir cantidades, eliminar y ver el total.
- Se valida el nombre y que exista al menos un plato también en el servidor.
- Al confirmar, se asigna código `#S...`, usuario (mozo), fecha/hora UTC, se guarda con estado `En cocina`, y la mesa cambia a `Ocupada`.
- Confirmación con mesa, nombre del cliente y enlace `Volver al inicio`.
- Se crean las mesas `M1` a `M8` solo si no existen (cambiar en `Data/DbInitializer.cs` si el local usa otras mesas).
- No se implementa el cierre/pago/liberación de mesas, porque es otro flujo no cubierto por HU-03. Por eso una mesa ocupada permanece ocupada hasta que se implemente la HU correspondiente o se gestione su liberación.

## HU-14: Iniciar ruta de entrega

- Rol permitido: `Repartidor` o `Administrador`.
- URL: `/DeliveryRoute`.
- `Despacho activo` lista pedidos delivery en `Listo para despacho` con nombre, dirección y cantidad de productos.
- Botón `Aceptar e iniciar ruta`: cambia a `En camino`, asigna el repartidor autenticado y guarda fecha/hora de inicio UTC.
- Contador `En ruta` y sección `Mis pedidos en ruta` reflejan los pedidos del repartidor actual.
- `Ver detalle y contacto`: nombre, dirección, teléfono, platos/cantidades y monto a cobrar.
- No se puede iniciar dos veces ni regresar al estado `Listo para despacho` desde esta funcionalidad.

## Dependencias que faltaban en `develop`

- HU-02 (estados de mesa) no estaba desarrollada; se incorpora el **mínimo para HU-03**: entidad persistente `RestaurantTable`, estado `Libre/Ocupada`, selección y validación concurrente. No reemplaza una HU-02 más completa.
- HU-09 (actualizar estado de pedido en cocina) no estaba desarrollada; se incorpora un **puente para HU-14** en `/KitchenDispatch` (rol `Cocinero`/`Administrador`): visualizar pedidos de salón enviados y pasar deliveries `Nuevo` a `Listo para despacho`. No reemplaza la HU-09 completa.
- Los pedidos de salón no aparecen en `/ReceptionOrder`; sigue mostrando solo delivery/para llevar.

## Migración y despliegue

- Se agregó `Migrations/20261008100000_AddSalonAndDeliveryRoutes.cs` y se actualizó el snapshot.
- `DbInitializer` llama a `Database.MigrateAsync` al arrancar; crear copia de seguridad de la base SQLite antes de desplegar.
- `dotnet restore && dotnet test software_elviejomadero.Tests/software_elviejomadero.Tests.csproj && dotnet run` (requiere SDK .NET 10).
- Deploy en Render luego de integrar a `develop`; verificar que Render conserve el archivo SQLite de forma persistente, porque el almacenamiento efímero puede perder datos al reiniciar.

## Pruebas manuales

1. Acceder como `Mozo`/`Administrador`, crear un pedido de dos platos en `M1` y comprobar que está `Ocupada` y que aparece en cocina.
2. Intentar registrar otro pedido en `M1` y comprobar rechazo. Intentar enviar un carrito vacío y comprobar rechazo.
3. Registrar un delivery en `/ReceptionOrder`, entrar como `Cocinero`/`Administrador` a `/KitchenDispatch` y marcarlo `Listo para despacho`.
4. Entrar como `Repartidor`, abrir `/DeliveryRoute`, aceptar el delivery y comprobar `En camino`, fecha/hora, contador y detalle.
5. Intentar aceptar el mismo delivery de nuevo: debe ser rechazado. Verificar que un repartidor diferente no lo ve como pedido propio.
6. Confirmar que los flujos de `Administración`, `Carta`, `Colaboradores` y `Recepción` siguen operando.

> Nota: en este entorno no está disponible el SDK de .NET 10 para ejecutar `dotnet build` / `dotnet test`; se debe ejecutar la suite en un ambiente con el SDK instalado antes del merge.
