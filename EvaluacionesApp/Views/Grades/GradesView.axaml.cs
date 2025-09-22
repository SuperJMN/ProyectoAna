using System;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml.Templates;
using EvaluacionesApp.Views.Grades;
using Zafiro.Avalonia.Controls.SlimDataGrid;
using Zafiro.Avalonia.Controls;
using Avalonia.VisualTree;
using Avalonia.Markup.Xaml;
using System.Reactive.Linq;
namespace EvaluacionesApp.Views.Grades;

public partial class GradesView : UserControl
{
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
    private IDisposable? subscription;
    
    void BuildDynamicColumns()
    {
        if (ScoresGrid == null) return;
        if (DataContext is not GradesViewModel vm) return;

        var sw = System.Diagnostics.Stopwatch.StartNew();
        
        // Check if we need to rebuild columns
        var currentCourseId = vm.SelectedCourse?.Id.ToString();
        var leaves = vm.SelectedCourse?.Criteria is null ? Array.Empty<EvaluacionesApp.Models.Criterion>() : GradesViewModel.GetLeafCriteria(vm.SelectedCourse.Criteria).ToArray();
        
        if (currentCourseId == lastCourseId && leaves.Length == lastCriteriaCount)
        {
            Console.WriteLine($"[PERF] Skip column rebuild - same course and criteria count");
            return;
        }
        
        lastCourseId = currentCourseId;
        lastCriteriaCount = leaves.Length;
        
        // Clear all columns except the first (Alumno)
        while (ScoresGrid.Columns.Count > 1)
        {
            ScoresGrid.Columns.RemoveAt(ScoresGrid.Columns.Count - 1);
        }
        Console.WriteLine($"[PERF] Clear columns: {sw.ElapsedMilliseconds}ms");
        sw.Restart();

        // Already computed above
        Console.WriteLine($"[PERF] Get leaves: {sw.ElapsedMilliseconds}ms, Count: {leaves.Length}");
        sw.Restart();
        
        vm.RecomputeWeights();
        Console.WriteLine($"[PERF] Recompute weights: {sw.ElapsedMilliseconds}ms");
        sw.Restart();

        foreach (var c in leaves)
        {
            var col = new Column
            {
                Header = c.Name,
                Width = GridLength.Star,
                // Bind to self to get the whole row
                Binding = new Binding(".")
                {
                    Mode = BindingMode.OneWay
                },
                CellTemplate = CreateEditableTextTemplate(c.Id.ToString(), vm)
            };
            ScoresGrid.Columns.Add(col);
        }
        Console.WriteLine($"[PERF] Add {leaves.Length} criterion columns: {sw.ElapsedMilliseconds}ms");
        sw.Restart();

        // Total column
        ScoresGrid.Columns.Add(new Column
        {
            Header = "Total",
            Width = GridLength.Auto,
            Binding = new Binding("Total") { StringFormat = "F2" }
        });
        
        Console.WriteLine($"[PERF] Total BuildDynamicColumns: {sw.Elapsed.TotalMilliseconds}ms");
    }

    private IDataTemplate CreateEditableTextTemplate(string criterionId, GradesViewModel vm)
    {
        // Direct approach without relying on SlimDataGrid's binding
        return new FuncDataTemplate<object>((data, _) =>
        {
            var textBox = new TextBox();
            
            // The data parameter should be the entire row (ScoreRow)
            if (data is ScoreRow row)
            {
                // Set initial value
                if (row.Scores.TryGetValue(criterionId, out var score))
                {
                    textBox.Text = score?.ToString() ?? string.Empty;
                }
                
                // Handle text changes directly
                bool isUpdating = false;
                textBox.TextChanged += (sender, e) =>
                {
                    if (isUpdating) return;
                    
                    var tb = sender as TextBox;
                    if (tb != null)
                    {
                        var text = tb.Text;
                        Console.WriteLine($"[DEBUG] TextChanged for criterion {criterionId}: '{text}' (Student: {row.Student.Name})");
                    }
                };
                
                // Handle lost focus to save changes
                textBox.LostFocus += (sender, e) =>
                {
                    var tb = sender as TextBox;
                    if (tb != null)
                    {
                        isUpdating = true;
                        var text = tb.Text;
                        Console.WriteLine($"[DEBUG] LostFocus for criterion {criterionId}: '{text}' (Student: {row.Student.Name})");
                        
                        if (double.TryParse(text, out var newValue))
                        {
                            row.Scores[criterionId] = newValue;
                            Console.WriteLine($"[DEBUG] Set score to {newValue} for student {row.Student.Name}");
                        }
                        else if (string.IsNullOrWhiteSpace(text))
                        {
                            row.Scores[criterionId] = null;
                            Console.WriteLine($"[DEBUG] Cleared score for student {row.Student.Name}");
                        }
                        
                        row.Touch();
                        vm.NotifyScoreEdited();
                        Console.WriteLine($"[DEBUG] Notified score edit");
                        isUpdating = false;
                    }
                };
            }
            else
            {
                Console.WriteLine($"[DEBUG] Warning: data is not ScoreRow, it's {data?.GetType()?.Name ?? "null"}");
            }
            
            return textBox;
        });
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
