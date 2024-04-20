using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CG.SDK.Dotnet.Engine.Models;
using CG.SDK.Dotnet.Helper;
using CG.SDK.Dotnet.Plugin.Output;

namespace CG.Output.Files;

public class UnitTest : IncludeFile<UnityCpp>
{
    public UnitTest(UnityCpp lang) : base(lang)
    {
    }

    public override string FileName => "UnitTest.cpp";

    public override bool IncludeInMainSdkFile { get; } = false;

    public override async ValueTask<string> ProcessAsync(OutputPurpose processPurpose)
    {
        // Read File
        string fileStr = await CGUtils.ReadEmbeddedFileAsync(Path.Combine("Internal", FileName), GetType().Assembly)
            .ConfigureAwait(false);

        // CLASSES_ASSERT
        const string unitTemplate = @"
		// {0}
		TEST_METHOD({1})
		{
			// Fields
{2}
			// Size
			CHEAT_GEAR_CHECK_SIZE({3}, {4});
		}";

        List<string> GenTestString(IEnumerable<EngineStruct> ss)
        {
            return ss.Where(c => !c.NameCpp.EndsWith("_Class"))
                .Select(
                    c =>
                    {
                        string cheatGearClassName = $"{Lang.SdkFile.Namespace}::{c.NameCpp}";
                        string[] memberTests = c.Fields.Where(m => !m.IsStatic && !m.IsBitField)
                            .Select(
                                m =>
                                    $"\t\t\tCHEAT_GEAR_CHECK_OFFSET({{3}}, {m.Name.Split('[')[0].Split(':')[0].Trim()}, 0x{m.Offset:X4});"
                            )
                            .ToArray();

                        return unitTemplate.Replace("{0}", c.FullName)
                            .Replace("{1}", c.FullName.Replace(" ", "__").Replace(".", "__").Replace("-", "_"))
                            .Replace("{2}", string.Join(Environment.NewLine, memberTests))
                            .Replace("{3}", cheatGearClassName)
                            .Replace("{4}", $"0x{c.Size:X4}");
                    }
                )
                .ToList();
        }

        List<string> classAssertStr = GenTestString(Lang.SavedClasses);
        List<string> structsAssertStr = GenTestString(Lang.SavedStructs);

        var fullAssertStr = new List<string>();
        fullAssertStr.AddRange(classAssertStr);
        fullAssertStr.AddRange(structsAssertStr);

        fileStr = fileStr.Replace("/*!!CLASSES_ASSERT!!*/", string.Join(Environment.NewLine, fullAssertStr));

        return fileStr;
    }
}
