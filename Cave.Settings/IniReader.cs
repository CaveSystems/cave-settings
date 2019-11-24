using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Cave.Compression;

namespace Cave
{
    /// <summary>
    /// Provides a fast and simple initialization data reader class.
    /// </summary>
    [DebuggerDisplay("{Name}")]
    public class IniReader : SettingsReader
    {
        #region static constructors

        /// <summary>Parses initialization data.</summary>
        /// <param name="name">The (file)name.</param>
        /// <param name="data">Content to parse.</param>
        /// <param name="properties">The data properties.</param>
        /// <returns>Returns a new <see cref="IniReader"/> instance.</returns>
        public static IniReader Parse(string name, string data, IniProperties properties = default)
        {
            return new IniReader(name, data.SplitNewLine(), properties);
        }

        /// <summary>Parses initialization data.</summary>
        /// <param name="name">The name.</param>
        /// <param name="data">Content to parse.</param>
        /// <param name="properties">The data properties.</param>
        /// <returns>Returns a new <see cref="IniReader"/> instance.</returns>
        public static IniReader Parse(string name, byte[] data, IniProperties properties = default)
        {
            return Parse(name, Encoding.UTF8.GetString(data), properties);
        }

        /// <summary>Loads initialization data from strings.</summary>
        /// <param name="name">The name.</param>
        /// <param name="lines">Content to parse.</param>
        /// <param name="properties">The content properties.</param>
        /// <returns>Returns a new <see cref="IniReader"/> instance.</returns>
        public static IniReader Parse(string name, string[] lines, IniProperties properties = default)
        {
            return new IniReader(name, lines, properties);
        }

        /// <summary>Loads initialization data from file.</summary>
        /// <param name="fileName">File name to read.</param>
        /// <param name="properties">The content properties.</param>
        /// <returns>Returns a new <see cref="IniReader"/> instance.</returns>
        public static IniReader FromFile(string fileName, IniProperties properties = default)
        {
            if (File.Exists(fileName))
            {
                return Parse(fileName, File.ReadAllBytes(fileName), properties);
            }
            return new IniReader(fileName, new string[0], properties);
        }

        /// <summary>Loads initialization data from stream.</summary>
        /// <param name="name">The name.</param>
        /// <param name="stream">The stream to read.</param>
        /// <param name="count">Number of bytes to read.</param>
        /// <param name="properties">The content properties.</param>
        /// <returns>Returns a new <see cref="IniReader"/> instance.</returns>
        public static IniReader FromStream(string name, Stream stream, int count, IniProperties properties = default)
        {
            byte[] data = stream.ReadBlock(count);
            return Parse(name, data, properties);
        }

        /// <summary>
        /// Obtains the configuration file for the current running process using the specified <see cref="FileLocation" />.
        /// </summary>
        /// <param name="fileLocation">The file location.</param>
        /// <param name="properties">The content properties.</param>
        /// <returns>Returns a new <see cref="IniReader"/> instance.</returns>
        public static IniReader FromLocation(FileLocation fileLocation, IniProperties properties = default)
        {
            if (fileLocation == null)
            {
                fileLocation = new FileLocation(root: RootLocation.RoamingUserConfig, extension: Ini.PlatformExtension);
            }

            string fileName = fileLocation.ToString();
            return FromFile(fileName, properties);
        }

        /// <summary>
        /// Obtains the configuration file for the current running process using the specified <see cref="RootLocation" />.
        /// </summary>
        /// <param name="root">The root location.</param>
        /// <param name="properties">The content properties.</param>
        /// <returns>Returns a new <see cref="IniReader"/> instance.</returns>
        public static IniReader FromLocation(RootLocation root, IniProperties properties = default)
        {
            var fileLocation = new FileLocation(root: root, extension: Ini.PlatformExtension);
            return FromLocation(fileLocation, properties);
        }
        #endregion

        /// <summary>
        /// Holds all lines of the configuration.
        /// </summary>
        string[] lines;

        Dictionary<string, int> sections = null;

        Dictionary<string, int> GetSectionIndices()
        {
            if (sections == null)
            {
                sections = new Dictionary<string, int>(Properties.CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < lines.Length; i++)
                {
                    string trimed = lines[i].Trim();
                    if (trimed.StartsWith("[") && trimed.EndsWith("]"))
                    {
                        var name = trimed.Substring(1, trimed.Length - 2).Trim();
                        sections.TryAdd(name, i);
                    }
                }
            }
            return sections;
        }

