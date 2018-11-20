#region CopyRight 2018
/*
    Copyright (c) 2003-2018 Andreas Rohleder (andreas@rohleder.cc)
    All rights reserved
*/
#endregion
#region License LGPL-3
/*
    This program/library/sourcecode is free software; you can redistribute it
    and/or modify it under the terms of the GNU Lesser General Public License
    version 3 as published by the Free Software Foundation subsequent called
    the License.

    You may not use this program/library/sourcecode except in compliance
    with the License. The License is included in the LICENSE file
    found at the installation directory or the distribution package.

    Permission is hereby granted, free of charge, to any person obtaining
    a copy of this software and associated documentation files (the
    "Software"), to deal in the Software without restriction, including
    without limitation the rights to use, copy, modify, merge, publish,
    distribute, sublicense, and/or sell copies of the Software, and to
    permit persons to whom the Software is furnished to do so, subject to
    the following conditions:

    The above copyright notice and this permission notice shall be included
    in all copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
    EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
    MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
    NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
    LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
    OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
    WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/
#endregion
#region Authors & Contributors
/*
   Author:
     Andreas Rohleder <andreas@rohleder.cc>

   Contributors:
 */
#endregion Authors & Contributors

using Cave.Compression;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;

namespace Cave.IO
{
    /// <summary>
    /// Provides access to an ini file
    /// </summary>
    /// <seealso cref="Cave.IO.SettingsReader" />
    public class Ini : SettingsReader
    {
        static Ini m_UserIniFile;
        static Ini m_LocalMachineIniFile;
        static Ini m_LocalUserIniFile;
        static Ini m_ProgramIniFile;

        /// <summary>Gets the default ini file.</summary>
        /// <value>The default ini file.</value>
        public static Ini Default
        {
            get
            {
                return UserIniFile;
            }
        }

        /// <summary>Gets the local user ini file.</summary>
        /// <value>The local user ini file.</value>
        public static Ini LocalUserIniFile
        {
            get
            {
                if (m_LocalUserIniFile == null)
                {
                    var location = new FileLocation(root: RootLocation.LocalUserConfig, extension: PlatformExtension);
                    m_LocalUserIniFile = new Ini(IniReader.FromLocation(location));
                }
                return m_LocalUserIniFile;
            }
        }

        /// <summary>Gets the local machine ini file.</summary>
        /// <value>The local machine ini file.</value>
        public static Ini LocalMachineIniFile
        {
            get
            {
                if (m_LocalMachineIniFile == null)
                {
                    var location = new FileLocation(root: RootLocation.AllUserConfig, extension: PlatformExtension);
                    m_LocalMachineIniFile = new Ini(IniReader.FromLocation(location));
                }
                return m_LocalMachineIniFile;
            }
        }

        /// <summary>Gets the user ini file.</summary>
        /// <value>The user ini file.</value>
        public static Ini UserIniFile
        {
            get
            {
                if (m_UserIniFile == null)
                {
                    var location = new FileLocation(root: RootLocation.RoamingUserConfig, extension: PlatformExtension);
                    m_UserIniFile = new Ini(IniReader.FromLocation(location));
                }
                return m_UserIniFile;
            }
        }

        /// <summary>Gets the program ini file.</summary>
        /// <value>The program ini file.</value>
        public static Ini ProgramIniFile
        {
            get
            {
                if (m_ProgramIniFile == null)
                {
                    var location = new FileLocation(root: RootLocation.Program, extension: PlatformExtension);
                    m_ProgramIniFile = new Ini(IniReader.FromLocation(location));
                }
                return m_ProgramIniFile;
            }
        }

        /// <summary>
        /// Obtains the platform specific extension of the configuration file
        /// </summary>
        public static string PlatformExtension
        {
            get
            {
                switch (Platform.Type)
                {
                    case PlatformType.CompactFramework:
                    case PlatformType.Windows:
                    case PlatformType.Xbox:
                        return ".ini";
                    case PlatformType.Linux:
                        return ".conf";
                    default:
                        return ".config";
                }
            }
        }

        /// <summary>Checks whether the config can be reloaded</summary>
        public override bool CanReload { get { return true; } }

        /// <summary>Gets the culture used to decode values</summary>
        public override CultureInfo Culture { get { return Properties.Culture; } }

        /// <summary>Gets the properties.</summary>
        /// <value>The properties.</value>
        public IniProperties Properties { get; set; }

