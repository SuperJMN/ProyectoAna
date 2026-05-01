# Migracion a MasterDetailsView

Estas notas aplican a `Zafiro.Avalonia` `52.0.0`, que ya contiene el nuevo `MasterDetailsView` responsive. Esta aplicacion esta ahora en `Zafiro.Avalonia` `51.9.6`; actualiza a `52.0.0` las referencias a `Zafiro.Avalonia`, `Zafiro.Avalonia.Dialogs`, `Zafiro.Avalonia.Generators` y `Zafiro.Avalonia.Icons.Optris` tanto en Desktop como en Android.

El cambio es especialmente util para:

- `EvaluacionesApp.Desktop/Features/Students/Views/StudentsView.axaml`;
- `EvaluacionesApp.Desktop/Features/Grades/Views/GradesView.axaml`.

Ambas vistas duplican hoy el mismo patron: una rama wide, una rama narrow, flags `Are...DetailsShown`, comandos de abrir/cerrar y `BackTriggerBehavior`. `MasterDetailsView` sustituye esa infraestructura y deja el ViewModel con estado de dominio: coleccion, seleccion y comandos reales de la pantalla.

## Como se usa

La forma minima es:

```xml
<controls:MasterDetailsView ItemsSource="{Binding Items}"
                            SelectedItem="{Binding SelectedItem, Mode=TwoWay}"
                            CompactWidth="720">
    <controls:MasterDetailsView.ItemTemplate>
        <DataTemplate>
            <!-- fila simple para el master wide por defecto -->
        </DataTemplate>
    </controls:MasterDetailsView.ItemTemplate>

    <controls:MasterDetailsView.CompactItemTemplate>
        <DataTemplate>
            <!-- fila tactil para movil; el control ya la convierte en boton de apertura -->
        </DataTemplate>
    </controls:MasterDetailsView.CompactItemTemplate>

    <controls:MasterDetailsView.DetailsTemplate>
        <DataTemplate>
            <!-- editor o detalle del item seleccionado -->
        </DataTemplate>
    </controls:MasterDetailsView.DetailsTemplate>
</controls:MasterDetailsView>
```

Puntos importantes:

- `SelectedItem` no abre el detalle en compacto. Esto permite preseleccionar el primer elemento para desktop sin saltarse la lista en movil.
- En compacto, el listado por defecto abre detalle al tocar una fila.
- Si la pantalla esta dentro de `Frame`, el boton/gesto de atras cierra primero el detalle compacto y despues delega en `Navigator.Back`.
- `NavigationKey` cierra el detalle compacto cuando cambia el contexto logico, por ejemplo clase o trimestre.
- No hace falta `BackTriggerBehavior` para estas vistas.

## Migracion de Notas

Vista actual: `EvaluacionesApp.Desktop/Features/Grades/Views/GradesView.axaml`.

Objetivo: reemplazar el `ResponsivePresenter` que separa wide/narrow por un solo `MasterDetailsView`.

Esqueleto recomendado:

```xml
<controls:MasterDetailsView ItemsSource="{Binding ScoreRows}"
                            SelectedItem="{Binding SelectedScoreRow, Mode=TwoWay}"
                            NavigationKey="{Binding ScoreDetailsScopeKey}"
                            CompactWidth="720"
                            MasterPaneWidth="420">
    <controls:MasterDetailsView.WideMasterTemplate>
        <DataTemplate DataType="controls:MasterDetailsViewContext">
            <DataGrid Width="{Binding MasterPaneWidth}"
                      ItemsSource="{Binding ItemsSource}"
                      SelectedItem="{Binding SelectedItem, Mode=TwoWay}"
                      AutoGenerateColumns="False">
                <DataGrid.Columns>
                    <DataGridTextColumn Width="*" Header="Apellidos" Binding="{Binding Student.LastName}" />
                    <DataGridTextColumn Width="*" Header="Nombre" Binding="{Binding Student.FirstName}" />
                    <DataGridTextColumn Header="Nota global" Binding="{Binding Total, StringFormat=F2}" />
                </DataGrid.Columns>
            </DataGrid>
        </DataTemplate>
    </controls:MasterDetailsView.WideMasterTemplate>

    <controls:MasterDetailsView.CompactItemTemplate>
        <DataTemplate x:DataType="vm:ScoreRow">
            <StackPanel>
                <TextBlock Text="{Binding Student.FullName}" FontWeight="SemiBold" />
                <TextBlock>
                    <TextBlock.Text>
                        <Binding Path="Total" StringFormat="Nota global: {0:F2}" />
                    </TextBlock.Text>
                </TextBlock>
            </StackPanel>
        </DataTemplate>
    </controls:MasterDetailsView.CompactItemTemplate>

    <controls:MasterDetailsView.DetailsTemplate>
        <DataTemplate x:DataType="vm:ScoreRow">
            <!-- mover aqui el contenido actual de ScoreDetailsTemplate,
                 quitando el boton Volver y el BackTriggerBehavior -->
        </DataTemplate>
    </controls:MasterDetailsView.DetailsTemplate>
</controls:MasterDetailsView>
```

