# ProyectoAna

## Estado Actual

- Aplicación Avalonia Desktop en `net10.0`, con Avalonia 12, ReactiveUI, DynamicData y Zafiro.Avalonia.
- La composición se hace en `CompositionRoot.CreateAsync()`: carga el JSON de persistencia, crea `DynamicRoot` y registra `IDynamicSchoolStore`.
- `DynamicSchoolStore` es estado en memoria + guardado; `PersistenceService` concentra el I/O JSON.
- La vista de notas usa selector de curso, clase y trimestre. La lista de alumnos se muestra en tabla y el detalle de criterios en árbol.

## Modelo de Dominio

- `Course` contiene `Classes`, `Criteria` y la lista de `Terms`.
- `Criterion.Term` define a qué trimestre pertenece un criterio. Si el término es `null`, el criterio se trata como legado del primer trimestre.
- `Assessment` no tiene `Term`: una nota se identifica por `StudentId + CriterionId`. Para separar trimestres, los criterios de cada trimestre deben tener IDs distintos.
- Los criterios forman un árbol. Las notas se editan solo en hojas; los padres muestran agregados calculados desde sus hijos.
- Los pesos de criterios se interpretan como no negativos y se normalizan al calcular totales.

## Persistencia

- Ruta por defecto: carpeta de datos de usuario de la plataforma, bajo `ProyectoAna/persistencia.json`.
- El JSON persistido guarda criterios por clase como `assessments` y puntuaciones como `scores`.
- El guardado limpia notas duplicadas por `(studentId, criterionId)` y conserva la última.
- El guardado escribe a archivo temporal y reemplaza el destino de forma atómica con backup `.bak`.
- Si el JSON está corrupto al cargar, se crea una copia `*.corrupt-*.json` y se propaga el error.

## Convenciones de Implementación

- MVVM puro: las vistas no deben tener lógica más allá de `InitializeComponent` y constructor.
- Usar ReactiveUI y DynamicData para flujos de estado y colecciones reactivas.
- Preferir `ReactiveUI.Validation` para formularios editables.
- Evitar lógica de refresh imperativa salvo que simplifique claramente el flujo y no duplique estado.
- Inspeccionar el código local de Zafiro cuando haga falta:
  - Zafiro: `/mnt/fast/Repos/Zafiro`
  - Zafiro.Avalonia: `/mnt/fast/Repos/Zafiro.Avalonia`

## Verificación

- Comando principal: `dotnet test EvaluacionesApp.sln --no-restore`.
- Antes de cerrar cambios de deuda técnica, el build de tests debe quedar sin warnings nuevos.
- Mantener tests centrados en persistencia, reglas de negocio y ViewModels; evitar pruebas frágiles de detalles visuales.

## Deuda Técnica Residual

- Mantener cubiertos con tests los contratos de persistencia antes de refactorizar `PersistenceService`.
- `StudentsViewModel` y `PersistenceService` son grandes; partirlos solo cuando haya una motivación concreta y tests de soporte.
- Mantener dependencias con versiones fijas; evitar comodines en `PackageReference`.
