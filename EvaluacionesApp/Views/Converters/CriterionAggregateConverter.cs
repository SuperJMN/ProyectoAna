using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using EvaluacionesApp.Models;
using EvaluacionesApp.Views.Grades;

namespace EvaluacionesApp.Views.Converters;

public class CriterionAggregateConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2) return string.Empty;
        var criterion = values[0] as Criterion;
        var row = values[1] as ScoreRow;
        if (criterion == null || row == null) return string.Empty;
        var result = Compute(criterion, row);
        return result.HasValue ? result.Value.ToString("F2", culture) : string.Empty;
    }

    static double? Compute(Criterion criterion, ScoreRow row)
    {
        if (criterion.Children.Count == 0)
        {
            return row.Scores.TryGetValue(criterion.Id, out var v) ? v : null;
        }

        double sum = 0;
        bool any = false;
        foreach (var child in criterion.Children)
        {
            var val = Compute(child, row);
            if (val.HasValue)
            {
                sum += val.Value;
                any = true;
            }
        }
        return any ? sum : null;
    }
}

public class HasChildrenConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ICollection coll) return coll.Count > 0;
        if (value is IEnumerable<object> seq) return System.Linq.Enumerable.Any(seq);
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

public class NotConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b) return !b;
        if (parameter is IValueConverter inner)
        {
            var innerVal = inner.Convert(value, typeof(bool), null, culture);
            if (innerVal is bool ib) return !ib;
        }
        return value;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
