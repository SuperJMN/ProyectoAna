using Zafiro.Avalonia.Wizards.Graph.Core;
using WizardGraph = Zafiro.Avalonia.Wizards.Graph.Core.GraphWizard;

namespace EvaluacionesApp.Desktop.Features.InitialSetup;

public sealed class InitialSetupWizardFactory
{
    public GraphWizard<InitialSetupDraft> Create()
    {
        var graph = WizardGraph.For<InitialSetupDraft>();
        var courses = new InitialSetupCoursesStepViewModel();
        var coursesNode = graph.Step(courses, "Cursos")
            .Next(step =>
                {
                    var classes = new InitialSetupClassesStepViewModel(step.CreateDraft());
                    return graph.Step(classes, "Clases")
                        .Finish(step => step.CreateDraft(), classes.IsValid, "Crear")
                        .Build();
                },
                courses.IsValid,
                "Siguiente")
            .Build();

        return new GraphWizard<InitialSetupDraft>(coursesNode);
    }
}
