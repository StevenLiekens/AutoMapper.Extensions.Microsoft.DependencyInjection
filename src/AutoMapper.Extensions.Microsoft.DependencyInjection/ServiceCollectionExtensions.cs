﻿namespace AutoMapper
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Microsoft.Extensions.DependencyInjection;
#if DEPENDENCY_MODEL
    using Microsoft.Extensions.DependencyModel;
#endif

    public static class ServiceCollectionExtensions
    {
        private static readonly Assembly AutoMapperAssembly = typeof(Mapper).GetTypeInfo().Assembly;

        private static readonly Action<IMapperConfigurationExpression> DefaultConfig = cfg => { };
#if DEPENDENCY_MODEL

        private static HashSet<string> ReferenceAssemblies { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            //"Microsoft.AspNetCore.Mvc",
            //"Microsoft.AspNetCore.Mvc.Abstractions",
            //"Microsoft.AspNetCore.Mvc.ApiExplorer",
            //"Microsoft.AspNetCore.Mvc.Core",
            //"Microsoft.AspNetCore.Mvc.Cors",
            //"Microsoft.AspNetCore.Mvc.DataAnnotations",
            //"Microsoft.AspNetCore.Mvc.Formatters.Json",
            //"Microsoft.AspNetCore.Mvc.Formatters.Xml",
            //"Microsoft.AspNetCore.Mvc.Localization",
            //"Microsoft.AspNetCore.Mvc.Razor",
            //"Microsoft.AspNetCore.Mvc.Razor.Host",
            //"Microsoft.AspNetCore.Mvc.TagHelpers",
            //"Microsoft.AspNetCore.Mvc.ViewFeatures",
            "AutoMapper"
        };

        public static IServiceCollection AddAutoMapper(this IServiceCollection services)
        {
            return services.AddAutoMapper(null, DependencyContext.Default);
        }

        public static IServiceCollection AddAutoMapper(this IServiceCollection services, Action<IMapperConfigurationExpression> additionalInitAction)
        {
            return services.AddAutoMapper(additionalInitAction, DependencyContext.Default);
        }

        public static IServiceCollection AddAutoMapper(this IServiceCollection services, Action<IMapperConfigurationExpression> additionalInitAction, DependencyContext dependencyContext)
        {
            return services.AddAutoMapper(additionalInitAction, GetCandidateAssemblies(dependencyContext));
        }

        private static IEnumerable<Assembly> GetCandidateAssemblies(DependencyContext dependencyContext)
        {
            return GetCandidateLibraries(dependencyContext)
                .SelectMany(library => library.GetDefaultAssemblyNames(dependencyContext))
                .Select(Assembly.Load);
        }

        private static IEnumerable<RuntimeLibrary> GetCandidateLibraries(DependencyContext dependencyContext)
        {
            if (ReferenceAssemblies == null)
            {
                return Enumerable.Empty<RuntimeLibrary>();
            }

            var candidatesResolver = new CandidateResolver(dependencyContext.RuntimeLibraries, ReferenceAssemblies);
            return candidatesResolver.GetCandidates();
        }

        private class CandidateResolver
        {
            private readonly IDictionary<string, Dependency> _dependencies;

            public CandidateResolver(IReadOnlyList<RuntimeLibrary> dependencies, ISet<string> referenceAssemblies)
            {
                var dependenciesWithNoDuplicates = new Dictionary<string, Dependency>(StringComparer.OrdinalIgnoreCase);
                foreach (var dependency in dependencies)
                {
                    if (dependenciesWithNoDuplicates.ContainsKey(dependency.Name))
                    {
                        throw new InvalidOperationException(
                            $"A duplicate entry for library reference {dependency.Name} was found. Please check that all package references in all projects use the same casing for the same package references.");
                    }
                    dependenciesWithNoDuplicates.Add(dependency.Name, CreateDependency(dependency, referenceAssemblies));
                }

                _dependencies = dependenciesWithNoDuplicates;
            }

            private Dependency CreateDependency(RuntimeLibrary library, ISet<string> referenceAssemblies)
            {
                var classification = DependencyClassification.Unknown;
                if (referenceAssemblies.Contains(library.Name))
                {
                    classification = DependencyClassification.AutoMapperReference;
                }

                return new Dependency(library, classification);
            }

            private DependencyClassification ComputeClassification(string dependency)
            {
                var candidateEntry = _dependencies[dependency];
                if (candidateEntry.Classification != DependencyClassification.Unknown)
                {
                    return candidateEntry.Classification;
                }
                else
                {
                    var classification = DependencyClassification.NotCandidate;
                    foreach (var candidateDependency in candidateEntry.Library.Dependencies)
                    {
                        var dependencyClassification = ComputeClassification(candidateDependency.Name);
                        if (dependencyClassification == DependencyClassification.Candidate ||
                            dependencyClassification == DependencyClassification.AutoMapperReference)
                        {
                            classification = DependencyClassification.Candidate;
                            break;
                        }
                    }

                    candidateEntry.Classification = classification;

                    return classification;
                }
            }

            public IEnumerable<RuntimeLibrary> GetCandidates()
            {
                foreach (var dependency in _dependencies)
                {
                    if (ComputeClassification(dependency.Key) == DependencyClassification.Candidate)
                    {
                        yield return dependency.Value.Library;
                    }
                }
            }

            private class Dependency
            {
                public Dependency(RuntimeLibrary library, DependencyClassification classification)
                {
                    Library = library;
                    Classification = classification;
                }

                public RuntimeLibrary Library { get; }

                public DependencyClassification Classification { get; set; }

                public override string ToString()
                {
                    return $"Library: {Library.Name}, Classification: {Classification}";
                }
            }

            private enum DependencyClassification
            {
                Unknown = 0,
                Candidate = 1,
                NotCandidate = 2,
                AutoMapperReference = 3
            }
        }

#endif

        public static IServiceCollection AddAutoMapper(this IServiceCollection services, params Assembly[] assemblies)
        {
            return AddAutoMapperClasses(services, null, assemblies);
        }

        public static IServiceCollection AddAutoMapper(this IServiceCollection services, Action<IMapperConfigurationExpression> additionalInitAction, params Assembly[] assemblies)
        {
            return AddAutoMapperClasses(services, additionalInitAction, assemblies);
        }

        public static IServiceCollection AddAutoMapper(this IServiceCollection services, Action<IMapperConfigurationExpression> additionalInitAction, IEnumerable<Assembly> assemblies)
        {
            return AddAutoMapperClasses(services, additionalInitAction, assemblies);
        }

        public static IServiceCollection AddAutoMapper(this IServiceCollection services, params Type[] profileAssemblyMarkerTypes)
        {
            return AddAutoMapperClasses(services, null, profileAssemblyMarkerTypes.Select(t => t.GetTypeInfo().Assembly));
        }

        public static IServiceCollection AddAutoMapper(this IServiceCollection services, Action<IMapperConfigurationExpression> additionalInitAction, params Type[] profileAssemblyMarkerTypes)
        {
            return AddAutoMapperClasses(services, additionalInitAction, profileAssemblyMarkerTypes.Select(t => t.GetTypeInfo().Assembly));
        }

        public static IServiceCollection AddAutoMapper(this IServiceCollection services, Action<IMapperConfigurationExpression> additionalInitAction, IEnumerable<Type> profileAssemblyMarkerTypes)
        {
            return AddAutoMapperClasses(services, additionalInitAction, profileAssemblyMarkerTypes.Select(t => t.GetTypeInfo().Assembly));
        }


        private static IServiceCollection AddAutoMapperClasses(IServiceCollection services, Action<IMapperConfigurationExpression> additionalInitAction, IEnumerable<Assembly> assembliesToScan)
        {
            additionalInitAction = additionalInitAction ?? DefaultConfig;
            assembliesToScan = assembliesToScan as Assembly[] ?? assembliesToScan.ToArray();

            var allTypes = assembliesToScan.SelectMany(a => a.ExportedTypes).ToArray();

            var profiles =
                allTypes
                    .Where(t => typeof(Profile).GetTypeInfo().IsAssignableFrom(t.GetTypeInfo()))
                    .Where(t => !t.GetTypeInfo().IsAbstract);

            Mapper.Initialize(cfg =>
            {
                additionalInitAction(cfg);

                foreach (var profile in profiles)
                {
                    cfg.AddProfile(profile);
                }
            });

            var openTypes = new[]
            {
                typeof(IValueResolver<,,>),
                typeof(IMemberValueResolver<,,,>),
                typeof(ITypeConverter<,>)
            };
            foreach (var openType in openTypes)
            {
                foreach (var type in allTypes
                    .Where(t => t.GetTypeInfo().IsClass)
                    .Where(t => !t.GetTypeInfo().IsAbstract)
                    .Where(t => t.ImplementsGenericInterface(openType)))
                {
                    services.AddTransient(type);
                }
            }

            services.AddSingleton(Mapper.Configuration);
            return services.AddScoped<IMapper>(sp => new Mapper(sp.GetRequiredService<IConfigurationProvider>(), sp.GetService));
        }

        private static bool ImplementsGenericInterface(this Type type, Type interfaceType)
        {
            if (type.IsGenericType(interfaceType))
            {
                return true;
            }
            foreach (var @interface in type.GetTypeInfo().ImplementedInterfaces)
            {
                if (@interface.IsGenericType(interfaceType))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsGenericType(this Type type, Type genericType)
        {
            return type.GetTypeInfo().IsGenericType && type.GetGenericTypeDefinition() == genericType;
        }


    }
}