using System;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml.Templates;
using EvaluacionesApp.Views.Grades;

namespace EvaluacionesApp.Views.Grades;

public partial class GradesView : UserControl
{
    IDisposable? subscription;

    public GradesView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        this.AttachedToVisualTree += async (_, __) =>
        {
            if (DataContext is GradesViewModel vm)
            {
                await vm.Reload();
            }
            BuildDynamicColumns();
        };
    }

    void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (subscription is IDisposable d)
        {
            d.Dispose();
            subscription = null;
        }

        if (DataContext is INotifyPropertyChanged npc)
        {
            npc.PropertyChanged += OnVmPropertyChanged;
            subscription = new ActionDisposable(() => npc.PropertyChanged -= OnVmPropertyChanged);
        }

        BuildDynamicColumns();
    }

    void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GradesViewModel.SelectedCourse) || 
            e.PropertyName == nameof(GradesViewModel.SelectedClass) ||
            e.PropertyName == nameof(GradesViewModel.ScoreRows))
        {
            BuildDynamicColumns();
        }
    }

    void BuildDynamicColumns()
    {
        if (ScoresGrid == null) return;
        if (DataContext is not GradesViewModel vm) return;

        // Clear columns
        ScoresGrid.Columns.Clear();

        // Student column
        ScoresGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Alumno",
            Binding = new Binding("Student.Name"),
            IsReadOnly = true
        });

        var leaves = vm.SelectedCourse?.Criteria is null ? Array.Empty<EvaluacionesApp.Models.Criterion>() : GradesViewModel.GetLeafCriteria(vm.SelectedCourse.Criteria).ToArray();
        vm.RecomputeWeights();
        
        // Debug output
        System.Diagnostics.Debug.WriteLine($"[GradesView] Building columns: {leaves.Length} leaf criteria found");
        System.Diagnostics.Debug.WriteLine($"[GradesView] ScoreRows count: {vm.ScoreRows.Count}");

        foreach (var c in leaves)
        {
            var col = new DataGridTemplateColumn
            {
                Header = c.Name,
                CellTemplate = CreateCellTemplate(c.Id, vm),
                CellEditingTemplate = CreateCellTemplate(c.Id, vm)
            };
            ScoresGrid.Columns.Add(col);
        }

        // Total column
        ScoresGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Total",
            Binding = new Binding("Total") { StringFormat = "F2" },
            IsReadOnly = true
        });
        
        // Force DataGrid to refresh
        ScoresGrid.ItemsSource = null;
        ScoresGrid.ItemsSource = vm.ScoreRows;
    }

    static IDataTemplate CreateCellTemplate(string criterionId, GradesViewModel vm)
    {
        var binding = new Binding($"Scores[{criterionId}]") { Mode = BindingMode.TwoWay, Converter = new NullableDoubleStringConverter(), UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged };
        var template = new FuncDataTemplate<object>((ctx, _) =>
        {
            var tb = new TextBox
            {
                [!TextBox.TextProperty] = binding
            };
            tb.TextChanged += (_, __) =>
            {
                if (ctx is ScoreRow row)
                {
                    row.Touch();
                    vm.NotifyScoreEdited();
                }
            };
            return tb;
        });
        return template;
    }

    class NullableDoubleStringConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            if (value is null) return string.Empty;
            if (value is double d) return d.ToString(culture);
            if (value is System.Nullable<double>)
            {
                var nd = (double?)value;
                return nd.HasValue ? nd.Value.ToString(culture) : string.Empty;
            }
            return value.ToString();
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            var s = value?.ToString();
            if (string.IsNullOrWhiteSpace(s)) return null;
            if (double.TryParse(s, System.Globalization.NumberStyles.Any, culture, out var d)) return d;
            return null;
        }
    }

    class ActionDisposable : IDisposable
    {
        readonly Action action;
        public ActionDisposable(Action action) { this.action = action; }
        public void Dispose() => action();
    }
}
