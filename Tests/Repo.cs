using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace CaravansCriticallyEnhanced.Tests
{
    /// <summary>
    /// Finds the repository and reads its files for the fixtures that check shipped text and data.
    /// </summary>
    internal static class Repo
    {
        /// <summary>
        /// The nine languages the mod ships, English first.
        /// </summary>
        public static readonly string[] Languages =
        {
            "English", "ChineseSimplified", "French", "German", "Japanese",
            "Polish", "PortugueseBrazilian", "Russian", "Spanish"
        };

        /// <summary>
        /// Gets the repository root, found by walking up from the test assembly to the solution file.
        /// </summary>
        public static string Root
        {
            get
            {
                var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
                while (directory != null
                       && !File.Exists(Path.Combine(directory.FullName, "rimworld-caravans-critically-enhanced.sln")))
                {
                    directory = directory.Parent;
                }

                Assert.That(directory, Is.Not.Null, "Could not find the repository root.");
                return directory.FullName;
            }
        }

        /// <summary>
        /// Gets the path of a file or folder under the repository root.
        /// </summary>
        /// <param name="parts">The path segments below the root.</param>
        /// <returns>The full path.</returns>
        public static string PathOf(params string[] parts)
        {
            return Path.Combine(new[] { Root }.Concat(parts).ToArray());
        }

        /// <summary>
        /// Gets every C# source file the mod ships, which is everything under <c>1.6/</c> bar build output.
        /// </summary>
        /// <returns>The paths.</returns>
        public static List<string> ModSourceFiles()
        {
            string separator = Path.DirectorySeparatorChar.ToString();
            return Directory.GetFiles(PathOf("1.6"), "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains(separator + "obj" + separator) && !f.Contains(separator + "Assemblies" + separator))
                .OrderBy(f => f, System.StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        /// Reads a C# file with its comments removed.
        /// </summary>
        /// <param name="file">The file.</param>
        /// <returns>The code alone.</returns>
        /// <remarks>
        /// Required rather than tidy. A check that reads source as text cannot tell a use from a
        /// description of one, and a comment naming a real key or method is silently counted as a use.
        /// String literals containing <c>//</c> would be cut short by this, which no check here relies on.
        /// </remarks>
        public static string CodeWithoutComments(string file)
        {
            string text = File.ReadAllText(file);
            text = Regex.Replace(text, @"/\*.*?\*/", "", RegexOptions.Singleline);
            return Regex.Replace(text, @"//.*$", "", RegexOptions.Multiline);
        }

        /// <summary>
        /// Gets the path of one language's Keyed file.
        /// </summary>
        /// <param name="language">The language folder name.</param>
        /// <returns>The path.</returns>
        public static string KeyedPath(string language)
        {
            return PathOf("1.6", "Languages", language, "Keyed", "CaravansCriticallyEnhanced_Keys.xml");
        }
    }
}