        /// <summary>
        /// Gets a value indicating whether the config can be reloaded.
        /// </summary>
        public override bool CanReload
        {
            get
            {
                if (string.IsNullOrEmpty(Name))
                {
                    return false;
                }

                if (Name.IndexOfAny(Path.GetInvalidPathChars()) > -1)
                {
                    return false;
                }

                try
                {
                    return File.Exists(Name);
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Obtains the index (linenumber) the specified section starts.
        /// </summary>
        /// <param name="section">Section to search.</param>
        /// <returns>Returns the index the section starts at.</returns>
        int SectionStart(string section)
        {
            var sections = GetSectionIndices();
            if (section == null)
            {
                return 0;
            }

            if (!sections.TryGetValue(section, out int result))
            {
                result = -1;
            }
            return result;
        }

        string[] Parse(byte[] data)
        {
            if (data.Length == 0)
            {
                return new string[0];
            }

            if ((Properties.Encryption == null) && (Properties.Compression == CompressionType.None))
            {
                return Properties.Encoding.GetString(data).SplitNewLine();
            }
            Stream stream = new MemoryStream(data);
            try
            {
                if (Properties.Encryption != null)
                {
                    stream = new CryptoStream(stream, Properties.Encryption.CreateDecryptor(), CryptoStreamMode.Read);
                }
                switch (Properties.Compression)
                {
                    case CompressionType.Deflate:
                        stream = new DeflateStream(stream, CompressionMode.Decompress, true);
                        break;
                    case CompressionType.GZip:
                        stream = new GZipStream(stream, CompressionMode.Decompress, true);
                        break;
                    case CompressionType.None: break;
                    default: throw new InvalidDataException(string.Format("Unknown Compression {0}", Properties.Compression));
                }
                var reader = new StreamReader(stream, Properties.Encoding);
                string[] result = reader.ReadToEnd().SplitNewLine();
                reader.Close();
                return result;
            }
            catch
            {
                stream.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Gets or sets the properties.
        /// </summary>
        public IniProperties Properties { get; set; }

        /// <summary>
        /// Gets the culture used to decode values.
        /// </summary>
        public override CultureInfo Culture => Properties.Culture;

        /// <summary>
        /// Initializes a new instance of the <see cref="IniReader"/> class.
        /// </summary>
        /// <param name="name">The (file)name.</param>
        /// <param name="lines">The lines.</param>
        /// <param name="properties">Properties of the initialization data.</param>
        IniReader(string name, string[] lines, IniProperties properties = default)
            : base(name)
        {
            Properties = properties.Valid ? properties : IniProperties.Default;
            this.lines = lines;
        }

        /// <summary>
        /// Reload the whole config.
        /// </summary>
        public override void Reload()
        {
            if (!CanReload)
            {
                throw new InvalidOperationException("Cannot reload!");
            }

            lines = Parse(File.ReadAllBytes(Name));
        }

        /// <summary>
        /// Obtains whether a specified section exists or not.
        /// </summary>
        /// <param name="section">Section to search.</param>
        /// <returns>Returns true if the sections exists false otherwise.</returns>
        public override bool HasSection(string section)
        {
            return SectionStart(section) > -1;
        }

        /// <summary>
        /// Obtains all section names present at the file.
        /// </summary>
        /// <returns>Returns an array of all section names.</returns>
        public override string[] GetSectionNames()
        {
            var sections = GetSectionIndices();
            return sections.Keys.ToArray();
        }

        /// <summary>
        /// Reads a whole section from the ini.
        /// </summary>
        /// <param name="section">Name of the section.</param>
        /// <param name="remove">Remove comments and empty lines.</param>
        /// <returns>Returns the whole section as string array.</returns>
        public override string[] ReadSection(string section, bool remove)
        {
            // find section
            int i;
            if (section == null)
            {
                i = -1;
            }
            else
            {
                i = SectionStart(section);
                if (i < 0)
                {
                    // empty or not present
                    return new string[0];
                }
            }

            // got it, add lines to result
            var result = new List<string>();
            for (; ++i < lines.Length;)
            {
                string line = lines[i];
                if (line.StartsWith("["))
                {
                    break;
                }

                if (remove)
                {
                    // remove comments and empty lines
                    int comment = line.IndexOfAny(new char[] { '#', ';' });
                    if (comment > -1)
                    {
                        // only remove if comment marker is the first character
                        string whiteSpace = line.Substring(0, comment);
                        if (string.IsNullOrEmpty(whiteSpace) || (whiteSpace.Trim().Length == 0))
                        {
                            continue;
                        }
                    }
                    if (line.Trim().Length == 0)
                    {
                        continue;
                    }
                }
                result.Add(line);
            }
            return result.ToArray();
        }

        /// <summary>
        /// Reads a setting from the ini.
        /// </summary>
        /// <param name="section">Sectionname of the setting.</param>
        /// <param name="settingName">Name of the setting.</param>
        /// <returns>Returns null if the setting is not present a string otherwise.</returns>
        public override string ReadSetting(string section, string settingName)
        {
            // find section
            int i = SectionStart(section);
            if (i < 0)
            {
                return null;
            }

            // iterate all lines
            for (++i; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                int last = line.Length - 1;

                if (last < 0)
                {
                    continue;
                }

                if ((line[0] == '[') && (line[last] == ']'))
                {
                    break;
                }

                // ignore comments
                switch (line[0])
                {
                    case '#': continue;
                    case ';': continue;
                }

                // find equal sign
                int sign = line.IndexOf('=');
                if (sign > -1)
                {
                    // got a setting, check name
                    string name = line.Substring(0, sign).Trim();
                    if (string.Compare(settingName, name, !Properties.CaseSensitive, Properties.Culture) == 0)
                    {
                        string value = line.Substring(sign + 1).Trim();
                        if (value.Length < 1)
                        {
                            return string.Empty;
                        }

                        if (value[0] == '"' || value[0] == '\'')
                        {
                            return value.UnboxText(false);
                        }
                        int comment = value.IndexOf('#');
                        if (comment > -1)
                        {
                            value = value.Substring(0, comment).Trim();
                        }

                        return value;
                    }
                }
            }

            // no setting with the specified name found
            return null;
        }

        /// <summary>
        /// Obtains a string array with the whole configuration.
        /// </summary>
        /// <returns>Returns an array containing all strings (lines) of the configuration.</returns>
        public string[] ToArray()
        {
            return (string[])lines.Clone();
        }

        /// <summary>
        /// Retrieves the whole data as string.
        /// </summary>
        /// <returns>Returns a new string.</returns>
        public override string ToString()
        {
            return StringExtensions.JoinNewLine(lines);
        }
    }
}
