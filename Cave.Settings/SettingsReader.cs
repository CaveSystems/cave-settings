using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;

namespace Cave
{
    /// <summary>Provides a fast and simple initialization data reader class.</summary>
    /// <seealso cref="ISettings" />
    [DebuggerDisplay("{Name}")]
    public abstract class SettingsReader : ISettings
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SettingsReader"/> class.
        /// </summary>
        /// <param name="name">The name.</param>
        public SettingsReader(string name)
        {
            Name = name;
        }

        #region abstract class

        /// <summary>
        /// Gets a value indicating whether the config can be reloaded.
        /// </summary>
        public abstract bool CanReload { get; }

        /// <summary>
        /// Reload the whole config.
        /// </summary>
        public abstract void Reload();

        /// <summary>
        /// Gets the culture used to decode values.
        /// </summary>
        public abstract CultureInfo Culture { get; }

        /// <summary>
        /// Obtains all section names present at the file.
        /// </summary>
        /// <returns>
        /// Returns an array of all section names.
        /// </returns>
        public abstract string[] GetSectionNames();

        /// <summary>
        /// Obtains whether a specified section exists or not.
        /// </summary>
        /// <param name="section">Section to search.</param>
        /// <returns>
        /// Returns true if the sections exists false otherwise.
        /// </returns>
        public abstract bool HasSection(string section);

        /// <summary>
        /// Reads a whole section from the settings.
        /// </summary>
        /// <param name="section">Name of the section.</param>
        /// <param name="remove">Remove comments and empty lines.</param>
        /// <returns>
        /// Returns the whole section as string array.
        /// </returns>
        public abstract string[] ReadSection(string section, bool remove);

        /// <summary>
        /// Reads a setting from the settings.
        /// </summary>
        /// <param name="section">Sectionname of the setting.</param>
        /// <param name="name">Name of the setting.</param>
        /// <returns>
        /// Returns null if the setting is not present a string otherwise.
        /// </returns>
        public abstract string ReadSetting(string section, string name);
        #endregion

        /// <summary>
        /// Gets the name of the settings.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Reads a whole section from the ini (automatically removes empty lines and comments).
        /// </summary>
        /// <param name="section">Name of the section.</param>
        /// <returns>Returns an array of string containing all section lines.</returns>
        public string[] ReadSection(string section)
        {
            return ReadSection(section, true);
        }

        /// <summary>
        /// Reads a whole section as values of an enum and returns them as array.
        /// </summary>
        /// <typeparam name="T">The enum type.</typeparam>
        /// <param name="section">The Section to read.</param>
        /// <param name="throwEx">Throw an error for any unknown value in the section.</param>
        /// <returns>Returns an array of values.</returns>
        public T[] ReadEnums<T>(string section, bool throwEx = true)
            where T : struct
        {
            // iterate all lines of the section
            var result = new List<T>();
            foreach (string value in ReadSection(section, true))
            {
                // try to parse enum value
                try
                {
                    result.Add((T)Enum.Parse(typeof(T), value.Trim(), true));
                }
                catch (Exception ex)
                {
                    if (throwEx)
                    {
                        throw;
                    }

                    Trace.TraceWarning($"Ignoring Invalid Enum Value: {value}, Section: {section}, {ex}");
                }
            }
            return result.ToArray();
        }

        /// <summary>
        /// Reads a whole section as values of a struct.
        /// </summary>
        /// <typeparam name="T">The type of the struct.</typeparam>
        /// <param name="section">Section to read.</param>
        /// <param name="throwEx">Throw an error for any unset value in the section.</param>
        /// <returns>Returns a new struct instance.</returns>
        public T ReadStruct<T>(string section, bool throwEx = true)
            where T : struct
        {
            object result = default(T);
            ReadObject(section, result, throwEx);
            return (T)result;
        }

        /// <summary>Reads a whole section as values of a struct.</summary>
        /// <typeparam name="T">The type of the struct.</typeparam>
        /// <param name="section">Section to read.</param>
        /// <param name="item">The structure.</param>
        /// <param name="throwEx">Throw an error for any unset value in the section.</param>
        /// <returns>Returns true if all fields could be read. Throws an exception or returns false otherwise.</returns>
        public bool ReadStruct<T>(string section, ref T item, bool throwEx = true)
            where T : struct
        {
            object box = item;
            var result = ReadObject(section, box, throwEx);
            item = (T)box;
            return result;
        }

