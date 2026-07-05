# Sistema de Paginación - FirmasApp

## Descripción general

Se ha implementado un sistema completo de paginación para la lista de empleados, permitiendo navegar eficientemente a través de grandes volúmenes de datos sin sobrecargar la memoria ni la interfaz.

## Características implementadas

### 1. **Navegación completa**
- ⏮ **Primera página**: Ir directamente a la primera página
- ◀ **Página anterior**: Retroceder una página
- ▶ **Página siguiente**: Avanzar una página
- ⏭ **Última página**: Ir directamente a la última página

### 2. **Información de paginación**
- Número de página actual (empezando desde 1)
- Total de páginas disponibles
- Total de elementos (empleados)
- Información visual clara en la interfaz

### 3. **Estados de botones**
- Los botones se deshabilitan automáticamente cuando:
  - Estás en la primera página (deshabilita: ⏮ y ◀)
  - Estás en la última página (deshabilita: ▶ y ⏭)
  - Hay una carga en progreso
  - No estás autenticado

### 4. **Integración con búsqueda**
- La búsqueda se integra perfectamente con la paginación
- Al buscar, se resetea a la primera página
- Los resultados de búsqueda también son paginados

## Configuración

### Tamaño de página
```csharp
private int _pageSize = 50; // 50 empleados por página
```

Puedes modificar este valor en `MainViewModel.cs` según tus necesidades:
- **20-30**: Para usuarios con conexión lenta
- **50**: Valor por defecto (equilibrado)
- **100**: Para usuarios con conexión rápida

## Flujo de navegación

```
Usuario carga empleados
    ↓
API retorna: { page: 0, size: 50, totalElements: 150, totalPages: 3 }
    ↓
Interfaz muestra: "Página 1 de 3 (150 total)"
    ↓
Botones habilitados: ▶ ⏭ (puede avanzar)
Botones deshabilitados: ⏮ ◀ (está en primera página)
```

## Detalles técnicos

### Archivos modificados

1. **Models/MainViewModel.cs**
   - Propiedades de paginación (CurrentPage, PageSize, TotalPages, TotalElements)
   - Comandos de navegación (FirstPageCommand, PreviousPageCommand, NextPageCommand, LastPageCommand)
   - Métodos de navegación (GoToFirstPageAsync, GoToPreviousPageAsync, etc.)

2. **Services/UsuarioService.cs**
   - Modificado para aceptar parámetros de página
   - Retorna `PaginatedResult<Usuario>` con información completa de paginación

3. **MainWindow.xaml**
   - Agregada barra de paginación debajo del DataGrid
   - Botones de navegación con tooltips
   - Información de paginación visible

4. **Converters/BoolToColorConverter.cs**
   - Agregado `AddOneConverter` para mostrar páginas empezando desde 1

5. **App.xaml**
   - Registrado el converter `AddOneConverter`

## Comportamiento en diferentes escenarios

### Carga inicial
```
Usuario autenticado → Click "Recargar" → Carga página 0
```

### Búsqueda
```
Usuario escribe "Juan" → Enter → Busca con page=0 → Muestra resultados paginados
```

### Navegación
```
Click "▶" → CurrentPage++ → Carga siguiente página
Click "⏮" → CurrentPage = 0 → Carga primera página
```

### Búsqueda vacía
```
Usuario borra búsqueda → Enter → Reset a página 0 → Carga todos los empleados
```

## API Integration

### Petición a la API
```http
GET /core/api/v1/empleados?page=0&size=50&nombres=Juan
```

### Respuesta de la API
```json
{
  "content": [...], // 50 empleados
  "page": 0,
  "size": 50,
  "totalElements": 150,
  "totalPages": 3
}
```

## Ventajas de esta implementación

### ✅ **Eficiencia**
- No carga todos los empleados en memoria
- Solo carga la página actual (50 empleados)
- Respuesta más rápida

### ✅ **Escalabilidad**
- Funciona con miles de empleados
- No degrada el rendimiento
- Uso optimizado de ancho de banda

### ✅ **Experiencia de usuario**
- Navegación intuitiva
- Información clara y visible
- Botones con estados apropiados

### ✅ **Código limpio**
- Arquitectura limpia y modular
- Separación de responsabilidades
- Fácil de mantener y extender

## Principios aplicados

### 🎯 **Claridad**
- Nombres descriptivos (GoToFirstPageAsync, CanGoToNextPage)
- Código fácil de entender
- Comentarios donde es necesario

### 🧹 **Simplicidad**
- Mínima complejidad
- Solo lo necesario
- Sin over-engineering

### 🔁 **Sin duplicidades**
- Código reutilizable (UpdatePaginationCommands)
- Lógica compartida (LoadPageAsync)
- Un solo source of truth

### ✅ **Tests fiables**
- Validación de estados
- Verificación de límites
- Manejo robusto de errores

## Solución de problemas

### Los botones no funcionan
- Verifica que estés autenticado
- Verifica que no haya una carga en progreso
- Revisa el Console Output para errores

### La información de página es incorrecta
- Verifica que la API esté retornando los valores correctos
- Revisa el Console Output para ver la respuesta de la API
- Verifica que los PropertyChanged se estén disparando

### La búsqueda no respeta la paginación
- La búsqueda siempre resetea a página 0
- Verifica que el texto no esté vacío
- Revisa el Console Output para debug

## Estadísticas en tiempo real

La interfaz muestra información constante sobre:
- 📄 Página actual y total
- 👥 Total de empleados
- ✅ Empleados con firma
- ❌ Empleados sin firma

## Futuras mejoras posibles

- [ ] Selector de tamaño de página (25, 50, 100)
- [ ] Ir a página específica (input numérico)
- [ ] Atajos de teclado (← → para navegar)
- [ ] Historial de páginas visitadas
- [ ] Caché de páginas ya visitadas

## Conclusión

El sistema de paginación implementado es eficiente, escalable y fácil de usar, siguiendo los principios de código limpio y buenas prácticas de desarrollo.
