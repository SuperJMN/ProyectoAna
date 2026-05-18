using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EvaluacionesApp.Desktop.Dynamic;
using EvaluacionesApp.Desktop.Persistence;
using EvaluacionesApp.Desktop.ViewModels;

namespace EvaluacionesApp.Desktop.Features.InitialSetup;

public sealed class InitialSetupApplicator
{
    private readonly IDynamicSchoolStore store;
    private readonly SchoolSelectionState selection;

    public InitialSetupApplicator(IDynamicSchoolStore store, SchoolSelectionState selection)
    {
        this.store = store;
        this.selection = selection;
    }

    public async Task<InitialSetupResult> Apply(InitialSetupDraft draft, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var created = draft.Courses
            .Select(courseDraft =>
            {
                var courseId = NextCourseId();
                return store.Root.AddCourse(new Course
                {
                    Id = courseId,
                    Name = courseDraft.Name,
                    Number = courseDraft.Number,
                    Terms = [1, 2, 3],
                    Classes = courseDraft.Classes
                        .Select((className, index) => new Class
                        {
                            Id = $"{courseId}-class-{index + 1}",
                            Name = className
                        })
                        .ToList()
                });
            })
            .ToList();

        var firstCourse = created.FirstOrDefault();
        var firstClass = firstCourse?.Classes.FirstOrDefault();

        selection.SelectedCourse = firstCourse;
        selection.SelectedClass = firstClass;

        await store.SaveAsync(cancellationToken);
        return new InitialSetupResult(created, firstCourse, firstClass);
    }

    string NextCourseId()
    {
        var index = store.Root.Courses.Count + 1;
        string id;
        do
        {
            id = $"course-{index}";
            index++;
        }
        while (store.Root.Courses.Any(course => course.Id == id));

        return id;
    }
}

public sealed record InitialSetupResult(
    IReadOnlyList<DynamicCourse> Courses,
    DynamicCourse? SelectedCourse,
    DynamicClass? SelectedClass);
