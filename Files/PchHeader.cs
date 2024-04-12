using System;
using System.IO;
using System.Threading.Tasks;
using CG.SDK.Dotnet.Helper;
using CG.SDK.Dotnet.Plugin.Output;

namespace CG.Output.UnityCpp.Files;

public class PchHeader : IncludeFile<UnityCpp>
{
    public PchHeader(UnityCpp lang) : base(lang)
    {
    }

    public override string FileName { get; } = "pch.h";
    public override bool IncludeInMainSdkFile { get; } = false;

    public override ValueTask<string> ProcessAsync(OutputPurpose processPurpose)
    {
        if (Lang.SdkFile is null)
        {
            throw new InvalidOperationException("Invalid output target.");
        }

        // Read File
        return CGUtils.ReadEmbeddedFileAsync(Path.Combine("Internal", FileName), GetType().Assembly);
    }
}
