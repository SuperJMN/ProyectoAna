using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Threading.Tasks;
using DynamicData;
using DynamicData.Binding;
using EvaluacionesApp.Desktop.Dynamic;
using EvaluacionesApp.Desktop.Persistence;
using EvaluacionesApp.Desktop.ViewModels;
using ReactiveUI;

namespace EvaluacionesApp.Desktop.Features.Criteria.Operations;

public sealed class CriteriaCopyFeatureViewModel : ReactiveObject, IDisposable
{
    private static readonly ReadOnlyObservableCollection<MenuViewModel> EmptyCopyMenuItems = new(new ObservableCollection<MenuViewModel>());
    
    private readonly CompositeDisposable anchors = new();
    private readonly SourceCache<MenuViewModel, string> copyMenuCache = new(menu => menu.Key);
    private readonly Dictionary<CourseCriterionCopyTarget, CourseCopyMenuViewModel> copyMenusByTarget = new();
    private readonly Dictionary<DynamicCourse, CourseCriterionCopyTarget> courseCopyTargetsByCourse = new();
    private readonly SourceCache<CourseCriterionCopyTarget, string> courseCopyTargetsCache = new(target => target.CourseId);
    private readonly IDynamicSchoolStore store;
    private readonly ReadOnlyObservableCollection<MenuViewModel> copyCriteriaMenu = EmptyCopyMenuItems;
    private readonly IObservable<IReadOnlyList<ScopedCriterionNode>> criteriaObservable;
    private readonly IObservable<DynamicCourse?> selectedCourseObservable;

    public CriteriaCopyFeatureViewModel(
        IDynamicSchoolStore store,
        IObservable<IReadOnlyList<ScopedCriterionNode>> criteriaObservable,
        IObservable<DynamicCourse?> selectedCourseObservable)
    {
        this.store = store;
        this.criteriaObservable = criteriaObservable;
        this.selectedCourseObservable = selectedCourseObservable;
        
        var hasCourse = selectedCourseObservable.Select(course => course != null);
        CopyCriteria = ReactiveCommand.CreateFromTask<CriterionCopyTarget?>(DoCopyCriteria, hasCourse);

        copyMenuCache.Connect()
            .DisposeMany()
            .AutoRefresh(menu => ((CourseCopyMenuViewModel)menu).CourseOrder)
            .AutoRefresh(menu => menu.Header)
            .SortAndBind(out copyCriteriaMenu, SortExpressionComparer<MenuViewModel>
                .Ascending(menu => ((CourseCopyMenuViewModel)menu).CourseOrder)
                .ThenByAscending(menu => menu.Header))
            .Subscribe()
            .DisposeWith(anchors);
    }

    public ReadOnlyObservableCollection<MenuViewModel> CopyCriteriaMenu => copyCriteriaMenu;
    
    public ReactiveCommand<CriterionCopyTarget?, Unit> CopyCriteria { get; }

    public void RegisterCourse(DynamicCourse course)
    {
        GetOrCreateCourseCopyTarget(course);
    }

    public void UnregisterCourse(DynamicCourse course)
    {
        RemoveCourseCopyTarget(course);
    }

    public void Dispose()
    {
        anchors.Dispose();
        foreach (var target in courseCopyTargetsCache.Items.ToList()) 
        {
            target.Dispose();
        }

        courseCopyTargetsCache.Dispose();
        courseCopyTargetsByCourse.Clear();
        copyMenuCache.Dispose();
        copyMenusByTarget.Clear();
    }

    private CourseCriterionCopyTarget GetOrCreateCourseCopyTarget(DynamicCourse course)
    {
        if (courseCopyTargetsByCourse.TryGetValue(course, out var existing))
        {
            return existing;
        }

        var target = new CourseCriterionCopyTarget(course);
        courseCopyTargetsByCourse[course] = target;
        courseCopyTargetsCache.AddOrUpdate(target);
        
        // Auto-register menu
        RegisterMenu(target, CopyCriteria);
        
        return target;
    }

    private void RemoveCourseCopyTarget(DynamicCourse course)
    {
        if (!courseCopyTargetsByCourse.Remove(course, out var target))
        {
            return;
        }

        RemoveCourseCopyMenu(target);
        courseCopyTargetsCache.RemoveKey(course.Id);
        target.Dispose();
    }

    private void RemoveCourseCopyMenu(CourseCriterionCopyTarget target)
    {
        if (!copyMenusByTarget.Remove(target, out var menu))
        {
            return;
        }

        copyMenuCache.RemoveKey(menu.Key);
    }

    private void RegisterMenu(CourseCriterionCopyTarget target, ReactiveCommand<CriterionCopyTarget?, Unit> command)
    {
        if (copyMenusByTarget.ContainsKey(target))
        {
            return;
        }

        var menu = new CourseCopyMenuViewModel(target, command);
        copyMenusByTarget[target] = menu;
        copyMenuCache.AddOrUpdate(menu);
    }

    private async Task DoCopyCriteria(CriterionCopyTarget? target)
    {
        if (target == null)
        {
            return;
        }

        var currentCourse = await selectedCourseObservable.Take(1);
        if (currentCourse == null)
        {
            return;
        }

        var currentCriteria = await criteriaObservable.Take(1);
        
        var destinationCourse = target.Course;
        var destinationTerm = target.Term;

        var clones = currentCriteria
            .Select(node => CloneCriterion(node, destinationTerm))
            .ToList();

        var toRemove = destinationCourse.Criteria
            .Where(rootCriterion => rootCriterion.MatchesTreeScope(destinationTerm))
            .ToList();

        foreach (var criterion in toRemove)
        {
            destinationCourse.RemoveCriterion(criterion);
        }

        foreach (var clone in clones)
        {
            destinationCourse.AddCriterion(clone);
        }

        await store.SaveAsync();
    }

    private Criterion CloneCriterion(ScopedCriterionNode node, int destinationTerm)
    {
        var criterion = node.Criterion;
        var term = criterion.Term.HasValue ? destinationTerm : criterion.Term;

        return new Criterion
        {
            Id = Guid.NewGuid().ToString(),
            Name = criterion.Name,
            Weight = criterion.Weight,
            ClassId = string.Empty,
            Term = term,
            Children = node.Children
                .Select(child => CloneCriterion(child, destinationTerm))
                .ToList()
        };
    }
}
