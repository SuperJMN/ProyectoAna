using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using EvaluacionesApp.Models;
using EvaluacionesApp.Services;

namespace EvaluacionesApp.Views.Maintenance;

public partial class CriteriaViewModel : ReactiveObject
{
    public ObservableCollection<Course> Courses { get; } = new();
    [Reactive] private Course? selectedCourse;
    [Reactive] private Criterion? selectedCriterion;

    public ReactiveCommand<Unit, Unit> AddRootCriterion { get; }
    public ReactiveCommand<Unit, Unit> AddChildCriterion { get; }
    public ReactiveCommand<Unit, Unit> DeleteCriterion { get; }

    readonly PersistenceService persistence;

    public CriteriaViewModel(PersistenceService persistence)
    {
        this.persistence = persistence;
        AddRootCriterion = ReactiveCommand.Create(DoAddRootCriterion, this.WhenAnyValue(x => x.SelectedCourse).Select(c => c != null));
        AddChildCriterion = ReactiveCommand.Create(DoAddChildCriterion, this.WhenAnyValue(x => x.SelectedCriterion).Select(c => c != null));
        DeleteCriterion = ReactiveCommand.Create(DoDeleteCriterion, this.WhenAnyValue(x => x.SelectedCriterion).Select(c => c != null));
        Load();
    }

    async void Load()
    {
        var root = await persistence.Load();
        Courses.Clear();
        foreach (var c in root.Courses) Courses.Add(c);
        SelectedCourse = Courses.FirstOrDefault();
        SelectedCriterion = SelectedCourse?.Criteria.FirstOrDefault();
    }

    void DoAddRootCriterion()
    {
        if (SelectedCourse == null) return;
        var idx = SelectedCourse.Criteria.Count + 1;
        var criterion = new Criterion { Id = $"C{idx}", Name = $"Criterion {idx}", Weight = 1 };
        SelectedCourse.Criteria.Add(criterion);
        SelectedCriterion = criterion;
        Save();
    }

    void DoAddChildCriterion()
    {
        if (SelectedCriterion == null) return;
        var parent = SelectedCriterion;
        var idx = parent.Children.Count + 1;
        var child = new Criterion { Id = $"{parent.Id}.{idx}", Name = $"Subcriterion {idx}", Weight = 1 };
        parent.Children.Add(child);
        SelectedCriterion = child;
        Save();
    }

    void DoDeleteCriterion()
    {
        if (SelectedCourse == null || SelectedCriterion == null) return;
        var criterion = SelectedCriterion;
        if (criterion.Children.Any())
        {
            // Cannot delete if has children
            return;
        }
        // Cannot delete if any class has assessments for this criterion
        var hasAssessments = SelectedCourse.Classes
            .SelectMany(c => c.Assessments)
            .Any(a => a.CriterionId == criterion.Id);
        if (hasAssessments)
        {
            return;
        }
        // Remove from tree
        RemoveCriterion(SelectedCourse.Criteria, criterion);
        SelectedCriterion = null;
        Save();
    }

    static bool RemoveCriterion(System.Collections.Generic.IList<Criterion> nodes, Criterion toRemove)
    {
        if (nodes.Remove(toRemove)) return true;
        foreach (var n in nodes.ToList())
        {
            if (RemoveCriterion(n.Children, toRemove)) return true;
        }
        return false;
    }

    async void Save()
    {
        var root = new Root { Courses = Courses.ToList() };
        await persistence.Save(root);
    }
    
    public async void SaveCommand()
    {
        var root = new Root { Courses = Courses.ToList() };
        await persistence.Save(root);
    }
}
