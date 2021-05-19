using System.IO;
using System.Reflection;

namespace StringSearch.Test
{
    public class TestBase
    {
        public static readonly string ContentFolderPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Content"));
    }
}
