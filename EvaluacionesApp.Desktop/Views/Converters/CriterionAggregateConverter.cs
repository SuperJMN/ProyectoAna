using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Data.Converters;
using EvaluacionesApp.Desktop.Dynamic;
using EvaluacionesApp.Desktop.ViewModels;
using EvaluacionesApp.Desktop.Views.Grades;

namespace EvaluacionesApp.Desktop.Views.Converters;

public class CriterionAggregateConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2)
        {
            return string.Empty;
        }

        if (values[0] is not ScopedCriterionNode node || values[1] is not ScoreRow row)
        {
            return string.Empty;
        }

        var result = Compute(node, row);
        return result.HasValue ? result.Value.ToString("F2", culture) : string.Empty;
    }

    static decimal? Compute(ScopedCriterionNode node, ScoreRow row)
    {
        if (node.Children.Count == 0)
        {
            return row.GetScore(node.Criterion.Id);
        }

        // Weighted sum of children using normalized weights.
        // Ignore children without value and renormalize to the sum of present weights.
        var present = new List<(decimal value, decimal weight)>();
        foreach (var child in node.Children)
        {
            var val = Compute(child, row);
            if (val.HasValue)
            {
                var w = child.Criterion.Weight;
                if (w < 0m) w = 0m; // no pesos negativos
                present.Add((val.Value, w));
            }
        }

        if (present.Count == 0)
        {
            return null;
        }

        var weightSum = present.Sum(t => t.weight);
        if (weightSum <= 0m)
        {
            // Reparto uniforme si todos los pesos son 0
            return present.Average(t => t.value);
        }

        decimal weighted = 0m;
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
        if (values[0] is not ScoreRow row)
        {
            return null;
        }

        var criterion = values[1] switch
        {
            ScopedCriterionNode node => node.Criterion,
            DynamicCriterion dynamicCriterion => dynamicCriterion,
            _ => null
        };

        return criterion == null ? null : row[criterion.Id];
    }
}
