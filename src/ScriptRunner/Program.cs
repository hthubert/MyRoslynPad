using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using RoslynPad.Runtime;

namespace ScriptRunner
{
    class Program
    {
        private static readonly Encoding _encoding;
        private static string _resolvePath;

        static Program()
        {
            var encodingName = ConfigurationManager.AppSettings["Encdoing"];
            if (!string.IsNullOrEmpty(encodingName)) {
                _encoding = Encoding.GetEncoding(encodingName);
            }
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
        }

        private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            if (string.IsNullOrEmpty(_resolvePath)) {
                return null;
            }
            var assemblyName = new AssemblyName(args.Name);
            var file = Path.Combine(_resolvePath, assemblyName.Name + ".dll");
            if (File.Exists(file)) {
                return Assembly.LoadFile(file);
            }
            return null;
        }

        static void DoInit(Assembly asm)
        {
            var type = asm.GetTypes().SingleOrDefault(n => n.BaseType == typeof(ScriptInitializer));
            if (type != null) {
                ((ScriptInitializer)Activator.CreateInstance(type)).Do();
            }
        }

        static void Main(string[] args)
        {
            RuntimeInitializer.Initialize(_encoding);
            if (args.Length > 0) {
                var asm = Assembly.LoadFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, args[0]));
                DoInit(asm);
                DoMain(asm);
            }
        }

        private static void DoMain(Assembly asm)
        {
            var type = asm.GetTypes().SingleOrDefault(n => n.Name == "Program");
            if (type != null) {
                _resolvePath = type
                    .GetFields(BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                    .SingleOrDefault(fi => fi.IsLiteral && !fi.IsInitOnly && fi.Name == "ResolvePath")
                    ?.GetRawConstantValue()
                    .ToString();

                var main = type
                    .GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static)
                    .SingleOrDefault(n => n.Name == "<Main>");
                if (main != null) {
                    try {
                        main.Invoke(null, new object[] { });
                    }
                    catch (TargetInvocationException e) {
                        throw e.InnerException ?? e;
                    }
                }
            }
        }
    }
}
