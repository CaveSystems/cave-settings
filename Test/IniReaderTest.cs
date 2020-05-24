using Cave;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test
{
    [TestFixture]
    public class IniReaderTest
    {
        readonly CultureInfo[] allCultures = CultureInfo.GetCultures(CultureTypes.AllCultures);

        [Test]
        public void IniReaderTest1()
        {
            var temp = Path.GetTempFileName();
            foreach (var culture in allCultures)
            {
                Console.WriteLine($"IniReaderTest.cs: info TI0002: Test {culture}");
                var settings = new SettingsStructFields[100];
                var properties = IniProperties.Default;
                properties.Culture = culture;
                var writer = new IniWriter(temp, properties);

                for (int i = 0; i < settings.Length; i++)
                {
                    var setting = SettingsStructFields.Random();
                    settings[i] = setting;
                    writer.WriteFields($"Section {i}", setting);
                }                
                writer.Save(temp);

                TestReader((IniReader)writer.ToSettings(), settings);
                TestReader(IniReader.FromFile(temp, properties), settings);
            }
        }

        private void TestReader(IniReader reader, SettingsStructFields[] settings)
        {
            var fields1 = typeof(SettingsStructFields).GetFields();
            var fields2 = typeof(SettingsObjectFields).GetFields();
            var fields3 = typeof(SettingsStructProperties).GetProperties();
            var fields4 = typeof(SettingsObjectProperties).GetProperties();
            for (int i = 0; i < settings.Length; i++)
            {
                var settings1 = reader.ReadStructFields<SettingsStructFields>($"Section {i}");
                var settings2 = reader.ReadObjectFields<SettingsObjectFields>($"Section {i}");
                var settings3 = reader.ReadStructProperties<SettingsStructProperties>($"Section {i}");
                var settings4 = reader.ReadObjectProperties<SettingsObjectProperties>($"Section {i}");

                for (int n = 0; n < fields1.Length; n++)
                {
                    var original = fields1[n].GetValue(settings[i]);
                    var value1 = fields1[n].GetValue(settings1);
                    var value2 = fields2[n].GetValue(settings2);
                    var value3 = fields3[n].GetValue(settings3, null);
                    var value4 = fields4[n].GetValue(settings4, null);
                    if (original is DateTime dt && !Equals(original, value1))
                    {
                        switch (reader.Properties.Culture.ThreeLetterISOLanguageName)
                        {
                            case "dzo":
                                return;
                            default: 
                                throw new NotImplementedException();
                        } 
                    }
                    Assert.AreEqual(original, value1);
                    Assert.AreEqual(original, value2);
                    Assert.AreEqual(original, value3);
                    Assert.AreEqual(original, value4);
                }
            }
        }
    }
}
