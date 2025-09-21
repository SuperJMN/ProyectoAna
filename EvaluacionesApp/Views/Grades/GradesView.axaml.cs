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
            try
            {
                if (DataContext is GradesViewModel vm)
                {
                    await vm.Reload();
                }
                BuildDynamicColumns();
            }
            catch
            {
            }
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
            e.PropertyName == nameof(GradesViewModel.SelectedClass))
        {
            BuildDynamicColumns();
        }
    }

    private string? lastCourseId = null;
    private int lastCriteriaCount = -1;
    
    void BuildDynamicColumns()
    {
        if (ScoresGrid == null) return;
        if (DataContext is not GradesViewModel vm) return;

        var sw = System.Diagnostics.Stopwatch.StartNew();
        
        // Check if we need to rebuild columns
        var currentCourseId = vm.SelectedCourse?.Id;
        var leaves = vm.SelectedCourse?.Criteria is null ? Array.Empty<EvaluacionesApp.Models.Criterion>() : GradesViewModel.GetLeafCriteria(vm.SelectedCourse.Criteria).ToArray();
        
        if (currentCourseId == lastCourseId && leaves.Length == lastCriteriaCount)
        {
            Console.WriteLine($"[PERF] Skip column rebuild - same course and criteria count");
            return;
        }
        
        lastCourseId = currentCourseId;
        lastCriteriaCount = leaves.Length;
        
        // Disconnect ItemsSource before clearing columns to avoid re-rendering
        var oldItemsSource = ScoresGrid.ItemsSource;
        ScoresGrid.ItemsSource = null;
        
        // Clear columns
        ScoresGrid.Columns.Clear();
        Console.WriteLine($"[PERF] Clear columns: {sw.ElapsedMilliseconds}ms");
        sw.Restart();

        // Student column
        ScoresGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Alumno",
            Binding = new Binding("Student.Name"),
            IsReadOnly = true
        });

        // Already computed above
        Console.WriteLine($"[PERF] Get leaves: {sw.ElapsedMilliseconds}ms, Count: {leaves.Length}");
        sw.Restart();
        
        vm.RecomputeWeights();
        Console.WriteLine($"[PERF] Recompute weights: {sw.ElapsedMilliseconds}ms");
        sw.Restart();

        foreach (var c in leaves)
        {
            // Use simple text column instead of template for better performance
            var col = new DataGridTextColumn
            {
                Header = c.Name,
                Binding = new Binding($"Scores[{c.Id}]") 
                { 
                    Mode = BindingMode.TwoWay,
                    Converter = DoubleConverterCache.Instance,
                    UpdateSourceTrigger = UpdateSourceTrigger.LostFocus
                },
                IsReadOnly = false
            };
            ScoresGrid.Columns.Add(col);
        }
        Console.WriteLine($"[PERF] Add {leaves.Length} criterion columns: {sw.ElapsedMilliseconds}ms");
        sw.Restart();

        // Total column
        ScoresGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Total",
            Binding = new Binding("Total") { StringFormat = "F2" },
            IsReadOnly = true
        });
        
        // Restore ItemsSource
        ScoresGrid.ItemsSource = oldItemsSource;
        
        Console.WriteLine($"[PERF] Total BuildDynamicColumns: {sw.Elapsed.TotalMilliseconds}ms");
    }

    static IDataTemplate CreateCellTemplate(string criterionId, GradesViewModel vm)
    {
        // Use a static converter instance
        var converter = DoubleConverterCache.Instance;
        var binding = new Binding($"Scores[{criterionId}]") 
        { 
            Mode = BindingMode.TwoWay, 
            Converter = converter, 
            UpdateSourceTrigger = UpdateSourceTrigger.LostFocus 
        };
        
        var template = new FuncDataTemplate<ScoreRow>((row, _) =>
        {
            var tb = new TextBox();
            tb.Bind(TextBox.TextProperty, binding);
            
            // Simplified event handler
            tb.LostFocus += (_, __) =>
            {
                row?.Touch();
                vm.NotifyScoreEdited();
            };
            return tb;
        });
        return template;
    }

    // Singleton converter to avoid multiple instances
    static class DoubleConverterCache
    {
        public static readonly IValueConverter Instance = new NullableDoubleStringConverter();
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
