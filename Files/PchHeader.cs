using System;
using System.IO;
using System.Threading.Tasks;
using CG.Framework.Helper;
using CG.Framework.Plugin.Output;

namespace CG.Output.UnityCpp.Files;

public class PchHeader : IncludeFile<UnityCpp>
{
    public override string FileName { get; } = "pch.h";
    public override bool IncludeInMainSdkFile { get; } = false;

    public PchHeader(UnityCpp lang) : base(lang) { }

    public override ValueTask<string> ProcessAsync(OutputProps processProps)
    {
        if (Lang.SdkFile is null)
            throw new InvalidOperationException("Invalid output target.");

        // Read File
        return CGUtils.ReadEmbeddedFileAsync(Path.Combine("Internal", FileName), this.GetType().Assembly);
    }
}
