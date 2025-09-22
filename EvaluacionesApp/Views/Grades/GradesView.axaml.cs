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

        this.WhenActivated(disposables =>
        {
            // Reload on first activation (when attached to visual tree)
            var attachSub = Observable.FromEventPattern<Avalonia.VisualTreeAttachmentEventArgs>(
                    h => this.AttachedToVisualTree += h,
                    h => this.AttachedToVisualTree -= h)
                .Select(_ => Unit.Default)
                .InvokeCommand(this, x => x.ViewModel!.Reload);
            disposables.Add(attachSub);

            // Rebuild columns reactively when course/class changes
            var rebuildSub = this.WhenAnyValue(x => x.ViewModel)
                .WhereNotNull()
                .SelectMany(vm => Observable.CombineLatest(
                        vm.WhenAnyValue(v => v.SelectedCourse),
                        vm.WhenAnyValue(v => v.SelectedClass),
                        (c, cl) => (c, cl)))
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(_ => BuildDynamicColumns());
            disposables.Add(rebuildSub);

            // Also rebuild when rows are rebuilt (after Reload or selection changes)
            var rowsSub = this.WhenAnyValue(x => x.ViewModel!.ScoreRows)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(_ => BuildDynamicColumns());
            disposables.Add(rowsSub);

            // Delay initial build to ensure control is fully loaded
            var timerSub = Observable.Timer(TimeSpan.FromMilliseconds(100))
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(_ => BuildDynamicColumns());
            disposables.Add(timerSub);
        });
    }

    string? lastCourseId = null;
    int lastCriteriaCount = -1;

    void BuildDynamicColumns()
    {
        if (ScoresGrid == null) return;
        if (ViewModel is not GradesViewModel vm) return;

        var currentCourseId = vm.SelectedCourse?.Id.ToString();
        var leaves = vm.SelectedCourse?.Criteria is null
            ? Array.Empty<EvaluacionesApp.Models.Criterion>()
            : GradesViewModel.GetLeafCriteria(vm.SelectedCourse.Criteria).ToArray();

        if (currentCourseId == lastCourseId && leaves.Length == lastCriteriaCount)
        {
            return;
        }

        lastCourseId = currentCourseId;
        lastCriteriaCount = leaves.Length;

        while (ScoresGrid.Columns.Count > 1)
        {
            ScoresGrid.Columns.RemoveAt(ScoresGrid.Columns.Count - 1);
        }

        vm.RecomputeWeights();

        foreach (var c in leaves)
        {
            var col = new Column
            {
                Header = c.Name,
                Width = GridLength.Star,
                Binding = new Binding(".") { Mode = BindingMode.OneWay },
                CellTemplate = CreateEditableTextTemplate(c.Id.ToString())
            };
            ScoresGrid.Columns.Add(col);
        }

        ScoresGrid.Columns.Add(new Column
        {
            Header = "Total",
            Width = GridLength.Auto,
            Binding = new Binding("Total") { StringFormat = "F2" }
        });
    }

    IDataTemplate CreateEditableTextTemplate(string criterionId)
    {
        return new FuncDataTemplate<object>((data, _) =>
        {
            if (data is ScoreRow)
            {
                var cell = new ScoreTextBox
                {
                    CriterionId = criterionId
                };
                // Bind the Row property of the ScoreTextBox to the row (DataContext is the row itself)
                cell.Bind(ScoreTextBox.RowProperty, new Binding("."));
                return cell;
            }

            return new TextBlock { Text = string.Empty };
        });
    }
}
