using EvaluacionesApp.Desktop.Features.Criteria;
using EvaluacionesApp.Desktop.Persistence;
using EvaluacionesApp.Tests.Support;

namespace EvaluacionesApp.Tests;

public sealed class CriteriaQuickSetupTests
{
    [Fact]
    public void Criteria_name_parser_ignores_empty_lines()
    {
        var names = CriteriaNameParser.Parse("""
            Trabajo diario

            Examen
            Proyecto
            """);

        Assert.Equal(["Trabajo diario", "Examen", "Proyecto"], names);
    }

    [Fact]
    public async Task Criteria_quick_setup_creates_root_criteria_for_selected_term_and_saves_once()
    {
        using var store = RecordingSchoolStore.FromDomain(new Root
        {
            Courses =
            [
                new Course
                {
                    Id = "course-1",
                    Name = "1 ESO",
                    Terms = [1, 2, 3],
                    Criteria =
                    [
                        new Criterion { Id = "term-1", Name = "Term 1", Weight = 1, ClassId = string.Empty, Term = 1 }
                    ]
                }
            ]
        });
        var course = store.Root.Courses.Single();
        var applicator = new CriteriaQuickSetupApplicator(store);

        await applicator.Apply(course, 2, ["Trabajo diario", "Examen"]);

        Assert.Equal(3, course.Criteria.Count);
        Assert.Contains(course.Criteria, criterion => criterion.Name == "Term 1" && criterion.Term == 1);
        Assert.Collection(
            course.Criteria.Where(criterion => criterion.Term == 2).OrderBy(criterion => criterion.Name),
            criterion =>
            {
                Assert.Equal("Examen", criterion.Name);
                Assert.Equal(1, criterion.Weight);
                Assert.Equal(string.Empty, criterion.ClassId);
                Assert.Empty(criterion.Children);
            },
            criterion =>
            {
                Assert.Equal("Trabajo diario", criterion.Name);
                Assert.Equal(1, criterion.Weight);
                Assert.Equal(string.Empty, criterion.ClassId);
                Assert.Empty(criterion.Children);
            });
        Assert.Equal(1, store.SaveCount);
    }
}
