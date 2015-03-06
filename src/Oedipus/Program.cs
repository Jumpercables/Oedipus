using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

using Fclp;

namespace Oedipus
{
    internal class ApplicationArguments
    {
        #region Public Properties

        public List<string> Files { get; set; }
        public string Output { get; set; }

        #endregion
    }

    internal class Program
    {
        #region Private Methods

        /// <summary>
        ///     Create the ReStructured text file for all of public classes and interfaces in the assembly.
        /// </summary>
        /// <param name="assembly"></param>
        /// <param name="root"></param>
        /// <returns></returns>
        private static IEnumerable<string> CreateReStructuredTextFile(Assembly assembly, string root)
        {
            var namespaces = assembly.GetTypes().GroupBy(o => o.Namespace);
            string assemblyName = Path.GetFileNameWithoutExtension(assembly.Location);

            // Iterate through all of the namespaces that are not null.            
            foreach (var @namespace in namespaces.Where(o => o.Key != null))
            {
                var list = @namespace.Where(o => o.IsPublic && o.IsClass && !o.IsGenericTypeDefinition).OrderBy(o => o.Name).ToList();
                if(list.Count == 0) continue;
                
                // Create a RST file for each namespace.
                string path = Path.Combine(root, string.Format("{0}.rst", @namespace.Key.ToLower()));
                using (StreamWriter sw = new StreamWriter(path, false))
                {
                    sw.WriteLine(@namespace.Key);
                    sw.WriteLine(new string('=', @namespace.Key.Length + 1));
                    sw.WriteLine();

                    // Write the doxygenclass tags for each public class or interface.
                    string x = null; 
                    foreach (var @class in list)
                    {
                        sw.Write(x);
                        sw.WriteLine(".. doxygenclass:: {0}", @class.FullName.Replace(".", "::"));
                        sw.WriteLine("\t :project: {0}", assemblyName);
                        sw.WriteLine(string.Format("\t :members:"));

                        x = Environment.NewLine;
                    }
                }

                yield return @namespace.Key.ToLower();
            }
        }

        /// <summary>
        /// The entry point for the executable.
        /// </summary>
        /// <param name="args">The arguments.</param>
        private static void Main(string[] args)
        {
            var parser = new FluentCommandLineParser<ApplicationArguments>();
            parser.Setup(arg => arg.Files).As('f', "files").Required();
            parser.Setup(arg => arg.Output).As('o', "output").SetDefault(AppDomain.CurrentDomain.BaseDirectory + "\\apidocs");

            var results = parser.Parse(args);
            if (results.HasErrors)
            {
                Console.WriteLine(results.ErrorText);
            }
            else
            {
                Console.WriteLine("Generating...");

                Run(parser.Object);

                Console.WriteLine("Done.");
            }
        }

        /// <summary>
        ///     Runs console application with the specified arguments.
        /// </summary>
        /// <param name="args">The arguments.</param>
        private static void Run(ApplicationArguments args)
        {
            // Drop the output directory.
            string path = Path.GetFullPath(args.Output);
            if (Directory.Exists(path))
                Directory.Delete(path, true);
    
            // Create the directory.
            Directory.CreateDirectory(path);

            // Attempt to resolve the assembly references.
            AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += (sender, a) => Assembly.ReflectionOnlyLoad(a.Name);

            // Iterate through all of the assembly files.
            foreach (var assemblyFile in args.Files.Where(File.Exists))
            {
                // Create a sub-directory for each assembly file.
                string assemblyName = Path.GetFileNameWithoutExtension(assemblyFile);
                if (assemblyName == null) return;                

                string toctree = assemblyName.ToLower();
                string root = Path.Combine(path, toctree);

                // Drop the assembly folder.
                if (Directory.Exists(root))
                    Directory.Delete(root, true);
                    
                // Create the assembly folder.
                Directory.CreateDirectory(root);

                // Create a table of contents file that references the namespace files.
                string toc = Path.Combine(args.Output, string.Format("{0}.rst", toctree));
                using (StreamWriter sw = new StreamWriter(toc))
                {
                    sw.WriteLine(assemblyName);
                    sw.WriteLine(new string('=', assemblyName.Length + 1));
                    sw.WriteLine("The API documentation for the contents of the {0} assembly.", assemblyName);
                    sw.WriteLine();
                    sw.WriteLine(".. toctree::");
                    sw.WriteLine("\t :maxdepth: 2");
                    sw.WriteLine();

                    // Load the assembly for reflection purposes only.
                    Assembly assembly = Assembly.ReflectionOnlyLoadFrom(assemblyFile);
                    foreach (var rst in CreateReStructuredTextFile(assembly, root).OrderBy(o => o))
                    {
                        sw.WriteLine("\t {0}/{1}", toctree, rst);
                    }
                }
            }
        }

        #endregion
    }
}