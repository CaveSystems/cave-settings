using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Cave.Compression;

namespace Cave
{
    /// <summary>
    /// Provides a fast and simple ini writer class.
    /// </summary>
    [DebuggerDisplay("{Name}")]
    public class IniWriter
    {
        #region static constructors

        /// <summary>Creates an new initialization writer by parsing the specified data.</summary>
        /// <param name="name">The (file)name.</param>
        /// <param name="data">Content to parse.</param>
        /// <param name="properties">The data properties.</param>
        /// <returns>Returns a new <see cref="IniWriter"/> instance.</returns>
        public static IniWriter Parse(string name, string data, IniProperties properties = default)
        {
            return new IniWriter(IniReader.Parse(name, data, properties));
        }

        /// <summary>Creates an new initialization writer by parsing the specified data.</summary>
        /// <param name="name">The name.</param>
        /// <param name="data">Content to parse.</param>
        /// <param name="properties">The data properties.</param>
        /// <returns>Returns a new <see cref="IniWriter"/> instance.</returns>
        public static IniWriter Parse(string name, byte[] data, IniProperties properties = default)
        {
            return Parse(name, Encoding.UTF8.GetString(data), properties);
        }

        /// <summary>Creates an new initialization writer by parsing the specified data.</summary>
        /// <param name="name">The name.</param>
        /// <param name="lines">Content to parse.</param>
        /// <param name="properties">The content properties.</param>
        /// <returns>Returns a new <see cref="IniWriter"/> instance.</returns>
        public static IniWriter Parse(string name, string[] lines, IniProperties properties = default)
        {
            return new IniWriter(IniReader.Parse(name, lines, properties));
        }

        /// <summary>Creates an new initialization writer with the specified preexisting content.</summary>
        /// <param name="fileName">File name to read.</param>
        /// <param name="properties">The content properties.</param>
        /// <returns>Returns a new <see cref="IniWriter"/> instance.</returns>
        public static IniWriter FromFile(string fileName, IniProperties properties = default)
        {
            return File.Exists(fileName) ? Parse(fileName, File.ReadAllBytes(fileName), properties) : new IniWriter(fileName, properties);
        }

        /// <summary>Creates an new initialization writer with the specified preexisting content.</summary>
        /// <param name="name">The name.</param>
        /// <param name="stream">The stream to read.</param>
        /// <param name="count">Number of bytes to read.</param>
        /// <param name="properties">The content properties.</param>
        /// <returns>Returns a new <see cref="IniWriter"/> instance.</returns>
        public static IniWriter FromStream(string name, Stream stream, int count, IniProperties properties = default)
        {
            byte[] data = stream.ReadBlock(count);
            return Parse(name, data, properties);
        }

        /// <summary>Obtains the configuration file writer using the specified <see cref="FileLocation" />.</summary>
        /// <param name="fileLocation">The file location.</param>
        /// <param name="properties">The content properties.</param>
        /// <returns>Returns a new <see cref="IniWriter"/> instance.</returns>
        public static IniWriter FromLocation(FileLocation fileLocation, IniProperties properties = default)
        {
            if (fileLocation == null)
            {
                fileLocation = new FileLocation(root: RootLocation.RoamingUserConfig, extension: Ini.PlatformExtension);
            }

            string fileName = fileLocation.ToString();
            return FromFile(fileName, properties);
        }

        /// <summary>Obtains the configuration file writer using the specified <see cref="RootLocation" />.</summary>
        /// <param name="root">The root location.</param>
        /// <param name="properties">The content properties.</param>
        /// <returns>Returns a new <see cref="IniWriter"/> instance.</returns>
        public static IniWriter FromLocation(RootLocation root, IniProperties properties = default)
        {
            var fileLocation = new FileLocation(root: root, extension: Ini.PlatformExtension);
            return FromLocation(fileLocation, properties);
        }

        #endregion

        readonly Dictionary<string, List<string>> data = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets or sets the IniProperties.
        /// </summary>
        public IniProperties Properties { get; set; }

        /// <summary>
        /// Gets or sets name of the ini writer.
        /// </summary>
        public string Name { get; set; }

