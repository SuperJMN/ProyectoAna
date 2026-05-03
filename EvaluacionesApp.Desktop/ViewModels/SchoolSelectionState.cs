using EvaluacionesApp.Desktop.Dynamic;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace EvaluacionesApp.Desktop.ViewModels;

public partial class SchoolSelectionState : ReactiveObject
{
    [Reactive] private DynamicCourse? selectedCourse;
    [Reactive] private DynamicClass? selectedClass;
}
