using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Markup.Xaml.Templates;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using Avalonia.ReactiveUI;
using ReactiveUI;
using System.Reactive;
using System.Reactive.Linq;
using Zafiro.Avalonia.Controls.SlimDataGrid;
using EvaluacionesApp.Views.Controls;

namespace EvaluacionesApp.Views.Grades;

public partial class GradesView : ReactiveUserControl<GradesViewModel>
{
    public GradesView()
    {
        InitializeComponent();
    }
}
