using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CommandLine;
using Microsoft.Extensions.FileSystemGlobbing;

namespace AssemblyDumper
{
    internal class Program
    {
        internal static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args).WithParsed(Run);
        }

        internal static void Run(Options opts)
        {
            var matcher = new Matcher();
            matcher.AddIncludePatterns(opts.Assemblies);
            var matches =
                matcher.GetResultsInFullPath(Directory.GetCurrentDirectory());

            var blacklist = opts.Blacklist.ToRegexes().ToArray();
            var whitelist = opts.Whitelist.ToRegexes().ToArray();

            var assemblies = matches.SelectMany(m =>
            {
                try
                {
                    if (opts.Verbose)
                    {
                        Console.WriteLine($"Loading assembly {m}");
                    }

                    return new[] {Assembly.LoadFile(m)};
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex);
                    return new Assembly[0];
                }
            });

            var classes = new List<Type>();
            var enums = new List<Type>();
            foreach (var assembly in assemblies)
            {
                if (opts.Verbose)
                {
                    Console.WriteLine(
                        $"Loading classes for assembly {assembly}");
                }

                var assemblyTypes = assembly.GetTypes();

                if (blacklist.Length > 0)
                {
                    assemblyTypes = assemblyTypes.Where(t =>
                            !blacklist.Any(r =>
                                r.IsMatch(t.FullName ?? t.Name)))
                        .ToArray();
                }
                else if (whitelist.Length > 0)
                {
                    assemblyTypes = assemblyTypes.Where(t =>
                            whitelist.Any(r => r.IsMatch(t.FullName ?? t.Name)))
                        .ToArray();
                }

                var assemblyClasses = assemblyTypes.Where(t =>
                    t.IsClass && !t.IsCompilerGenerated());
                classes.AddRange(assemblyClasses);

                var assemblyEnums = assemblyTypes.Where(t =>
                    t.IsEnum && !t.IsCompilerGenerated());
                enums.AddRange(assemblyEnums);
            }

            Console.WriteLine("========================================");
            Console.WriteLine("================ Classes ===============");
            Console.WriteLine("========================================");
            Console.WriteLine();
            foreach (var @class in classes)
            {
                Console.WriteLine(@class);

                Console.WriteLine("================ Fields ================");
                foreach (var field in @class.GetRuntimeFields())
                {
                    Console.WriteLine(field);
                }

                Console.WriteLine("================ Methods ===============");
                foreach (var method in @class.GetMeaningfulMethods()
                    .Where(m => m.DeclaringType == @class))
                {
                    Console.WriteLine(method);
                }

                Console.WriteLine();
            }

            Console.WriteLine("========================================");
            Console.WriteLine("================ Enums =================");
            Console.WriteLine("========================================");
            Console.WriteLine();
            foreach (var @enum in enums)
            {
                var type = @enum.GetEnumUnderlyingType();
                Console.WriteLine($"{@enum} : {type}");

                Console.WriteLine("================ Members ===============");
                var names = @enum.GetEnumNames();
                var values = @enum.GetEnumValues().OfType<object>()
                    .Select(v => Convert.ChangeType(v, type));
                foreach (var (name, value) in names.Zip(values))
                {
                    Console.WriteLine(
                        $"{name} = {value}");
                }

                Console.WriteLine();
            }
        }

        public class Options
        {
            [Value(0, Min = 1,
                HelpText = "Assemblies to dump, evaluated as glob patterns.")]
            public IEnumerable<string> Assemblies { get; set; }

            [Option('v', "verbose",
                HelpText = "Display verbose output.")]
            public bool Verbose { get; set; }

            [Option('b', "blacklist",
                HelpText =
                    "Classes to blacklist, evaluated as regular expressions.",
                SetName = "Blacklist")]
            public IEnumerable<string> Blacklist { get; set; }

            [Option('w', "whitelist",
                HelpText =
                    "Classes to whitelist, evaluated as regular expressions.",
                SetName = "Whitelist")]
            public IEnumerable<string> Whitelist { get; set; }

            [Option('o', "output",
                HelpText = "File to output to instead of standard output.")]
            public string Output { get; set; }
        }
    }
}
