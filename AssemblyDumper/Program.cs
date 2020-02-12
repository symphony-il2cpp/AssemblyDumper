using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using CommandLine;
using Microsoft.Extensions.FileSystemGlobbing;

namespace AssemblyDumper
{
    internal static class Extensions
    {
        public static Regex[] ToRegexes(
            this IEnumerable<string> self)
        {
            return self.SelectMany((p) =>
            {
                try
                {
                    return new[] {new Regex(p)};
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex);
                    return new Regex[0];
                }
            }).ToArray();
        }
    }

    internal class Program
    {
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

            var blacklist = opts.Blacklist.ToRegexes();
            var whitelist = opts.Whitelist.ToRegexes();

            var assemblies = matches.SelectMany((m) =>
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
            foreach (var assembly in assemblies)
            {
                if (opts.Verbose)
                {
                    Console.WriteLine(
                        $"Loading classes for assembly {assembly}");
                }

                var assemblyClasses =
                    assembly.GetTypes().Where((t) => t.IsClass);

                if (blacklist.Length > 0)
                {
                    assemblyClasses = assemblyClasses.Where((c) =>
                        !blacklist.Any(r => r.IsMatch(c.FullName ?? c.Name)));
                }
                else if (whitelist.Length > 0)
                {
                    assemblyClasses = assemblyClasses.Where((c) =>
                        whitelist.Any(r => r.IsMatch(c.FullName ?? c.Name)));
                }

                classes.AddRange(assemblyClasses);
            }

            foreach (var klass in classes)
            {
                Console.WriteLine(klass);
            }
        }
    }
}
