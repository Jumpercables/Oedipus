using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

using Fclp;

namespace Oedipus
{
    /// <summary>
    ///     The application arguments.
    /// </summary>
    internal class ApplicationArguments
    {
        #region Public Properties

        /// <summary>
        ///     Gets or sets the assembly files.
        /// </summary>
        /// <value>
        ///     The assembly files.
        /// </value>
        public List<string> AssemblyFiles { get; set; }

        /// <summary>
        ///     Gets or sets the description.
        /// </summary>
        /// <value>
        ///     The description.
        /// </value>
        public string Description { get; set; }

        /// <summary>
        ///     Gets or sets the output directory.
        /// </summary>
        /// <value>
        ///     The output directory.
        /// </value>
        public string OutputDirectory { get; set; }

        #endregion
    }

    internal class Program
    {
        #region Private Methods

        /// <summary>
        ///     Create the reStructured text file for the assembly.
        /// </summary>
        /// <param name="assembly">The assembly.</param>
        /// <param name="directory">The output directory.</param>
        /// <returns>
        ///     Returns a <see cref="IEnumerable{String}" /> representing the name of the assembly rst file.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">
        ///     assembly
        ///     or
        ///     outputDirectory
        /// </exception>
        private IEnumerable<string> CreateAssemblyRst(Assembly assembly, string directory)
        {
            if (assembly == null) throw new ArgumentNullException("assembly");
            if (directory == null) throw new ArgumentNullException("directory");           

            // Create a sub-directory for each assembly file.
            string assemblyName = Path.GetFileNameWithoutExtension(assembly.Location);
            string toctree = assemblyName.ToLower();
            string root = Path.Combine(directory, toctree);

            // Create the assembly folder.
            Directory.CreateDirectory(root);

            // Create a table of contents file that references the namespace files.
            string toc = Path.Combine(directory, string.Format("{0}.rst", toctree));

            Log.Debug(this, "\tCreating {0}.rst", toctree);

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
                var namespaces = assembly.GetTypes().GroupBy(o => o.Namespace);
                foreach (var rst in this.CreateNamespaceRst(namespaces, assemblyName, root).OrderBy(o => o))
                {
                    sw.WriteLine("\t {0}/{1}", toctree, rst);
                }
            }

            yield return toctree;
        }

        /// <summary>
        ///     Create the reStructured text file for the namespaces.
        /// </summary>
        /// <param name="namespaces">The namespaces.</param>
        /// <param name="assemblyName">Name of the assembly.</param>
        /// <param name="directory">The output directory.</param>
        /// <returns>
        ///     Returns a <see cref="IEnumerable{String}" /> representing the rst files for the namespaces.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">
        ///     namespaces
        ///     or
        ///     assemblyName
        ///     or
        ///     outputDirectory
        /// </exception>
        private IEnumerable<string> CreateNamespaceRst(IEnumerable<IGrouping<string, Type>> namespaces, string assemblyName, string directory)
        {
            if (namespaces == null) throw new ArgumentNullException("namespaces");
            if (assemblyName == null) throw new ArgumentNullException("assemblyName");
            if (directory == null) throw new ArgumentNullException("directory");

            // Iterate through all of the namespaces that are not null.            
            foreach (var name in namespaces.Where(o => o.Key != null))
            {
                // NOTE: The doxygenclass does not support interfaces and generic definitions.
                var list = name.Where(o => o.IsPublic && o.IsClass && !o.IsGenericTypeDefinition).OrderBy(o => o.Name).ToList();
                if (list.Count == 0) continue;

                // Create a RST file for each namespace.
                Log.Debug(this, "\t\tCreating {0}.rst", name.Key.ToLower());

                string path = Path.Combine(directory, string.Format("{0}.rst", name.Key.ToLower()));
                using (StreamWriter sw = new StreamWriter(path, false))
                {
                    sw.WriteLine(name.Key);
                    sw.WriteLine(new string('=', name.Key.Length + 1));
                    sw.WriteLine();

                    // Write the doxygenclass tags for each public class or interface.
                    string x = null;
                    foreach (var type in list)
                    {
                        sw.Write(x);
                        sw.WriteLine(".. doxygenclass:: {0}", type.FullName.Replace(".", "::"));
                        sw.WriteLine("\t :project: {0}", assemblyName);
                        sw.WriteLine(string.Format("\t :members:"));

                        x = Environment.NewLine;
                    }
                }

                yield return name.Key.ToLower();
            }
        }

        /// <summary>
        ///     The entry point for the executable.
        /// </summary>
        /// <param name="args">The arguments.</param>
        private static void Main(string[] args)
        {
            try
            {
                new Program().Run(args);
            }
            catch (AggregateException ex)
            {
                foreach (var e in ex.InnerExceptions)
                {
                    Console.WriteLine("ERROR: " + e.Message);
                }
            }
        }

        /// <summary>
        ///     Runs program as a task
        /// </summary>
        /// <param name="args">The arguments.</param>
        private void Run(string[] args)
        {
            var parser = new FluentCommandLineParser<ApplicationArguments>();
            parser.Setup(arg => arg.AssemblyFiles).As('f', "files").Required();
            parser.Setup(arg => arg.OutputDirectory).As('o', "output").SetDefault(AppDomain.CurrentDomain.BaseDirectory + "\\apidocs");
            parser.Setup(arg => arg.Description).As('d', "description").SetDefault("The API documentation has been generated using `Oedipus <https://github.com/Jumpercables/Oedipus>`_ and built using the `Breathe <http://breathe.readthedocs.org/en/latest/>`_ extension for `Sphinx <http://sphinx-doc.org/index.html>`_.");

            var results = parser.Parse(args);
            if (results.HasErrors)
            {
                Console.WriteLine(results.ErrorText);
            }
            else
            {
                // Drop the output directory.
                string path = Path.GetFullPath(parser.Object.OutputDirectory);
                if (Directory.Exists(path))
                    Directory.Delete(path, true);

                // Create the directory.
                Directory.CreateDirectory(path);

                // Attempt to resolve the assembly references.
                AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += (sender, a) => Assembly.ReflectionOnlyLoad(a.Name);

                // Create the 'apidocs' subdirectory.                
                string apidocs = Path.Combine(path, "apidocs");
                DirectoryInfo di = Directory.CreateDirectory(apidocs);

                // Create the 'apidocs.rst' for the assemblies.
                using (StreamWriter sw = new StreamWriter(Path.Combine(path, "apidocs.rst"), false))
                {
                    sw.WriteLine("API");
                    sw.WriteLine(new string('=', 4));
                    sw.WriteLine(parser.Object.Description);
                    sw.WriteLine();
                    sw.WriteLine(".. toctree::");
                    sw.WriteLine("\t :maxdepth: 2");
                    sw.WriteLine();

                    // Iterate through all of the assembly files.
                    foreach (var assemblyFile in parser.Object.AssemblyFiles.Select(Path.GetFullPath))
                    {                        
                        // Create a sub-directory for each assembly file.
                        string assemblyName = Path.GetFileNameWithoutExtension(assemblyFile);
                        if (assemblyName == null) continue;                        

                        Log.Info(this, "Creating {0} files.", assemblyName);

                        // Load the assembly and create the rst file.
                        Assembly assembly = Assembly.ReflectionOnlyLoadFrom(assemblyFile);
                        foreach (var rst in this.CreateAssemblyRst(assembly, apidocs))
                        {
                            sw.WriteLine("\t {0}/{1}", di.Name, rst);
                        }
                    }
                }
            }
        }

        #endregion
    }
}