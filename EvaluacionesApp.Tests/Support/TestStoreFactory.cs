using System;
using System.IO;
using System.Reflection;
using EvaluacionesApp.Desktop.Dynamic;
using EvaluacionesApp.Desktop.Persistence;
using EvaluacionesApp.Desktop.Services;

namespace EvaluacionesApp.Tests.Support;

public static class TestStoreFactory
{
    public static DynamicSchoolStore Create(Root root)
    {
        var path = Path.Combine(Path.GetTempPath(), $"evaluaciones-tests-{Guid.NewGuid():N}.json");
        var persistence = new PersistenceService(path);
        var store = new DynamicSchoolStore(persistence);
        var dynamicRoot = new DynamicRoot(root);
        typeof(DynamicSchoolStore)
            .GetField("root", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(store, dynamicRoot);
        return store;
    }
}
