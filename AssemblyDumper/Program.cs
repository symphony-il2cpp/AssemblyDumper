using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AssemblyDumper.CSharp;
using CommandLine;
using Microsoft.Extensions.FileSystemGlobbing;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Enum = AssemblyDumper.CSharp.Enum;

namespace AssemblyDumper
{
    internal class Program
    {
        internal static string[] ValidTypes =
        {
            typeof(void).GetFullNameOrName(),

            typeof(byte).GetFullNameOrName(),
            typeof(sbyte).GetFullNameOrName(),
            typeof(ushort).GetFullNameOrName(),
            typeof(short).GetFullNameOrName(),
            typeof(uint).GetFullNameOrName(),
            typeof(int).GetFullNameOrName(),
            typeof(ulong).GetFullNameOrName(),
            typeof(long).GetFullNameOrName(),

            typeof(UIntPtr).GetFullNameOrName(),
            typeof(IntPtr).GetFullNameOrName(),

            typeof(float).GetFullNameOrName(),
            typeof(double).GetFullNameOrName(),

            typeof(bool).GetFullNameOrName(),

            typeof(string).GetFullNameOrName(),
            typeof(object).GetFullNameOrName(),

            typeof(Type).GetFullNameOrName(),
            typeof(Exception).GetFullNameOrName(),
            typeof(Delegate).GetFullNameOrName()
        };

        internal static string DefaultType = typeof(object).GetFullNameOrName();

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
                                r.IsMatch(t.GetFullNameOrName())))
                        .ToArray();
                }
                if (whitelist.Length > 0)
                {
                    assemblyTypes = assemblyTypes.Where(t =>
                            whitelist.Any(r =>
                                r.IsMatch(t.GetFullNameOrName())))
                        .ToArray();
                }

                var assemblyClasses = assemblyTypes.Where(t =>
                    t.IsClass && !t.IsCompilerGenerated());
                classes.AddRange(assemblyClasses);

                var assemblyEnums = assemblyTypes.Where(t =>
                    t.IsEnum && !t.IsCompilerGenerated());
                enums.AddRange(assemblyEnums);
            }

            var validTypes = new HashSet<string>(ValidTypes);

            var outputClasses = classes.Select(c =>
            {
                validTypes.Add(c.GetFullNameOrName());
                if (opts.Verbose)
                {
                    Console.WriteLine($"Dumping class {c}");
                }

                return new Class
                {
                    Name = c.Name,
                    Namespace = c.Namespace != null
                        ? c.Namespace.Split(".")
                        : new string[0],
                    Constructors = c.GetConstructors().Select(ctor =>
                        new Constructor
                        {
                            Parameters = ctor.GetParameters()
                                .Select(Parameter.SelectFromParameterInfo)
                                .ToArray()
                        }).ToArray(),
                    Fields = c.GetFields().Select(f => new Field
                    {
                        Name = f.Name,
                        Type = f.FieldType.GetFullNameOrName()
                    }).ToArray(),
                    Methods = c.GetMeaningfulMethods()
                        .Where(m =>
                            opts.InheritedMethods || m.DeclaringType == c)
                        .Select(m => new Method
                        {
                            Name = m.Name,
                            ReturnType = m.ReturnType.GetFullNameOrName(),
                            Parameters = m.GetParameters()
                                .Select(Parameter.SelectFromParameterInfo)
                                .ToArray(),
                            IsStatic = m.IsStatic
                        }).ToArray()
                };
            }).ToArray();
            var outputEnums = enums.Select(e =>
            {
                validTypes.Add(e.GetFullNameOrName());
                if (opts.Verbose)
                {
                    Console.WriteLine($"Dumping enum {e}");
                }

                return new Enum
                {
                    Name = e.Name,
                    Namespace = e.Namespace != null
                        ? e.Namespace.Split(".")
                        : new string[0],
                    BackingType = e.GetEnumUnderlyingType().GetFullNameOrName(),
                    Members = e.GetEnumNames()
                        .Zip(e.GetEnumValues().OfType<object>().Select(v =>
                            Convert.ChangeType(v, e.GetEnumUnderlyingType())))
                        .Select(t => new EnumMember
                        {
                            Name = t.First,
                            Value = t.Second
                        }).ToArray()
                };
            }).ToArray();

            foreach (var @class in outputClasses)
            {
                foreach (var field in @class.Fields)
                {
                    if (!validTypes.ContainsType(field.Type))
                    {
                        field.Type = DefaultType;
                    }
                }
                foreach (var method in @class.Methods)
                {
                    if (!validTypes.ContainsType(method.ReturnType))
                    {
                        method.ReturnType = DefaultType;
                    }
                    foreach (var parameter in method.Parameters)
                    {
                        if (!validTypes.ContainsType(parameter.Type))
                        {
                            parameter.Type = DefaultType;
                        }
                    }
                }
            }

            var output = new Output
            {
                Classes = outputClasses,
                Enums = outputEnums
            };
            using var writer = new JsonTextWriter(opts.Output != null
                ? File.CreateText(opts.Output)
                : Console.Out);
            var serializer = new JsonSerializer
            {
                Formatting =
                    opts.Pretty ? Formatting.Indented : Formatting.None,
                ContractResolver = new DefaultContractResolver
                {
                    NamingStrategy = new CamelCaseNamingStrategy()
                }
            };
            serializer.Serialize(writer, output);
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
                    "Classes to blacklist, evaluated as regular expressions.")]
            public IEnumerable<string> Blacklist { get; set; }

            [Option('w', "whitelist",
                HelpText =
                    "Classes to whitelist, evaluated as regular expressions.")]
            public IEnumerable<string> Whitelist { get; set; }

            [Option('o', "output",
                HelpText = "File to output to instead of standard output.")]
            public string Output { get; set; }

            [Option('p', "pretty", HelpText = "Indent the output JSON.")]
            public bool Pretty { get; set; }

            [Option('i', "inherited-methods",
                HelpText = "Dump inherited methods.")]
            public bool InheritedMethods { get; set; }
        }

        public class Output
        {
            public Class[] Classes;
            public Enum[] Enums;
        }
    }
}
