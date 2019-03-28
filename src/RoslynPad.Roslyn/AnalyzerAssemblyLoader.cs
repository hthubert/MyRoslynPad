using System;
using System.Composition;
using System.Reflection;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace RoslynPad.Roslyn
{
    internal class DesktopAnalyzerAssemblyLoader : MyAnalyzerAssemblyLoader
    {
        private int _hookedAssemblyResolve;

        protected override Assembly LoadFromPathImpl(string fullPath)
        {
            if (Interlocked.CompareExchange(ref _hookedAssemblyResolve, 0, 1) == 0)
            {
                AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
            }

            return LoadImpl(fullPath);
        }

        protected virtual Assembly LoadImpl(string fullPath) => Assembly.LoadFrom(fullPath);

        private Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            try
            {
                return Load(AppDomain.CurrentDomain.ApplyPolicy(args.Name));
            }
            catch
            {
                return null;
            }
        }
    }

    [Export(typeof(IAnalyzerAssemblyLoader)), Shared]
    public class AnalyzerAssemblyLoader : IAnalyzerAssemblyLoader
    {
        // TODO: .NET Core?
        private readonly IAnalyzerAssemblyLoader _impl = new DesktopAnalyzerAssemblyLoader();

        public void AddDependencyLocation(string fullPath)
        {
            _impl.AddDependencyLocation(fullPath);
        }

        public Assembly LoadFromPath(string fullPath)
        {
            return _impl.LoadFromPath(fullPath);
        }
    }
}