        /// <summary>
        /// Reads a whole section as values of a struct.
        /// </summary>
        /// <typeparam name="T">The type of the struct.</typeparam>
        /// <param name="section">Section to read.</param>
        /// <param name="throwEx">Throw an error for any invalid value in the section.</param>
        /// <returns>Returns a new struct instance.</returns>
        public T ReadObject<T>(string section, bool throwEx)
            where T : class, new()
        {
            object result = new T();
            ReadObject(section, result, throwEx);
            return (T)result;
        }

        /// <summary>
        /// Reads a whole section as values of an object (this does not work with structs).
        /// </summary>
        /// <param name="section">Section to read.</param>
        /// <param name="container">Container to set the field at.</param>
        /// <param name="throwEx">Throw an error for any unset value in the section.</param>
        /// <returns>Returns true if all fields could be read. Throws an exception or returns false otherwise.</returns>
        public bool ReadObject(string section, object container, bool throwEx = false)
        {
            if (container == null)
            {
                throw new ArgumentNullException("container");
            }

            // iterate all fields of the struct
            Type type = container.GetType();
            FieldInfo[] fields = type.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            if (fields.Length == 0)
            {
                if (!throwEx)
                {
                    return false;
                }

                throw new ArgumentException("Container does not have any fields!");
            }
            bool result = true;
            int i = 0;
            foreach (FieldInfo field in fields)
            {
                i++;

                // yes, can we read a value from the config for this field ?
                string value = ReadSetting(section, field.Name);
                if (string.IsNullOrEmpty(value))
                {
                    Trace.TraceError($"Field is not set, using default value: {field.FieldType.Name} {field.Name}");
                    continue;
                }
                value = value.UnboxText(false);

                // yes, try to set value to field
                try
                {
                    object obj = SettingsField.ConvertValue(field.FieldType, value, Culture);
                    field.SetValue(container, obj);
                }
                catch (Exception ex)
                {
                    string message = $"Invalid field value {value} for field {field.FieldType.Name} {field.Name}";
                    if (throwEx)
                    {
                        throw new InvalidDataException(message, ex);
                    }
                    else
                    {
                        Trace.TraceWarning(message);
                    }

                    result = false;
                }
            }
            if (i == 0)
            {
                string message = $"No field in section {section}!";
                if (throwEx)
                {
                    throw new ArgumentException(message, nameof(container));
                }
                else
                {
                    Trace.TraceWarning(message);
                }

                result = false;
            }
            return result;
        }

        #region Read Value Members

        /// <summary>Reads a string value.</summary>
        /// <param name="section">The section.</param>
        /// <param name="name">The name.</param>
        /// <param name="defaultValue">The default value.</param>
        /// <returns>Returns the (converted) value if a value is present or the default value if not.</returns>
        public string ReadString(string section, string name, string defaultValue = null)
        {
            string result = null;
            if (!GetValue(section, name, ref result))
            {
                result = defaultValue ?? throw new InvalidDataException(string.Format("Section [{0}] Setting {1} is unset!", section, name));
            }
            return result;
        }

        /// <summary>Reads a bool value.</summary>
        /// <param name="section">The section.</param>
        /// <param name="name">The name.</param>
        /// <param name="defaultValue">The default value.</param>
        /// <returns>Returns the (converted) value if a value is present or the default value if not.</returns>
        public bool ReadBool(string section, string name, bool? defaultValue = null)
        {
            bool result = false;
            if (!GetValue(section, name, ref result))
            {
                if (!defaultValue.HasValue)
                {
                    throw new InvalidDataException(string.Format("Section [{0}] Setting {1} is unset!", section, name));
                }
                result = defaultValue.Value;
            }
            return result;
        }

        /// <summary>Reads a int32 value.</summary>
        /// <param name="section">The section.</param>
        /// <param name="name">The name.</param>
        /// <param name="defaultValue">The default value.</param>
        /// <returns>Returns the (converted) value if a value is present or the default value if not.</returns>
        public int ReadInt32(string section, string name, int? defaultValue = null)
        {
            int result = 0;
            if (!GetValue(section, name, ref result))
            {
                if (!defaultValue.HasValue)
                {
                    throw new InvalidDataException(string.Format("Section [{0}] Setting {1} is unset!", section, name));
                }
                result = defaultValue.Value;
            }
            return result;
        }

