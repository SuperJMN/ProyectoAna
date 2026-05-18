using System.Collections.Generic;

namespace EvaluacionesApp.Desktop.Features.InitialSetup;

public sealed record InitialSetupCourseDraft(string Name, int? Number);

public sealed record InitialSetupCourseClassesDraft(
    string Name,
    int? Number,
    IReadOnlyList<string> Classes);

public sealed record InitialSetupDraft(IReadOnlyList<InitialSetupCourseClassesDraft> Courses);
