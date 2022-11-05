﻿using System;
using System.IO;
using System.Threading.Tasks;
using CG.Framework.Helper;
using CG.Framework.Plugin.Output;

namespace CG.Output.UnityCpp.Files;

public class MemManagerCpp : IncludeFile<UnityCpp>
{
    public override string FileName { get; } = "MemoryManager.cpp";
    public override bool IncludeInMainSdkFile { get; } = false;

    public MemManagerCpp(UnityCpp lang) : base(lang)
    {
    }

    public override ValueTask<string> ProcessAsync(OutputProps processProps)
    {
        if (Lang.SdkFile is null)
            throw new InvalidOperationException("Invalid output target.");

        // Read File
        return CGUtils.ReadEmbeddedFileAsync(Path.Combine("External", FileName), this.GetType().Assembly);
    }
}