        /// <summary>Reads a uint32 value.</summary>
        /// <param name="section">The section.</param>
        /// <param name="name">The name.</param>
        /// <param name="defaultValue">The default value.</param>
        /// <returns>Returns the (converted) value if a value is present or the default value if not.</returns>
        public uint ReadUInt32(string section, string name, uint? defaultValue = null)
        {
            uint result = 0;
            if (!GetValue(section, name, ref result))
            {
                if (!defaultValue.HasValue)
                {
                    throw new InvalidDataException(string.Format("Section [{0}] Setting {1} is unset!", section, name));
                }
                result = defaultValue.Value;
            }
            return result;
        }

        /// <summary>Reads a int64 value.</summary>
        /// <param name="section">The section.</param>
        /// <param name="name">The name.</param>
        /// <param name="defaultValue">The default value.</param>
        /// <returns>Returns the (converted) value if a value is present or the default value if not.</returns>
        public long ReadInt64(string section, string name, long? defaultValue = null)
        {
            long result = 0;
            if (!GetValue(section, name, ref result))
            {
                if (!defaultValue.HasValue)
                {
                    throw new InvalidDataException(string.Format("Section [{0}] Setting {1} is unset!", section, name));
                }
                result = defaultValue.Value;
            }
            return result;
        }

        /// <summary>Reads a uint64 value.</summary>
        /// <param name="section">The section.</param>
        /// <param name="name">The name.</param>
        /// <param name="defaultValue">The default value.</param>
        /// <returns>Returns the (converted) value if a value is present or the default value if not.</returns>
        public ulong ReadUInt64(string section, string name, ulong? defaultValue = null)
        {
            ulong result = 0;
            if (!GetValue(section, name, ref result))
            {
                if (!defaultValue.HasValue)
                {
                    throw new InvalidDataException(string.Format("Section [{0}] Setting {1} is unset!", section, name));
                }
                result = defaultValue.Value;
            }
            return result;
        }

        /// <summary>Reads a float value.</summary>
        /// <param name="section">The section.</param>
        /// <param name="name">The name.</param>
        /// <param name="defaultValue">The default value.</param>
        /// <returns>Returns the (converted) value if a value is present or the default value if not.</returns>
        public float ReadFloat(string section, string name, float? defaultValue = null)
        {
            float result = 0;
            if (!GetValue(section, name, ref result))
            {
                if (!defaultValue.HasValue)
                {
                    throw new InvalidDataException(string.Format("Section [{0}] Setting {1} is unset!", section, name));
                }
                result = defaultValue.Value;
            }
            return result;
        }

        /// <summary>Reads a double value.</summary>
        /// <param name="section">The section.</param>
        /// <param name="name">The name.</param>
        /// <param name="defaultValue">The default value.</param>
        /// <returns>Returns the (converted) value if a value is present or the default value if not.</returns>
        public double ReadDouble(string section, string name, double? defaultValue = null)
        {
            double result = 0;
            if (!GetValue(section, name, ref result))
            {
                if (!defaultValue.HasValue)
                {
                    throw new InvalidDataException(string.Format("Section [{0}] Setting {1} is unset!", section, name));
                }
                result = defaultValue.Value;
            }
            return result;
        }

        /// <summary>Reads a decimal value.</summary>
        /// <param name="section">The section.</param>
        /// <param name="name">The name.</param>
        /// <param name="defaultValue">The default value.</param>
        /// <returns>Returns the (converted) value if a value is present or the default value if not.</returns>
        public decimal ReadDecimal(string section, string name, decimal? defaultValue = null)
        {
            decimal result = 0;
            if (!GetValue(section, name, ref result))
            {
                if (!defaultValue.HasValue)
                {
                    throw new InvalidDataException(string.Format("Section [{0}] Setting {1} is unset!", section, name));
                }
                result = defaultValue.Value;
            }
            return result;
        }

        /// <summary>Reads a time span value.</summary>
        /// <param name="section">The section.</param>
        /// <param name="name">The name.</param>
        /// <param name="defaultValue">The default value.</param>
        /// <returns>Returns the (converted) value if a value is present or the default value if not.</returns>
        public TimeSpan ReadTimeSpan(string section, string name, TimeSpan? defaultValue = null)
        {
            TimeSpan result = TimeSpan.Zero;
            if (!GetValue(section, name, ref result))
            {
                if (!defaultValue.HasValue)
                {
                    throw new InvalidDataException(string.Format("Section [{0}] Setting {1} is unset!", section, name));
                }
                result = defaultValue.Value;
            }
            return result;
        }