		[DebuggerDisplay("{Name} = {Value}")]
		class Item
		{
			string name, val;
			public string Name { get => name; set => name = value.Trim(); }
			public string Value { get => val; set => val = value?.Trim(); }
		}
		[DebuggerDisplay("{Name} [{Items.Count}]")]
		class Section
		{
			string name;
			public string Name { get => name; set => name = value.Trim(); }
			public List<Item> Items = new List<Item>();
		}
		List<Section> m_Sections = new List<Section>();

		Section GetSection(string name)
		{
			return m_Sections.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.CurrentCultureIgnoreCase));
		}

        /// <summary>Initializes a new instance of the <see cref="Ini"/> class.</summary>
        /// <param name="reader">The reader.</param>
        Ini(IniReader reader) : base(reader.Name)
        {
            Properties = reader.Properties;
            Load(reader);
        }

        /// <summary>Initializes a new instance of the <see cref="Ini" /> class.</summary>
        /// <param name="fileName">Name of the file.</param>
        /// <param name="properties">The properties.</param>
        public Ini(string fileName, IniProperties properties = default(IniProperties)) : this(IniReader.FromFile(fileName, properties))
        {
        }

        /// <summary>Reload the whole config</summary>
        public override void Reload()
        {
            Load(IniReader.FromFile(Name));
        }

        /// <summary>Obtains all section names present at the file</summary>
        /// <returns>Returns an array of all section names</returns>
        public override string[] GetSectionNames()
        {
			return m_Sections.Select(s => s.Name).ToArray();
        }

        /// <summary>Obtains whether a specified section exists or not</summary>
        /// <param name="section">Section to search</param>
        /// <returns>Returns true if the sections exists false otherwise</returns>
        public override bool HasSection(string section)
        {
			return GetSection(section) != null;
		}

		/// <summary>Reads a whole section from the settings</summary>
		/// <param name="sectionName">Name of the section</param>
		/// <param name="remove">Remove comments and empty lines</param>
		/// <returns>Returns the whole section as string array</returns>
		public override string[] ReadSection(string sectionName, bool remove)
        {
            List<string> result = new List<string>();
			var section = GetSection(sectionName);
			if (section != null)
            {
                foreach (var item in section.Items)
                {
                    string name = item.Name.Trim();
                    if (remove && (name.StartsWith("#") || name.StartsWith(";"))) continue;
                    if (item.Value == null) result.Add(item.Name); else result.Add(item.Name + " = " + item.Value.Trim());
                }
            }
            return result.ToArray();
        }

		/// <summary>Reads a setting from the settings</summary>
		/// <param name="sectionName">Sectionname of the setting</param>
		/// <param name="name">Name of the setting</param>
		/// <returns>Returns null if the setting is not present a string otherwise</returns>
		public override string ReadSetting(string sectionName, string name)
		{
			var section = GetSection(sectionName);
			if (section != null)
			{
				var item = section.Items.FirstOrDefault(i => string.Equals(i.Name, name, StringComparison.CurrentCultureIgnoreCase));
				if (item != null)
				{
					return item.Value?.Trim();
				}
			}
			return null;
		}

        /// <summary>Loads all sections from the specified reader.</summary>
        /// <param name="reader">The reader.</param>
        /// <exception cref="System.IO.FileNotFoundException"></exception>
        public void Load(ISettings reader)
        {
			m_Sections.Clear();
            foreach (string section in reader.GetSectionNames())
            {
				WriteSection(section, reader.ReadSection(section));
            }
        }

        /// <summary>Clears this instance.</summary>
        public void Clear()
        {
            m_Sections.Clear();
        }

        /// <summary>Removes a whole section from the ini file</summary>
        /// <param name="sectionName">Name of the section</param>
        public void RemoveSection(string sectionName)
        {
			var section = GetSection(sectionName);
			m_Sections.Remove(section);
        }

        /// <summary>
        /// Writes (replaces) a whole section at the ini
        /// </summary>
        /// <param name="sectionName">Name of the section</param>
        /// <param name="value">The value.</param>
        public void WriteSection(string sectionName, string value)
        {
            WriteSection(sectionName, value.SplitNewLine());
        }

        /// <summary>
        /// Writes (replaces) a whole section at the ini
        /// </summary>
        /// <param name="sectionName">Name of the section</param>
        /// <param name="values">The values</param>
        /// <returns></returns>
        public void WriteSection(string sectionName, IEnumerable values)
        {
            if (sectionName == null) throw new ArgumentNullException("section");
            if (values == null) throw new ArgumentNullException("values");

            List<string> strings = new List<string>();
            foreach (object value in values)
            {
                strings.Add(StringExtensions.ToString(value, Culture));
            }
            WriteSection(sectionName, strings);
        }

        /// <summary>
        /// Writes (replaces) a whole section at the ini
        /// </summary>
        /// <param name="sectionName">Name of the section</param>
        /// <param name="lines">The lines</param>
        /// <returns></returns>
        public void WriteSection(string sectionName, IEnumerable<string> lines)
        {
            if (sectionName == null) throw new ArgumentNullException("section");
            if (lines == null) throw new ArgumentNullException("lines");

			Section newSection = new Section() { Name = sectionName };
			foreach (string line in lines)
			{
				int i = line.IndexOf('=');
				if (i > -1)
				{
					newSection.Items.Add(new Item() { Name = line.Substring(0, i), Value = line.Substring(i + 1) });
				}
				else
				{
					newSection.Items.Add(new Item() { Name = line, });
				}
			}
			Section section = GetSection(sectionName);
			if (section != null)
			{
				//replace
				int i = m_Sections.IndexOf(section);
				m_Sections[i] = newSection;
			}
			else
			{
				//add
				m_Sections.Add(newSection);
			}
        }

		/// <summary>
		/// Writes all fields of the struct to the specified section (replacing a present one) 
		/// </summary>
		/// <typeparam name="T">The struct type</typeparam>
		/// <param name="sectionName">The section to write to</param>
		/// <param name="item">The struct</param>
		public void WriteStruct<T>(string sectionName, T item) where T : struct
		{
			List<string> newSection = new List<string>();
			foreach (FieldInfo field in item.GetType().GetFields())
			{
				string value = StringExtensions.ToString(field.GetValue(item), Culture);
				newSection.Add(field.Name + " = " + value);
			}
			WriteSection(sectionName, newSection);
		}

        /// <summary>
        /// Writes all fields of the object to the specified section (replacing a present one) 
        /// </summary>
        /// <typeparam name="T">The class type</typeparam>
        /// <param name="sectionName">The section to write to</param>
        /// <param name="obj">The object</param>
        public void WriteObject<T>(string sectionName, T obj) where T : class
        {
            if (obj == null) throw new ArgumentNullException("obj");
			List<string> newSection = new List<string>();
			foreach (FieldInfo field in obj.GetType().GetFields())
            {
                string value = StringExtensions.ToString(field.GetValue(obj), Culture);
				newSection.Add(field.Name + " = " + value);
			}
			WriteSection(sectionName, newSection);
		}

		/// <summary>
		/// Writes a setting to the ini file (replacing a present one) 
		/// </summary>
		/// <param name="section">Name of the section</param>
		/// <param name="name">Name of the setting</param>
		/// <param name="value">Value of the setting</param>
		public void WriteSetting(string section, string name, object value)
		{
			WriteSetting(section, name, StringExtensions.ToString(value, Culture));
		}

		/// <summary>
		/// Writes a setting to the ini file (replacing a present one) 
		/// </summary>
		/// <param name="sectionName">Name of the section</param>
		/// <param name="name">Name of the setting</param>
		/// <param name="value">Value of the setting</param>
		public void WriteSetting(string sectionName, string name, string value)
        {
            if (sectionName == null) throw new ArgumentNullException("section");
            if (name == null) throw new ArgumentNullException("name");
            if (name.IndexOf('=') > -1) throw new ArgumentException(string.Format("Name may not contain an equal sign!"));

			var section = GetSection(sectionName);
			if (section == null)
			{
				section = new Section() { Name = sectionName };
				m_Sections.Add(section);
			}
			var item = section.Items.FirstOrDefault(i => string.Equals(i.Name, name, StringComparison.CurrentCultureIgnoreCase));
			if (item == null)
			{
				item = new Item() { Name = name };
				section.Items.Add(item);
			}
			item.Value = value;
        }

        /// <summary>
        /// Saves the content of the ini to a file readable by <see cref="IniReader"/>
        /// </summary>
        public void Save()
        {
			try { File.Open(Name, FileMode.OpenOrCreate).Close(); }
			catch (Exception ex) { throw new InvalidOperationException("You can use the Save() function only when 'Name' is the Path of the loaded file.", ex); }
            Stream stream = File.Open(Name, FileMode.Create, FileAccess.Write, FileShare.None);
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

                StreamWriter writer = new StreamWriter(stream, Properties.Encoding);
                foreach (var section in m_Sections)
                {
                    writer.WriteLine("[" + section.Name + "]");
                    foreach (var setting in section.Items)
                    {
                        if (setting.Value == null)
                        {
                            writer.WriteLine(setting.Name);
                        }
                        else
                        {
                            writer.WriteLine(setting.Name + " = " + setting.Value);
                        }
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
    }
}