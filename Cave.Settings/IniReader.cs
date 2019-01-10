using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
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
        /// <summary>Parses initialization data</summary>
        /// <param name="name">The (file)name.</param>
        /// <param name="data">Content to parse.</param>
        /// <param name="properties">The data properties.</param>
        /// <returns></returns>
        public static IniReader Parse(string name, string data, IniProperties properties = default(IniProperties))
        {
            return new IniReader(name, data.SplitNewLine(), properties);
        }

        /// <summary>Parses initialization data</summary>
        /// <param name="name">The name.</param>
        /// <param name="data">Content to parse.</param>
        /// <param name="properties">The data properties.</param>
        /// <returns></returns>
        public static IniReader Parse(string name, byte[] data, IniProperties properties = default(IniProperties))
        {
            return Parse(name, Encoding.UTF8.GetString(data), properties);
        }

        /// <summary>Loads initialization data from strings</summary>
        /// <param name="name">The name.</param>
        /// <param name="lines">Content to parse.</param>
        /// <param name="properties">The content properties.</param>
        /// <returns></returns>
        public static IniReader Parse(string name, string[] lines, IniProperties properties = default(IniProperties))
        {
            return new IniReader(name, lines, properties);
        }


        /// <summary>Loads initialization data from file</summary>
        /// <param name="fileName">File name to read</param>
        /// <param name="properties">The content properties.</param>
        /// <returns></returns>
        public static IniReader FromFile(string fileName, IniProperties properties = default(IniProperties))
        {
            if (File.Exists(fileName))
            {
                return Parse(fileName, File.ReadAllBytes(fileName), properties);
            }
            return new IniReader(fileName, new string[0], properties);
        }

        /// <summary>Loads initialization data from stream</summary>
        /// <param name="name">The name.</param>
        /// <param name="stream">The stream to read</param>
        /// <param name="count">Number of bytes to read</param>
        /// <param name="properties">The content properties.</param>
        /// <returns></returns>
        public static IniReader FromStream(string name, Stream stream, int count, IniProperties properties = default(IniProperties))
        {
            byte[] data = stream.ReadBlock(count);
            return Parse(name, data, properties);
        }

        /// <summary>
        /// Obtains the configuration file for the current running process using the specified <see cref="FileLocation" />
        /// </summary>
        /// <param name="fileLocation">The file location.</param>
        /// <param name="properties">The content properties.</param>
        /// <returns></returns>
        public static IniReader FromLocation(FileLocation fileLocation, IniProperties properties = default(IniProperties))
        {
            if (fileLocation == null)
            {
                fileLocation = new FileLocation(root: RootLocation.RoamingUserConfig, extension: Ini.PlatformExtension);
            }

            string fileName = fileLocation.ToString();
            return FromFile(fileName, properties);
        }

        /// <summary>
        /// Obtains the configuration file for the current running process using the specified <see cref="RootLocation" />
        /// </summary>
        /// <param name="root">The root location.</param>
        /// <param name="properties">The content properties.</param>
        /// <returns></returns>
        public static IniReader FromLocation(RootLocation root, IniProperties properties = default(IniProperties))
        {
            FileLocation fileLocation = new FileLocation(root: root, extension: Ini.PlatformExtension);
            return FromLocation(fileLocation, properties);
        }
        #endregion

        /// <summary>
        /// Holds all lines of the configuration
        /// </summary>
        string[] m_Lines;

        /// <summary>
        /// Checks whether the config can be reloaded
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

                try { return File.Exists(Name); }
                catch { return false; }
            }
        }

        /// <summary>
        /// Obtains the index (linenumber) the specified section starts
        /// </summary>
        /// <param name="section">Section to search</param>
        /// <returns>Returns the index the section starts at</returns>
        int SectionStart(string section)
        {
            if (section == null)
            {
                return 0;
            }

            section = "[" + section + "]";

            int i = 0;
            while (i < m_Lines.Length)
            {
                string line = m_Lines[i].Trim();
                if (string.Compare(line, section, !Properties.CaseSensitive, Properties.Culture) == 0)
                {
                    return i;
                }

                i++;
            }
            return -1;
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
                StreamReader reader = new StreamReader(stream, Properties.Encoding);
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
        /// Provides access to the IniProperties 
        /// </summary>
        public IniProperties Properties { get; set; }

        /// <summary>
        /// Gets the culture used to decode values
        /// </summary>
        public override CultureInfo Culture => Properties.Culture;

        /// <summary>Loads initialization data</summary>
        /// <param name="name">The (file)name.</param>
        /// <param name="lines">The lines.</param>
        /// <param name="properties">Properties of the initialization data</param>
        IniReader(string name, string[] lines, IniProperties properties = default(IniProperties)) : base(name)
        {
            Properties = properties.Valid ? properties : IniProperties.Default;
            m_Lines = lines;
        }

        /// <summary>
        /// Reload the whole config
        /// </summary>
        public override void Reload()
        {
            if (!CanReload)
            {
                throw new InvalidOperationException("Cannot reload!");
            }

            m_Lines = Parse(File.ReadAllBytes(Name));
        }

        /// <summary>
        /// Obtains whether a specified section exists or not
        /// </summary>
        /// <param name="section">Section to search</param>
        /// <returns>Returns true if the sections exists false otherwise</returns>
        public override bool HasSection(string section)
        {
            return SectionStart(section) > -1;
        }

        /// <summary>
        /// Obtains all section names present at the file
        /// </summary>
        /// <returns>Returns an array of all section names</returns>
        public override string[] GetSectionNames()
        {
            List<string> result = new List<string>();
            foreach (string line in m_Lines)
            {
                string trimed = line.Trim();
                if (trimed.StartsWith("[") && trimed.EndsWith("]"))
                {
                    result.Add(trimed.Substring(1, trimed.Length - 2).Trim());
                }
            }
            return result.ToArray();
        }

        /// <summary>
        /// Reads a whole section from the ini
        /// </summary>
        /// <param name="section">Name of the section</param>
        /// <param name="remove">Remove comments and empty lines</param>
        /// <returns>Returns the whole section as string array</returns>
        public override string[] ReadSection(string section, bool remove)
        {
            //find section
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
                    //empty or not present
                    return new string[0];
                }
            }
            //got it, add lines to result
            List<string> result = new List<string>();
            for (; ++i < m_Lines.Length;)
            {
                string line = m_Lines[i];
                if (line.StartsWith("["))
                {
                    break;
                }

                if (remove)
                {
                    //remove comments and empty lines
                    int comment = line.IndexOfAny(new char[] { '#', ';' });
                    if (comment > -1)
                    {
                        //only remove if comment marker is the first character
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
        /// Reads a setting from the ini
        /// </summary>
        /// <param name="section">Sectionname of the setting</param>
        /// <param name="settingName">Name of the setting</param>
        /// <returns>Returns null if the setting is not present a string otherwise</returns>
        public override string ReadSetting(string section, string settingName)
        {
            //find section
            int i = SectionStart(section);
            if (i < 0)
            {
                return null;
            }
            //iterate all lines
            for (++i; i < m_Lines.Length; i++)
            {
                string line = m_Lines[i].Trim();
                if (line.StartsWith("[") && (line.EndsWith("]")))
                {
                    break;
                }
                //ignore comments
                if (line.StartsWith("#"))
                {
                    continue;
                }

                if (line.StartsWith(";"))
                {
                    continue;
                }
                //find equal sign
                int sign = line.IndexOf('=');
                if (sign > -1)
                {
                    //got a setting, check name
                    string name = line.Substring(0, sign).Trim();
                    if (string.Compare(settingName, name, !Properties.CaseSensitive, Properties.Culture) == 0)
                    {
                        string value = line.Substring(sign + 1).Trim();
                        if (value.Length < 1)
                        {
                            return "";
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
            //no setting with the specified name found
            return null;
        }

        /// <summary>
        /// Obtains a string array with the whole configuration
        /// </summary>
        /// <returns>Returns an array containing all strings (lines) of the configuration</returns>
        public string[] ToArray() { return (string[])m_Lines.Clone(); }

        /// <summary>
        /// Retrieves the whole data as string
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return StringExtensions.JoinNewLine(m_Lines);
        }
    }
}