        /// <summary>Reads a date time value.</summary>
        /// <param name="section">The section.</param>
        /// <param name="name">The name.</param>
        /// <param name="defaultValue">The default value.</param>
        /// <returns>Returns the (converted) value if a value is present or the default value if not.</returns>
        public DateTime ReadDateTime(string section, string name, DateTime? defaultValue = null)
        {
            DateTime result = DateTime.MinValue;
            if (!GetValue(section, name, ref result))
            {
                if (!defaultValue.HasValue)
                {
                    throw new InvalidDataException(string.Format("Section [{0}] Setting {1} is unset!", section, name));
                }
                result = defaultValue.Value;
            }
            return result;
        }

        /// <summary>Reads the enum.</summary>
        /// <typeparam name="T">Enum type.</typeparam>
        /// <param name="section">The section.</param>
        /// <param name="name">The name.</param>
        /// <param name="defaultValue">The default value.</param>
        /// <returns>Returns the (converted) value if a value is present or the default value if not.</returns>
        public T ReadEnum<T>(string section, string name, T? defaultValue = null)
            where T : struct, IConvertible
        {
            var result = default(T);
            if (!GetEnum(section, name, ref result))
            {
                if (!defaultValue.HasValue)
                {
                    throw new InvalidDataException(string.Format("Section [{0}] Setting {1} is unset!", section, name));
                }
                result = defaultValue.Value;
            }
            return result;
        }

        #endregion Read Value Members
        #region GetValue Members

