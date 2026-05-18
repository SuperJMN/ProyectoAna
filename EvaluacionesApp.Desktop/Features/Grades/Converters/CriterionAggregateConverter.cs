using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Data;
using Avalonia.Data.Converters;
using EvaluacionesApp.Desktop.Dynamic;
using EvaluacionesApp.Desktop.ViewModels;
using EvaluacionesApp.Desktop.Features.Grades;

namespace EvaluacionesApp.Desktop.Features.Grades.Converters;

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

        var result = CriterionScoreAggregator.Compute(node, row);
        return result.HasValue ? result.Value.ToString("F2", culture) : string.Empty;
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

public class NullableDecimalTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is decimal score ? score.ToString(culture) : string.Empty;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string text)
        {
            return value;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return decimal.TryParse(text, NumberStyles.Number, culture, out var score)
            ? score
            : new BindingNotification(new FormatException("La nota debe ser un numero"), BindingErrorType.DataValidationError);
    }
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
