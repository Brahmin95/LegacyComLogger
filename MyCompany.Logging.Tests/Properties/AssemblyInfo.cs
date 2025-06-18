using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Xunit;

// This assembly-level attribute is a directive to the xUnit test runner.
// It instructs it to disable all test parallelization for this entire project.
// This is the standard and correct way to handle tests that interact with
// shared, static state.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
[assembly: AssemblyTitle("MyCompany.Logging.Tests")]
[assembly: AssemblyDescription("")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyProduct("MyCompany.Logging.Tests")]
[assembly: AssemblyCopyright("Copyright ©  2025")]
[assembly: AssemblyTrademark("")][assembly: AssemblyCulture("")]

[assembly: ComVisible(false)]

[assembly: Guid("61f869ff-c378-42de-b411-0f1cdf2f2e04")]

// [assembly: AssemblyVersion("1.0.*")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]