En `GradesViewModel` conviene eliminar:

- `AreScoreDetailsShown`;
- `OpenScoreDetails`;
- `ShowScoreRows`;
- `OpenDetailsForRow`;
- `HideScoreDetails`;
- la suscripcion que solo llama a `HideScoreDetails()` cuando cambian clase o trimestre.

Mantener:

- `ScoreRows`;
- `SelectedScoreRow`;
- `SelectedCourse`;
- `SelectedClass`;
- `SelectedTerm`;
- `CriteriaTree`;
- `Save`, `Reload` y `RebuildRows`.

Para que el detalle compacto vuelva a la lista al cambiar clase o trimestre, anade una clave de navegacion:

```csharp
public string ScoreDetailsScopeKey => $"{SelectedClass?.Id}:{SelectedTerm}";
```

Y emite `RaisePropertyChanged(nameof(ScoreDetailsScopeKey))` cuando cambien `SelectedClass` o `SelectedTerm`. Esa propiedad no representa UI; solo define cuando un detalle deja de pertenecer al contexto actual.

## Migracion de Alumnos

Vista actual: `EvaluacionesApp.Desktop/Features/Students/Views/StudentsView.axaml`.

El caso de alumnos tiene dos modos: edicion de un alumno y seleccion multiple para borrar/mover. `MasterDetailsView` debe cubrir el flujo maestro/detalle normal. El modo de seleccion multiple puede seguir siendo una accion de la vista: cuando `IsCompactSelectionMode` sea `true`, se puede mostrar el `ListBox` multiple actual fuera del `MasterDetailsView` o dentro de un `CompactMasterTemplate` especializado.

Para el flujo principal:

```xml
<controls:MasterDetailsView ItemsSource="{Binding SelectedClass.Students}"
                            SelectedItem="{Binding SelectedStudent, Mode=TwoWay}"
                            NavigationKey="{Binding SelectedClass}"
                            CompactWidth="720"
                            MasterPaneWidth="320">
    <controls:MasterDetailsView.ItemTemplate>
        <DataTemplate x:DataType="dynamic:DynamicStudent">
            <TextBlock Text="{Binding FullName}" />
        </DataTemplate>
    </controls:MasterDetailsView.ItemTemplate>

    <controls:MasterDetailsView.CompactItemTemplate>
        <DataTemplate x:DataType="dynamic:DynamicStudent">
            <TextBlock Text="{Binding FullName}" FontWeight="SemiBold" />
        </DataTemplate>
    </controls:MasterDetailsView.CompactItemTemplate>

    <controls:MasterDetailsView.DetailsTemplate>
        <DataTemplate x:DataType="dynamic:DynamicStudent">
            <ContentControl Content="{Binding}"
                            ContentTemplate="{StaticResource StudentDetailFieldsTemplate}" />
        </DataTemplate>
    </controls:MasterDetailsView.DetailsTemplate>
</controls:MasterDetailsView>
```

En `StudentsViewModel` se puede eliminar del flujo principal:

- `AreStudentDetailsShown`;
- `ShowSelectedStudentDetails`;
- `ShowStudentsList`;
- `OpenStudentDetails`;
- `OpenDetailsForStudent`;
- `ShowDetails`;
- `HideDetails`;
- el `BackTriggerBehavior` de la vista.

Mantener con cuidado:

- `StudentsSelection`, porque se usa para borrar y mover varios alumnos;
- `EnterCompactSelectionMode` y `ExitCompactSelectionMode` si se conserva el modo de seleccion multiple;
- `AddStudent`, `DeleteStudent`, `MoveStudents`, `Save`.

Cuando `AddStudent` cree un alumno, basta con seleccionar el alumno nuevo. En desktop se vera su detalle por seleccion. En compacto seguira mostrando la lista hasta que el usuario abra el detalle; si se quiere abrir automaticamente despues de crear, eso ya es una decision de producto, no una necesidad del layout.

## Limpieza esperada

Despues de migrar cada pantalla:

- no deberia quedar ningun `Are...DetailsShown` para el flujo master/detalle;
- no deberia quedar `BackTriggerBehavior` para cerrar detalles;
- no deberia haber dos arboles XAML completos para wide y narrow con el mismo contenido;
- el `Frame` del shell no debe cambiar;
- los ViewModels deben seguir siendo MVVM puros y sin referencias a controles.

## Validacion manual

Despues de actualizar NuGet y migrar:

1. Ejecutar `dotnet test EvaluacionesApp.sln --no-restore`.
2. Abrir Desktop en ancho grande y comprobar que master y detalle aparecen juntos.
3. Reducir la ventana por debajo de `CompactWidth` y comprobar que aparece solo la lista.
4. Abrir un elemento en compacto y pulsar atras: debe volver a la lista, no salir de la seccion.
5. Cambiar curso, clase o trimestre: debe cerrarse el detalle compacto y mantenerse una seleccion coherente.
6. Repetir en Android, usando el gesto/boton fisico de atras.
