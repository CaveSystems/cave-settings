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
#endregion

using Cave.Compression;
using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Cave
{
    /// <summary>
    /// Provides properties for the <see cref="IniReader"/> and <see cref="IniWriter"/> classes.
    /// </summary>
    public struct IniProperties : IEquatable<IniProperties>
    {
        /// <summary>Implements the operator ==.</summary>
        /// <param name="properties1">The properties1.</param>
        /// <param name="properties2">The properties2.</param>
        /// <returns>The result of the operator.</returns>
        public static bool operator ==(IniProperties properties1, IniProperties properties2)
        {
            return properties1.Equals(properties2);
        }

        /// <summary>Implements the operator !=.</summary>
        /// <param name="properties1">The properties1.</param>
        /// <param name="properties2">The properties2.</param>
        /// <returns>The result of the operator.</returns>
        public static bool operator !=(IniProperties properties1, IniProperties properties2)
        {
            return !properties1.Equals(properties2);
        }

        /// <summary>
        /// Obtains <see cref="IniProperties"/> with default settings:
        /// Encoding=UTF8, Compression=None, InvariantCulture and no encryption
        /// </summary>
        public static IniProperties Default
        {
            get
            {
                IniProperties result = new IniProperties();
                result.Culture = CultureInfo.InvariantCulture;
                result.Compression = CompressionType.None;
                result.Encoding = new UTF8Encoding(false);
                result.DateTimeFormat = StringExtensions.InterOpDateTimeFormat;
                return result;
            }
        }

		/// <summary>
		/// Obtains <see cref="IniProperties"/> with default settings and simple encryption.
		/// (This is not a security feature, use file system acl to protect from other users.)
		/// </summary>
		/// <param name="password"></param>
		/// <returns></returns>
		public static IniProperties Encrypted(string password)
		{
			byte[] salt = new byte[16];
			for (byte i = 0; i < salt.Length; salt[i] = ++i)
            {
                ;
            }

            PasswordDeriveBytes PBKDF1 = new PasswordDeriveBytes(password, salt);
			IniProperties result = Default;
			result.Encryption = new RijndaelManaged();
			result.Encryption.BlockSize = 128;
			result.Encryption.Key = PBKDF1.GetBytes(result.Encryption.KeySize / 8);
			result.Encryption.IV = PBKDF1.GetBytes(result.Encryption.BlockSize / 8);
			(PBKDF1 as IDisposable)?.Dispose();
			return result;
		}

        /// <summary>
        /// Default is case insensitive. Set this to true to match properties exactly.
        /// </summary>
        public bool CaseSensitive;

        /// <summary>
        /// Use simple synchroneous encryption to protect from users eyes ? 
        /// (This is not a security feature, use file system acl to protect from other users.)
        /// </summary>
        public SymmetricAlgorithm Encryption;

        /// <summary>
        /// Gets / sets the culture used to en/decode values
        /// </summary>
        public CultureInfo Culture;

        /// <summary>
        /// Gets / sets the <see cref="CompressionType"/>
        /// </summary>
        public CompressionType Compression;

        /// <summary>
        /// Gets / sets the <see cref="Encoding"/>
        /// </summary>
        public Encoding Encoding;

        /// <summary>
        /// Gets / sets the format of date time fields
        /// </summary>
        public string DateTimeFormat;

        /// <summary>
        /// Obtains whether the properties are all set or not
        /// </summary>
        public bool Valid
        {
            get
            {
                return
                    Enum.IsDefined(typeof(CompressionType), Compression) &&
                    (Encoding != null) &&
                    (Culture != null);
            }
        }

        /// <summary>Returns a hash code for this instance.</summary>
        /// <returns>A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table. </returns>
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        /// <summary>Determines whether the specified <see cref="object" />, is equal to this instance.</summary>
        /// <param name="obj">The <see cref="object" /> to compare with this instance.</param>
        /// <returns><c>true</c> if the specified <see cref="object" /> is equal to this instance; otherwise, <c>false</c>.</returns>
        public override bool Equals(object obj)
        {
            if (obj is IniProperties)
            {
                return base.Equals((IniProperties)obj);
            }

            return false;
        }

        /// <summary>Determines whether the specified <see cref="IniProperties" />, is equal to this instance.</summary>
        /// <param name="other">The <see cref="IniProperties" /> to compare with this instance.</param>
        /// <returns><c>true</c> if the specified <see cref="IniProperties" /> is equal to this instance; otherwise, <c>false</c>.</returns>
        public bool Equals(IniProperties other)
        {
            return other.CaseSensitive == CaseSensitive
                && other.Compression == Compression
                && other.Culture == Culture
                && other.DateTimeFormat == DateTimeFormat
                && other.Encoding == Encoding
                && other.Encryption == Encryption;
        }
    }
}