        /// <summary>
        /// Directly obtains a value from the specified subsection(s) with the specified name.
        /// </summary>
        /// <param name="section">The subsection(s).</param>
        /// <param name="name">The name of the setting.</param>
        /// <param name="value">The default value.</param>
        /// <returns>Returns true if the setting exist and the read value was returned, true otherwise (default value returned).</returns>
        public bool GetValue(string section, string name, ref bool value)
        {
            string v = ReadSetting(section, name);
            if (!string.IsNullOrEmpty(v))
            {
                if (bool.TryParse(v, out bool b))
                {
                    value = b;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Directly obtains a value from the specified subsection(s) with the specified name.
        /// </summary>
        /// <param name="section">The subsection(s).</param>
        /// <param name="name">The name of the setting.</param>
        /// <param name="value">The default value.</param>
        /// <returns>Returns true if the setting exist and the read value was returned, true otherwise (default value returned).</returns>
        public bool GetValue(string section, string name, ref string value)
        {
            string v = ReadSetting(section, name);
            if (string.IsNullOrEmpty(v))
            {
                return false;
            }

            value = v;
            return true;
        }

        /// <summary>
        /// Directly obtains a value from the specified subsection(s) with the specified name.
        /// </summary>
        /// <param name="section">The subsection(s).</param>
        /// <param name="name">The name of the setting.</param>
        /// <param name="value">The default value.</param>
        /// <returns>Returns true if the setting exist and the read value was returned, true otherwise (default value returned).</returns>
        public bool GetValue(string section, string name, ref int value)
        {
            string data = value.ToString(Culture);
            if (!GetValue(section, name, ref data))
            {
                return false;
            }

            if (int.TryParse(data, NumberStyles.Any, Culture, out int result))
            {
                value = result;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Directly obtains a value from the specified subsection(s) with the specified name.
        /// </summary>
        /// <param name="section">The subsection(s).</param>
        /// <param name="name">The name of the setting.</param>
        /// <param name="value">The default value.</param>
        /// <returns>Returns true if the setting exist and the read value was returned, true otherwise (default value returned).</returns>
        public bool GetValue(string section, string name, ref uint value)
        {
            string data = value.ToString(Culture);
            if (!GetValue(section, name, ref data))
            {
                return false;
            }

            if (uint.TryParse(data, NumberStyles.Any, Culture, out uint result))
            {
                value = result;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Directly obtains a value from the specified subsection(s) with the specified name.
        /// </summary>
        /// <param name="section">The subsection(s).</param>
        /// <param name="name">The name of the setting.</param>
        /// <param name="value">The default value.</param>
        /// <returns>Returns true if the setting exist and the read value was returned, true otherwise (default value returned).</returns>
        public bool GetValue(string section, string name, ref long value)
        {
            string data = value.ToString(Culture);
            if (!GetValue(section, name, ref data))
            {
                return false;
            }

            if (long.TryParse(data, NumberStyles.Any, Culture, out long result))
            {
                value = result;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Directly obtains a value from the specified subsection(s) with the specified name.
        /// </summary>
        /// <param name="section">The subsection(s).</param>
        /// <param name="name">The name of the setting.</param>
        /// <param name="value">The default value.</param>
        /// <returns>Returns true if the setting exist and the read value was returned, true otherwise (default value returned).</returns>
        public bool GetValue(string section, string name, ref ulong value)
        {
            string data = value.ToString(Culture);
            if (!GetValue(section, name, ref data))
            {
                return false;
            }

            if (ulong.TryParse(data, NumberStyles.Any, Culture, out ulong result))
            {
                value = result;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Directly obtains a value from the specified subsection(s) with the specified name.
        /// </summary>
        /// <param name="section">The subsection(s).</param>
        /// <param name="name">The name of the setting.</param>
        /// <param name="value">The default value.</param>
        /// <returns>Returns true if the setting exist and the read value was returned, true otherwise (default value returned).</returns>
        public bool GetValue(string section, string name, ref float value)
        {
            string data = value.ToString("R", Culture);
            if (!GetValue(section, name, ref data))
            {
                return false;
            }

            if (float.TryParse(data, NumberStyles.Any, Culture, out float result))
            {
                value = result;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Directly obtains a value from the specified subsection(s) with the specified name.
        /// </summary>
        /// <param name="section">The subsection(s).</param>
        /// <param name="name">The name of the setting.</param>
        /// <param name="value">The default value.</param>
        /// <returns>Returns true if the setting exist and the read value was returned, true otherwise (default value returned).</returns>
        public bool GetValue(string section, string name, ref double value)
        {
            string data = value.ToString("R", Culture);
            if (!GetValue(section, name, ref data))
            {
                return false;
            }

            if (double.TryParse(data, NumberStyles.Any, Culture, out double result))
            {
                value = result;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Directly obtains a value from the specified subsection(s) with the specified name.
        /// </summary>
        /// <param name="section">The subsection(s).</param>
        /// <param name="name">The name of the setting.</param>
        /// <param name="value">The default value.</param>
        /// <returns>Returns true if the setting exist and the read value was returned, true otherwise (default value returned).</returns>
        public bool GetValue(string section, string name, ref decimal value)
        {
            string data = value.ToString(Culture);
            if (!GetValue(section, name, ref data))
            {
                return false;
            }

            if (decimal.TryParse(data, NumberStyles.Any, Culture, out decimal result))
            {
                value = result;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Directly obtains a value from the specified subsection(s) with the specified name.
        /// </summary>
        /// <param name="section">The subsection(s).</param>
        /// <param name="name">The name of the setting.</param>
        /// <param name="value">The default value.</param>
        /// <returns>Returns true if the setting exist and the read value was returned, true otherwise (default value returned).</returns>
        public bool GetValue(string section, string name, ref DateTime value)
        {
            string data = value.ToString(Culture);
            if (!GetValue(section, name, ref data))
            {
                return false;
            }

            if (StringExtensions.TryParseDateTime(data, out DateTime result))
            {
                value = result;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Directly obtains a value from the specified subsection(s) with the specified name.
        /// </summary>
        /// <param name="section">The subsection(s).</param>
        /// <param name="name">The name of the setting.</param>
        /// <param name="value">The default value.</param>
        /// <returns>Returns true if the setting exist and the read value was returned, true otherwise (default value returned).</returns>
        public bool GetValue(string section, string name, ref TimeSpan value)
        {
            string data = value.ToString();
            if (!GetValue(section, name, ref data))
            {
                return false;
            }

            if (TimeSpan.TryParse(data, out TimeSpan result))
            {
                value = result;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Directly obtains a (enum) value from the specified subsection(s) with the specified name.
        /// </summary>
        /// <typeparam name="T">Type of the enum.</typeparam>
        /// <param name="section">The subsection(s).</param>
        /// <param name="name">The name of the setting.</param>
        /// <param name="value">The default value.</param>
        /// <returns>Returns true if the setting exist and the read value was returned, true otherwise (default value returned).</returns>
        public bool GetEnum<T>(string section, string name, ref T value)
            where T : struct, IConvertible
        {
            string data = value.ToString();
            if (!GetValue(section, name, ref data))
            {
                return false;
            }

            T resultValue = value;
            bool result = data.TryParse(out resultValue);
            if (result)
            {
                value = resultValue;
            }

            return result;
        }
        #endregion

    }
}
