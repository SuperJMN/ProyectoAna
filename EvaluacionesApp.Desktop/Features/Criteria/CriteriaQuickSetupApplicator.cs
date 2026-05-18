using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EvaluacionesApp.Desktop.Dynamic;
using EvaluacionesApp.Desktop.Persistence;

namespace EvaluacionesApp.Desktop.Features.Criteria;

public sealed class CriteriaQuickSetupApplicator
{
    private readonly IDynamicSchoolStore store;

    public CriteriaQuickSetupApplicator(IDynamicSchoolStore store)
    {
        this.store = store;
    }

    public async Task Apply(DynamicCourse course, int term, IReadOnlyList<string> names, CancellationToken cancellationToken = default)
    {
        if (names.Count == 0)
        {
            return;
        }

        foreach (var name in names)
        {
            course.AddCriterion(new Criterion
            {
                Id = Guid.NewGuid().ToString(),
                Name = name,
                Weight = 1,
                ClassId = string.Empty,
                Term = term
            });
        }

        await store.SaveAsync(cancellationToken);
    }
}
