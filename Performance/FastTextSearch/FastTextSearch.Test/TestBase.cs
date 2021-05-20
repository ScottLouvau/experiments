// Copyright (c) Scott Louvau. All rights reserved.
// Licensed under the MIT License.

using System.IO;
using System.Reflection;

namespace FastTextSearch.Test
{
    public class TestBase
    {
        public static readonly string ContentFolderPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Content"));
    }
}
