# Oedipus #
Oedipus is C# command line utility that will generate reStructured text (rst) files for .NET assemblies, that can be used for automatically generation API documentation using the Sphinx and Breathe projects.

## Documentation ##
The utility requires that the -f and -o parameters are specified. 

The -f parameter can be a list of assemblies or a single assembly file. (When the list of assmeblies are dependant on one another, it's best to list the assemblies in dependency order, starting with the lowest).
The -o parameter is the output directory for the rst files.

### Requirements ###
- 4.5 .NET Framework
- Visual Studio 2010+

### Third Party Libraries ###
- FluentCommandLineParser.1.4.1
