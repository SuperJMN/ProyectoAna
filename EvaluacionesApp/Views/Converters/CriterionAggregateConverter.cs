using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Data.Converters;
using EvaluacionesApp.Dynamic;
using EvaluacionesApp.Views.Grades;

namespace EvaluacionesApp.Views.Converters;

public class CriterionAggregateConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2) return string.Empty;
        var criterion = values[0] as DynamicCriterion;
        var row = values[1] as ScoreRow;
        if (criterion == null || row == null) return string.Empty;
        var result = Compute(criterion, row);
        return result.HasValue ? result.Value.ToString("F2", culture) : string.Empty;
    }

    static double? Compute(DynamicCriterion criterion, ScoreRow row)
    {
        if (criterion.Children.Count == 0)
        {
            return row.GetScore(criterion.Id);
        }

        // Weighted sum of children using normalized weights.
        // Ignore children without value and renormalize to the sum of present weights.
        var present = new List<(double value, double weight)>();
        foreach (var child in criterion.Children)
        {
            var val = Compute(child, row);
            if (val.HasValue)
            {
                var w = child.Weight;
                if (w < 0) w = 0; // no pesos negativos
                present.Add((val.Value, w));
            }
        }

        if (present.Count == 0)
        {
            return null;
        }

        var weightSum = present.Sum(t => t.weight);
        if (weightSum <= 0)
        {
            // Reparto uniforme si todos los pesos son 0
            return present.Average(t => t.value);
        }

        double weighted = 0;
        foreach (var (value, weight) in present)
        {
            weighted += value * (weight / weightSum);
        }
        return weighted;
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

public class IsLeafConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // True si la colección está vacía (nodo hoja)
        if (value is System.Collections.ICollection coll)
        {
            return coll.Count == 0;
        }
        if (value is System.Collections.IEnumerable en)
        {
            var e = en.GetEnumerator();
            return !e.MoveNext();
        }
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

// Devuelve un ScoreBinding para un par (ScoreRow, Criterion)
public class RowCriterionToBindingConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2) return null;
        var row = values[0] as ScoreRow;
        var criterion = values[1] as DynamicCriterion;
        if (row == null || criterion == null) return null;
        return row[criterion.Id];
    }
}

public class ScopedChildrenConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 3)
        {
            return Array.Empty<DynamicCriterion>();
        }

        if (values[0] is not DynamicCriterion criterion)
        {
            return Array.Empty<DynamicCriterion>();
        }

        var classId = values[1] as string;
        if (values[2] is not int selectedTerm)
        {
            return Array.Empty<DynamicCriterion>();
        }

        return criterion.FilterChildren(classId, selectedTerm).ToList();
    }
}