        /// <summary>Initializes a new instance of the <see cref="IniWriter"/> class.</summary>
        public IniWriter()
        {
            Properties = IniProperties.Default;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IniWriter"/> class.
        /// </summary>
        /// <param name="fileName">Name of the file to write to.</param>
        /// <param name="properties">Encoding properties.</param>
        public IniWriter(string fileName, IniProperties properties)
        {
            Properties = properties.Valid ? properties : IniProperties.Default;
            Name = fileName;
            if (File.Exists(fileName))
            {
                Load(IniReader.FromFile(fileName));
            }
            else
            {
                File.Open(fileName, FileMode.OpenOrCreate).Close();
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IniWriter"/> class.
        /// </summary>
        /// <param name="reader">Settings to initialize the writer from.</param>
        public IniWriter(ISettings reader)
        {
            if (reader == null)
            {
                throw new ArgumentNullException("reader");
            }

            Name = reader.Name;
            if (reader is IniReader)
            {
                Properties = ((IniReader)reader).Properties;
                if (!Properties.Valid)
                {
                    Properties = IniProperties.Default;
                }
            }
            else
            {
                Properties = IniProperties.Default;
            }
            Load(reader);
        }

        /// <summary>
        /// Loads all settings from the specified reader and replaces all present sections.
        /// </summary>
        /// <param name="reader">The reader to obtain the config from.</param>
        public void Load(ISettings reader)
        {
            if (reader == null)
            {
                throw new ArgumentNullException("reader");
            }

            foreach (string section in reader.GetSectionNames())
            {
                data[section] = new List<string>(reader.ReadSection(section, false));
            }
        }

        /// <summary>
        /// Removes a whole section from the ini file.
        /// </summary>
        /// <param name="section">Name of the section.</param>
        public void RemoveSection(string section)
        {
            if (data.ContainsKey(section))
            {
                if (!data.Remove(section))
                {
                    throw new KeyNotFoundException();
                }
            }
        }

        /// <summary>
        /// Writes (replaces) a whole section at the ini.
        /// </summary>
        /// <param name="section">Name of the section.</param>
        /// <param name="value">The value.</param>
        public void WriteSection(string section, string value)
        {
            WriteSection(section, value.SplitNewLine());
        }

        /// <summary>
        /// Writes (replaces) a whole section at the ini.
        /// </summary>
        /// <param name="section">Name of the section.</param>
        /// <param name="values">The values.</param>
        public void WriteSection(string section, IEnumerable values)
        {
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            var strings = new List<string>();
            foreach (object value in values)
            {
                strings.Add(value.ToString());
            }
            WriteSection(section, strings);
        }

        /// <summary>
        /// Writes (replaces) a whole section at the ini.
        /// </summary>
        /// <param name="section">Name of the section.</param>
        /// <param name="lines">The lines.</param>
        public void WriteSection(string section, IEnumerable<string> lines)
        {
            if (section == null)
            {
                throw new ArgumentNullException(nameof(section));
            }

            if (lines == null)
            {
                throw new ArgumentNullException(nameof(lines));
            }

            var result = new List<string>();
            result.AddRange(lines);
            data[section] = result;
        }

        /// <summary>
        /// Writes all fields of the struct to the specified section (replacing a present one).
        /// </summary>
        /// <typeparam name="T">The struct type.</typeparam>
        /// <param name="section">The section to write to.</param>
        /// <param name="item">The struct.</param>
        [Obsolete("Use WriteFields instead!")]
        public void WriteStruct<T>(string section, T item)
            where T : struct
            => WriteFields<T>(section, item);

        /// <summary>
        /// Writes all fields of the object to the specified section (replacing a present one).
        /// </summary>
        /// <typeparam name="T">The class type.</typeparam>
        /// <param name="section">The section to write to.</param>
        /// <param name="obj">The object.</param>
        [Obsolete("Use WriteFields instead!")]
        public void WriteObject<T>(string section, T obj)
            where T : class
            => WriteFields<T>(section, obj);

        /// <summary>
        /// Writes all fields of the object to the specified section (replacing a present one).
        /// </summary>
        /// <typeparam name="T">The class type.</typeparam>
        /// <param name="section">The section to write to.</param>
        /// <param name="obj">The object.</param>
        public void WriteFields<T>(string section, T obj)
        {
            if (obj == null)
            {
                throw new ArgumentNullException(nameof(obj));
            }

            foreach (var field in obj.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                var value = field.GetValue(obj);
                WriteSetting(section, field.Name, value);
            }
        }

        /// <summary>
        /// Writes all properties of the object to the specified section (replacing a present one).
        /// </summary>
        /// <typeparam name="T">The class type.</typeparam>
        /// <param name="section">The section to write to.</param>
        /// <param name="obj">The object.</param>
        public void WriteProperties<T>(string section, T obj)
        {
            if (obj == null)
            {
                throw new ArgumentNullException(nameof(obj));
            }

            foreach (var field in obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var value = field.GetValue(obj, null);
                WriteSetting(section, field.Name, value);
            }
        }

        /// <summary>
        /// Writes a setting to the ini file (replacing a present one).
        /// </summary>
        /// <param name="section">Name of the section.</param>
        /// <param name="name">Name of the setting.</param>
        /// <param name="value">Value of the setting.</param>
        public void WriteSetting(string section, string name, object value)
        {
            WriteSetting(section, name, value is null ? null : StringExtensions.ToString(value, Properties.Culture));
        }

        /// <summary>
        /// Writes a setting to the ini tile (replacing a present one).
        /// </summary>
        /// <param name="section">Name of the section.</param>
        /// <param name="name">Name of the setting.</param>
        /// <param name="value">Value of the setting.</param>
        public void WriteSetting(string section, string name, string value)
        {
            if (section == null)
            {
                throw new ArgumentNullException(nameof(section));
            }

            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (value == null)
            {
                RemoveSetting(section, name);
                return;
            }

            if (value.Any(c => c < 32))
            {
                throw new ArgumentException($"Setting {name}: Value may not contain any ascii control code. Use .Escape() or .EscapeUtf8() to save this value!", nameof(value));
            }

            if (value.IndexOf('#') > -1 || value.Trim() != value || value.UnboxText(false) != value)
            {
                value = value.Box('"');
            }

            if (name.IndexOf('=') > -1)
            {
                throw new ArgumentException($"Setting {name}: Name may not contain an equal sign!", nameof(name));
            }

            if (!data.TryGetValue(section, out List<string> result))
            {
                data[section] = result = new List<string>();
            }

            // try to replace first
            for (int i = 0; i < result.Count; i++)
            {
                string setting = result[i].BeforeFirst('=').Trim();
                if (string.Equals(setting, name.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    result[i] = name + "=" + value;
                    return;
                }
            }

            // add new one
            result.Add(name + "=" + value);
        }

        /// <summary>
        /// Removes a setting from the ini file.
        /// </summary>
        /// <param name="section">Name of the section.</param>
        /// <param name="name">Name of the setting.</param>
        /// <returns>Returns true if the setting was removed, false if it was not present.</returns>
        public bool RemoveSetting(string section, string name)
        {
            if (section == null)
            {
                throw new ArgumentNullException(nameof(section));
            }

            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (!data.TryGetValue(section, out List<string> items))
            {
                return false;
            }

            for (int i = 0; i < items.Count; i++)
            {
                string setting = items[i].BeforeFirst('=').Trim();
                if (string.Equals(setting, name.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    items.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Saves the content of the ini to a file readable by <see cref="IniReader"/>.
        /// </summary>
        /// <param name="fileName">The fileName to write to.</param>
        public void Save(string fileName = null)
        {
            if (fileName == null)
            {
                fileName = Name;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(fileName));
            Stream stream = File.Open(fileName, FileMode.Create, FileAccess.Write, FileShare.None);
            try
            {
                if (Properties.Encryption != null)
                {
                    stream = new CryptoStream(stream, Properties.Encryption.CreateEncryptor(), CryptoStreamMode.Write);
                }
                switch (Properties.Compression)
                {
                    case CompressionType.Deflate:
                        stream = new DeflateStream(stream, CompressionMode.Compress, true);
                        break;
                    case CompressionType.GZip:
                        stream = new GZipStream(stream, CompressionMode.Compress, true);
                        break;
                    case CompressionType.None: break;
                    default: throw new InvalidDataException(string.Format("Unknown Compression {0}", Properties.Compression));
                }

                var writer = new StreamWriter(stream, Properties.Encoding);
                foreach (string section in data.Keys)
                {
                    writer.WriteLine("[" + section + "]");
                    bool allowOneEmpty = false;
                    foreach (string setting in data[section])
                    {
                        if (string.IsNullOrEmpty(setting) || (setting.Trim().Length == 0))
                        {
                            if (allowOneEmpty)
                            {
                                writer.WriteLine();
                                allowOneEmpty = false;
                            }
                            continue;
                        }
                        writer.WriteLine(setting);
                        allowOneEmpty = true;
                    }
                    writer.WriteLine();
                }
                writer.Close();
            }
            catch
            {
                stream.Dispose();
                throw;
            }
        }

        /// <summary>Converts all settings to a new reader.</summary>
        /// <returns>Returns a new <see cref="ISettings"/> instance containing all settings.</returns>
        public ISettings ToSettings()
        {
            return IniReader.Parse(Name, ToString(), Properties);
        }

        /// <summary>
        /// Retrieves the whole data as string.
        /// </summary>
        /// <returns>Returns the content of the settings in ini format.</returns>
        public override string ToString()
        {
            using (var writer = new StringWriter())
            {
                foreach (string section in data.Keys)
                {
                    writer.WriteLine("[" + section + "]");
                    bool allowOneEmpty = false;
                    foreach (string setting in data[section])
                    {
                        if (string.IsNullOrEmpty(setting) || (setting.Trim().Length == 0))
                        {
                            if (allowOneEmpty)
                            {
                                writer.WriteLine();
                                allowOneEmpty = false;
                            }
                            continue;
                        }
                        writer.WriteLine(setting);
                        allowOneEmpty = true;
                    }
                    writer.WriteLine();
                }
                return writer.ToString();
            }
        }
    }
}
