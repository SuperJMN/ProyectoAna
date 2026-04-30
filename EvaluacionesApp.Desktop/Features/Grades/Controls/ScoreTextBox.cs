using System;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using EvaluacionesApp.Desktop.Features.Grades;
using ReactiveUI;

namespace EvaluacionesApp.Desktop.Features.Grades.Controls;

// A reusable, MVVM-friendly TextBox for editing a ScoreRow cell reactively
public class ScoreTextBox : TextBox
{
    public static readonly StyledProperty<ScoreRow?> RowProperty =
        AvaloniaProperty.Register<ScoreTextBox, ScoreRow?>(nameof(Row));

    public ScoreRow? Row
    {
        get => GetValue(RowProperty);
        set => SetValue(RowProperty, value);
    }

    public static readonly StyledProperty<string?> CriterionIdProperty =
        AvaloniaProperty.Register<ScoreTextBox, string?>(nameof(CriterionId));

    public string? CriterionId
    {
        get => GetValue(CriterionIdProperty);
        set => SetValue(CriterionIdProperty, value);
    }

    bool isUpdating;

    public ScoreTextBox()
    {
        // Update Text when Row or CriterionId changes
        this.GetObservable(RowProperty)
            .Merge(this.GetObservable(CriterionIdProperty).Select(_ => Row))
            .Subscribe(_ => UpdateFromRow());

        // Reactive commit on typing with throttle
        this.GetObservable(TextProperty)
            .Skip(1)
            .Throttle(TimeSpan.FromMilliseconds(300), RxSchedulers.MainThreadScheduler)
            .Subscribe(_ => Commit());

        // Immediate commit on losing focus
        this.LostFocus += (_, __) => Commit();
    }

    void UpdateFromRow()
    {
        if (Row == null || string.IsNullOrEmpty(CriterionId)) return;
        var score = Row.GetScore(CriterionId!);
        isUpdating = true;
        Text = score?.ToString() ?? string.Empty;
        isUpdating = false;
    }

    void Commit()
    {
        if (isUpdating) return;
        if (Row == null || string.IsNullOrEmpty(CriterionId)) return;

        var text = Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            Row.SetScore(CriterionId!, null);
            return;
        }

        if (decimal.TryParse(text, out var newValue))
        {
            Row.SetScore(CriterionId!, newValue);
        }
    }
}
