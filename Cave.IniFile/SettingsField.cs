using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;

namespace Cave
{
    /// <summary>
    /// Provides static functions for struct field reflections.
    /// </summary>
    static class SettingsField
    {
        /// <summary>
        /// Converts a (primitive) value to the desired type.
        /// </summary>
        /// <param name="toType">Type to convert to.</param>
        /// <param name="value">Value to convert.</param>
        /// <param name="cultureInfo">The culture to use during formatting.</param>
        /// <returns>Returns a new instance of the specified type.</returns>
        public static object ConvertPrimitive(Type toType, object value, IFormatProvider cultureInfo)
        {
            try
            {
                return Convert.ChangeType(value, toType, cultureInfo);
            }
            catch (Exception ex)
            {
                throw new NotSupportedException(string.Format("The value '{0}' cannot be converted to target type '{1}'!", value, toType), ex);
            }
        }

        /// <summary>
        /// Converts a value to the desired field value.
        /// </summary>
        /// <param name="toType">Type to convert to.</param>
        /// <param name="value">Value to convert.</param>
        /// <param name="cultureInfo">The culture to use during formatting.</param>
        /// <returns>Returns a new instance of the specified type.</returns>
        public static object ConvertValue(Type toType, object value, CultureInfo cultureInfo)
        {
            if (toType == null)
            {
                throw new ArgumentNullException("fieldType");
            }

            if (cultureInfo == null)
            {
                throw new ArgumentNullException("cultureInfo");
            }

            if (value == null)
            {
                return null;
            }

            if (toType.Name.StartsWith("Nullable"))
            {
#if NET45 || NET46 || NET47 || NETSTANDARD20
                toType = toType.GenericTypeArguments[0];
#elif NET20 || NET35 || NET40
                toType = toType.GetGenericArguments()[0];
#else
#error No code defined for the current framework or NETXX version define missing!
#endif
            }
            if (toType == typeof(bool))
            {
                switch (value.ToString().ToLower())
                {
                    case "true":
                    case "on":
                    case "yes":
                    case "1":
                        return true;
                    case "false":
                    case "off":
                    case "no":
                    case "0":
                        return false;
                }
            }
            if (toType.IsPrimitive)
            {
                return ConvertPrimitive(toType, value, cultureInfo);
            }

            if (toType.IsAssignableFrom(value.GetType()))
            {
                return Convert.ChangeType(value, toType);
            }

            if (toType.IsEnum)
            {
                return Enum.Parse(toType, value.ToString(), true);
            }

            // convert to string
            string str;
            {
                if (value is string)
                {
                    str = (string)value;
                }
                else
                {
                    // try to find public ToString(IFormatProvider) method in class
                    MethodInfo method = value.GetType().GetMethod("ToString", BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(IFormatProvider) }, null);
                    if (method != null)
                    {
                        try
                        {
                            str = (string)method.Invoke(value, new object[] { cultureInfo });
                        }
                        catch (TargetInvocationException ex)
                        {
                            throw ex.InnerException;
                        }
                    }
                    else
                    {
                        str = value.ToString();
                    }
                }
            }
            if (toType == typeof(string))
            {
                return str;
            }

            if (toType == typeof(DateTime))
            {
                if (long.TryParse(str, out var ticks))
                {
                    return new DateTime(ticks, DateTimeKind.Unspecified);
                }

                if (DateTimeParser.TryParseDateTime(str, out DateTime dt))
                {
                    return dt;
                }
            }
            if (toType == typeof(TimeSpan))
            {
                try
                {
                    if (str.Contains(":"))
                    {
                        return TimeSpan.Parse(str);
                    }
                    if (str.EndsWith("ms"))
                    {
                        return new TimeSpan((long)Math.Round(double.Parse(str.SubstringEnd(1)) * TimeSpan.TicksPerMillisecond));
                    }
                    return str.EndsWith("s")
                        ? new TimeSpan((long)Math.Round(double.Parse(str.SubstringEnd(1)) * TimeSpan.TicksPerSecond))
                        : (object)new TimeSpan(long.Parse(str));
                }
                catch (Exception ex)
                {
                    throw new InvalidDataException(string.Format("Value '{0}' is not a valid TimeSpan!", str), ex);
                }
            }

            // parse from string
            {
                // try to find public static Parse(string, IFormatProvider) method in class
                var errors = new List<Exception>();
                MethodInfo method = toType.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(string), typeof(IFormatProvider) }, null);
                if (method != null)
                {
                    try
                    {
                        return method.Invoke(null, new object[] { str, cultureInfo });
                    }
                    catch (TargetInvocationException ex)
                    {
                        errors.Add(ex.InnerException);
                    }
                }
                method = toType.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(string) }, null);
                if (method != null)
                {
                    try
                    {
                        return method.Invoke(null, new object[] { str });
                    }
                    catch (TargetInvocationException ex)
                    {
                        errors.Add(ex.InnerException);
                    }
                }
                if (errors.Count > 0)
                {
                    throw new AggregateException(errors.ToArray());
                }

                throw new MissingMethodException(string.Format("Type {0} has no public static Parse(string, IFormatProvider) or Parse(string) method!", toType));
            }
        }

        /// <summary>
        /// Sets all fieldvalues of a struct/class object.
        /// </summary>
        /// <param name="obj">structure object.</param>
        /// <param name="fields">fields to be set.</param>
        /// <param name="values">values to set.</param>
        /// <param name="cultureInfo">The culture to use during formatting.</param>
        public static void SetValues(ref object obj, IList<FieldInfo> fields, IList<object> values, CultureInfo cultureInfo)
        {
            if (obj == null)
            {
                throw new ArgumentNullException("obj");
            }

            if (fields == null)
            {
                throw new ArgumentNullException("fields");
            }

            if (values == null)
            {
                throw new ArgumentNullException("values");
            }

            if (cultureInfo == null)
            {
                throw new ArgumentNullException("cultureInfo");
            }

            for (var i = 0; i < values.Count; i++)
            {
                FieldInfo fieldInfo = fields[i];
                var value = ConvertValue(fieldInfo.FieldType, values[i], cultureInfo);
                fields[i].SetValue(obj, value);
            }
        }
    }
}
