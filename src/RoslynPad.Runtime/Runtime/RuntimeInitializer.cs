using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using RoslynPad.Utilities;

namespace RoslynPad.Runtime
{
    public abstract class ScriptInitializer
    {
        public static string GlobalPackageFolder { get; set; }

        public abstract void Do();

        public static void CopyDir(string source, string dest)
        {
            Directory.CreateDirectory(dest);
            foreach (var path in Directory.GetDirectories(source, "*",
                SearchOption.AllDirectories))
                Directory.CreateDirectory(path.Replace(source, dest));

            //Copy all the files & Replaces any files with the same name
            foreach (var fileName in Directory.GetFiles(source, "*.*",
                SearchOption.AllDirectories))
                File.Copy(fileName, fileName.Replace(source, dest), true);
        }
    }

    /// <summary>
    /// This class initializes the RoslynPad standalone host.
    /// </summary>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public static class RuntimeInitializer
    {
        private static bool _initialized;
        private static Encoding _encoding;

        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static void Initialize(Encoding encoding = null)
        {
            if (_initialized) return;
            _initialized = true;
            _encoding = encoding ?? Encoding.UTF8;
            ParseCommandLine("nugetRoot", "[^\"]+", out var path);
            ScriptInitializer.GlobalPackageFolder = path;

            var isAttachedToParent = TryAttachToParentProcess();
            DisableWer();
            AttachConsole(isAttachedToParent);
        }

        private static void AttachConsole(bool isAttachedToParent)
        {
            Console.OutputEncoding = _encoding;
            Console.InputEncoding = _encoding;

            var consoleDumper = isAttachedToParent ? (IConsoleDumper)new JsonConsoleDumper(_encoding) : new DirectConsoleDumper();

            if (consoleDumper.SupportsRedirect) {
                Console.SetOut(consoleDumper.CreateWriter());
                Console.SetError(consoleDumper.CreateWriter("Error"));
                Console.SetIn(consoleDumper.CreateReader());

                AppDomain.CurrentDomain.UnhandledException += (o, e) => {
                    consoleDumper.DumpException((Exception)e.ExceptionObject);
                    Environment.Exit(1);
                };
            }

            ObjectExtensions.Dumped += data => consoleDumper.Dump(data);
            AppDomain.CurrentDomain.ProcessExit += (o, e) => consoleDumper.Flush();
        }

        private static bool TryAttachToParentProcess()
        {
            if (ParseCommandLine("pid", @"\d+", out var parentProcessId)) {
                AttachToParentProcess(int.Parse(parentProcessId));
                return true;
            }
            return false;
        }

        internal static void AttachToParentProcess(int parentProcessId)
        {
            Process clientProcess;
            try {
                clientProcess = Process.GetProcessById(parentProcessId);
            }
            catch (ArgumentException) {
                Environment.Exit(1);
                return;
            }

            clientProcess.EnableRaisingEvents = true;
            clientProcess.Exited += (o, e) => {
                Environment.Exit(1);
            };

            if (!clientProcess.IsAlive()) {
                Environment.Exit(1);
            }
        }

        /// <summary>
        /// Disables Windows Error Reporting for the process, so that the process fails fast.
        /// </summary>
        internal static void DisableWer()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                WindowsNativeMethods.DisableWer();
            }
        }

        private static bool ParseCommandLine(string name, string pattern, out string value)
        {
            var match = Regex.Match(Environment.CommandLine, $@"--{name}\s+""?({pattern})""?");
            value = match.Success ? match.Groups[1].Value : string.Empty;
            return match.Success;
        }
    }
}
