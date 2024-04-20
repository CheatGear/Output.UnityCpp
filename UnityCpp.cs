using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CG.Output.Files;
using CG.Output.Helper;
using CG.SDK.Dotnet.Attributes;
using CG.SDK.Dotnet.Engine;
using CG.SDK.Dotnet.Engine.Models;
using CG.SDK.Dotnet.Engine.Unity;
using CG.SDK.Dotnet.Engine.Unreal;
using CG.SDK.Dotnet.Helper;
using CG.SDK.Dotnet.Helper.IO;
using CG.SDK.Dotnet.Plugin.Output;
using LangPrint;
using LangPrint.Cpp;

namespace CG.Output;

internal enum CppOptions
{
    PrecompileSyntax,
}

[PluginInfo(
    Name = nameof(UnityCpp),
    Version = "5.0.0",
    Author = "CorrM",
    Description = "Cpp syntax support for Unity",
    WebsiteLink = "https://github.com/CheatGear",
    SourceCodeLink = "https://github.com/CheatGear/Output.UnityCpp"
)]
public sealed class UnityCpp : OutputPlugin<UnitySdkFile>
{
    private CppProcessor _cppProcessor;

    protected override Dictionary<string, string> LangTypes { get; } = new()
    {
        // SdkVarType       LangType
        { "int64", "int64_t" },
        { "int32", "int32_t" },
        { "int16", "int16_t" },
        { "int8", "int8_t" },
        { "uint64", "uint64_t" },
        { "uint32", "uint32_t" },
        { "uint16", "uint16_t" },
        { "uint8", "uint8_t" },
    };

    internal List<EngineClass> SavedClasses { get; } = [];
    internal List<EngineStruct> SavedStructs { get; } = [];

    public override string OutputName => "Cpp";

    public override GameEngine SupportedEngines => GameEngine.Unity;

    public override OutputPurpose SupportedPurpose => OutputPurpose.Internal /* | OutputProps.External*/;

    public override IReadOnlyDictionary<Enum, OutputOption> Options { get; } = new Dictionary<Enum, OutputOption>
    {
        {
            CppOptions.PrecompileSyntax, new OutputOption(
                "Precompile Syntax",
                OutputOptionType.CheckBox,
                "Use precompile headers for most build speed",
                "true"
            )
        },
    };

    private List<string> BuildMethodBody(EngineStruct @class, EngineFunction function)
    {
        var body = new List<string>();

        // Function init
        {
            string prefix;
            string initBody;

            prefix = "static UFunction* fn = ";
            initBody = $"UObject::GetObjectCasted<UFunction>({function.Index});";

            body.Add($"{prefix}{initBody}");
            body.Add("");
        }

        // Parameters
        {
            //if (Options[CppOptions.GenerateParametersFile].Value == "true")
            //{
            //    body.Add($"{@class.NameCpp}_{function.Name}_Params params {{}};");
            //}
            //else
            //{
            //    body.Add("struct");
            //    body.Add("{");
            //
            //    foreach (EngineParameter param in function.Parameters)
            //    {
            //        if (param.IsReturn)
            //            continue;
            //
            //        if (param.Name.StartsWith("UnknownData_") && param.Type == "unsigned char")
            //            continue;
            //
            //        body.Add($"\t{param.Type,-50} {param.Name};");
            //    }
            //
            //    body.Add("} params;");
            //}

            List<EngineParameter> validParams = function.Parameters.Where(p => p.IsDefault).ToList();
            if (validParams.Count > 0)
            {
                foreach (EngineParameter param in validParams)
                {
                    // Not needed
                    if (param.Name.StartsWith("UnknownData_") && param.Type == "unsigned char")
                    {
                        continue;
                    }

                    body.Add($"params.{param.Name} = {param.Name};");
                }
            }

            body.Add("");
        }

        // Function call
        {
            body.Add("auto flags = fn->FunctionFlags;");

            if (function.IsNative)
            {
                body.Add($"fn->FunctionFlags |= 0x{UnrealFunctionFlags.UE4Native:X};");
            }

            if (function.IsStatic)
            {
                //string prefix;
                //if (Options[CppOptions.LazyFindObject].Value == "true")
                //{
                //    body.Add("static UObject* defaultObj = nullptr;");
                //    body.Add("if (!defaultObj)");
                //    prefix = "\tdefaultObj = ";
                //}
                //else
                //{
                //    prefix = "static UObject* defaultObj = ";
                //}

                //body.Add($"{prefix}StaticClass()->CreateDefaultObject<{@class.NameCpp}>();");
                body.Add("defaultObj->ProcessEvent(fn, &params);");
            }
            else
            {
                body.Add("UObject::ProcessEvent(fn, &params);");
            }

            body.Add("fn->FunctionFlags = flags;");
        }

        // Out Parameters
        {
            List<EngineParameter> rOut = function.Parameters.Where(p => p.IsOut).ToList();
            if (rOut.Count > 0)
            {
                body.Add("");
                foreach (EngineParameter param in rOut)
                {
                    body.Add($"if ({param.Name} != nullptr)");
                    body.Add($"\t*{param.Name} = params.{param.Name};");
                }
            }
        }

        // Return Value
        {
            List<EngineParameter> retParams = function.Parameters.Where(p => p.IsReturn).ToList();
            if (retParams.Count > 0)
            {
                body.Add("");
                body.Add($"return params.{retParams[0].Name};");
            }
        }

        return body;
    }

    private void AddPredefinedMethodsToStruct(EngineStruct es)
    {
    }

    private void AddPredefinedMethodsToClass(EngineClass ec)
    {
    }

    private static List<string> GenerateMethodConditions()
    {
        return [];
    }

    private void PreparePackageModel(CppPackage cppPackage, IEnginePackage enginePackage)
    {
        // # Conditions
        //if (Options[CppOptions.OffsetsOnly].Value == "true")
        //    cppPackage.Conditions.Add(nameof(CppOptions.OffsetsOnly));

        if (enginePackage.IsPredefined && enginePackage.Name == "BasicTypes")
        {
            //cppPackage.Pragmas.Add("warning(disable: 4267)");
            // # conditions
            //if (processProps == OutputProps.External)
            //    cppPackage.Conditions.Add("EXTERNAL_PROPS");
            //fileStr.Replace("/*!!POINTER_SIZE!!*/", sdkFile.Is64BitGame ? "0x08" : "0x04");
            //fileStr.Replace("/*!!FText_SIZE!!*/", $"0x{Lang.EngineConfig.GetStruct("FText").Size:X}");
            //// Set FUObjectItem_MEMBERS
            //{
            //    List<PredefinedMember> fUObjectItemMembers = Lang.EngineConfig.GetStructAsLangMembers(Lang, "FUObjectItem");
            //    string fUObjectItemStr = string.Join(Environment.NewLine, fUObjectItemMembers.Select(variable => $"\t{variable.Type} {variable.Name};"));
            //    fileStr.Replace("/*!!FUObjectItem_MEMBERS!!*/", fUObjectItemStr);
            //}

            cppPackage.Structs.First(s => s.Name == "Il2CppObject").Alignas = SdkFile.Is64BitGame ? 0x08 : 0x04;

            /*
            CppFunction initFieldsFunc = cppPackage.Functions.Find(cf => cf.Name == "InitFields");
            if (initFieldsFunc is not null)
            {
                // Make static fields string
                var sb = new StringBuilder();
                foreach (IEnginePackage package in SdkFile.Packages)
                {
                    IEnumerable<EngineStruct> structs = package.Structs.Concat(package.Classes);
                    foreach (EngineStruct engineStruct in structs)
                    {
                        foreach (EngineField engineField in engineStruct.Fields.Where(f => f.IsStatic))
                        {
                            sb.Append("\t\t");
                            sb.AppendLine($"{engineStruct.NameCpp}::{engineField.Name} = {engineField.Value};");

                            // Clear value as it will be set from InitSdk Func
                            engineField.Value = "";
                        }
                    }
                }

                for (var i = 0; i < initFieldsFunc.Body.Count; i++)
                {
                    string s = initFieldsFunc.Body[i]
                        .Replace("INIT_STATIC_FIELDS_STR", sb.ToString());
                    initFieldsFunc.Body[i] = s;
                }
            }
            */

            CppFunction? initZeroParamFunc =
                cppPackage.Functions.Find(cf => cf.Name == "InitSdk" && cf.Params.Count == 0);
            if (initZeroParamFunc is not null)
            {
                for (int i = 0; i < initZeroParamFunc.Body.Count; i++)
                {
                    string s = initZeroParamFunc.Body[i]
                        .Replace("MODULE_NAME", SdkFile.GameModule);
                    initZeroParamFunc.Body[i] = s;
                }
            }
        }
    }

    private void PrepareEngineFunction(EngineStruct parent, EngineFunction eFunc)
    {
        //if (!eFunc.IsPredefined)
        //    eFunc.Body = BuildMethodBody(parent, eFunc);
    }

    private void PrepareCppFunction(EngineFunction originalFunc, CppFunction cppFunc)
    {
    }

    private CppStruct ConvertStruct(EngineStruct eStruct)
    {
        // Prepare methods
        foreach (EngineFunction eFunc in eStruct.Methods)
        {
            PrepareEngineFunction(eStruct, eFunc);
        }

        // Const not in struct memory layout
        // ex: FVector2 have `kEpsilon` and `kEpsilonNormalSqrt`
        eStruct.Fields.RemoveAll(ef => ef.IsConst);

        // Convert
        CppStruct cppStruct = eStruct.ToCpp();

#if !DEBUG
        // TODO: Here clearing methods to not print them
        cppStruct.Methods.Clear();
#endif

        if (eStruct is EngineClass)
        {
            cppStruct.IsClass = true;
        }

        foreach (CppFunction cppFunc in cppStruct.Methods)
        {
            cppFunc.Conditions.AddRange(GenerateMethodConditions());
            PrepareCppFunction(eStruct.Methods.Find(m => m.Name == cppFunc.Name), cppFunc);
        }

        return cppStruct;
    }

    private IEnumerable<CppDefine> GetDefines(IEnginePackage enginePackage)
    {
        return enginePackage.Defines.Select(ec => ec.ToCpp());
    }

    private IEnumerable<CppConstant> GetConstants(IEnginePackage enginePackage)
    {
        return enginePackage.Constants.Select(ec => ec.ToCpp());
    }

    private List<CppEnum> GetEnums(IEnginePackage enginePackage)
    {
        List<CppEnum> ret = enginePackage.Enums
            .Select(ee => ee.ToCpp())
            .ToList();

        foreach (CppEnum cppEnum in ret)
        {
            bool haveValue = cppEnum.Values[0].Name is "value" or "value__";
            if (haveValue)
            {
                cppEnum.Values.RemoveAt(0);
            }
        }

        return ret;
    }

    private IEnumerable<CppField> GetFields(IEnginePackage enginePackage)
    {
        return enginePackage.Fields.Select(ef => ef.ToCpp());
    }

    private IEnumerable<CppFunction> GetFunctions(IEnginePackage enginePackage)
    {
        return enginePackage.Functions.Select(ef => ef.ToCpp());
    }

    private IEnumerable<CppStruct> GetStructs(IEnginePackage enginePackage)
    {
        return enginePackage.Structs.Select(ConvertStruct);
    }

    private IEnumerable<CppStruct> GetClasses(IEnginePackage enginePackage)
    {
        return enginePackage.Classes.Select(ConvertStruct);
    }

    private IEnumerable<CppStruct> GetFuncParametersStructs(IEnginePackage enginePackage)
    {
        var ret = new List<CppStruct>();
        IEnumerable<(EngineClass, EngineFunction)> functions = enginePackage.Classes
            .SelectMany(@class => @class.Methods.Select(func => (@class, func)))
            .Where(classFunc => !classFunc.func.IsPredefined);

        foreach ((EngineClass @class, EngineFunction func) in functions)
        {
            var cppParamStruct = new CppStruct
            {
                Name = $"{@class.NameCpp}_{func.Name}_Params",
                IsClass = false,
                Comments = [func.FullName],
            };

            foreach (EngineParameter p in func.Parameters)
            {
                if (p.IsReturn)
                {
                    continue;
                }

                if (p.Name.StartsWith("UnknownData_") && p.Type == "unsigned char")
                {
                    continue;
                }

                var cppVar = new CppField
                {
                    Type = p.Type,
                    Name = p.Name,
                    InlineComment = $"0x{p.Offset:X4}(0x{p.Size:X4}) {p.Comment} ({p.FlagsString})",
                };

                cppParamStruct.Fields.Add(cppVar);
            }

            ret.Add(cppParamStruct);
        }

        return ret;
    }

    private string MakeFuncParametersFile(CppPackage package, IEnumerable<CppStruct> paramStructs)
    {
        var sb = new StringBuilder();
        var pragmas = new List<string> { "once" };
        var includes = new List<string>();

        if (Options[CppOptions.PrecompileSyntax].Value != "true")
        {
            includes.Add("\"../SDK.h\"");
        }

        // File header
        sb.Append(
            _cppProcessor.GetFileHeader(
                package.HeadingComment,
                package.NameSpace,
                pragmas,
                includes,
                null,
                package.TypeDefs,
                package.BeforeNameSpace,
                out int indentLvl
            )
        );

        // Structs
        sb.Append(_cppProcessor.GenerateStructs(paramStructs, indentLvl, null));

        // File footer
        sb.Append(_cppProcessor.GetFileFooter(package.NameSpace, package.AfterNameSpace, ref indentLvl));

        return sb.ToString();
    }

    /// <summary>
    ///     Merge packages in one package
    /// </summary>
    /// <param name="packages">Packages to merge</param>
    /// <param name="packageName">Merged package name</param>
    /// <returns>Merged package</returns>
    private UnityPackage MergePackages(IEnumerable<UnityPackage> packages, string packageName)
    {
        var bigPackage = new UnityPackage(UnityEngineVersion.UnityIl2Cpp, null)
        {
            Name = packageName,
            CppName = packageName,
        };

        foreach (UnityPackage package in packages)
        {
            bigPackage.Structs.AddRange(package.Structs.DistinctBy(s => s.NameCpp));
            bigPackage.Classes.AddRange(package.Classes.DistinctBy(s => s.NameCpp));
            bigPackage.Enums.AddRange(package.Enums.DistinctBy(s => s.NameCpp));
            bigPackage.Constants.AddRange(package.Constants.DistinctBy(s => s.Name));
            bigPackage.Defines.AddRange(package.Defines.DistinctBy(s => s.Name));
            bigPackage.Functions.AddRange(package.Functions.DistinctBy(s => s.Name));
            bigPackage.Conditions.AddRange(package.Conditions.DistinctBy(s => s));
            bigPackage.Forwards.AddRange(package.Forwards.DistinctBy(s => s));
            bigPackage.TypeDefs.AddRange(package.TypeDefs.DistinctBy(s => s));
        }

        List<EngineStruct> structs = bigPackage.Structs.DistinctBy(s => s.NameCpp).ToList();
        bigPackage.Structs.Clear();
        bigPackage.Structs.AddRange(structs);

        List<EngineClass> classes = bigPackage.Classes.DistinctBy(s => s.NameCpp).ToList();
        bigPackage.Classes.Clear();
        bigPackage.Classes.AddRange(classes);

        List<EngineEnum> enums = bigPackage.Enums.DistinctBy(s => s.NameCpp).ToList();
        bigPackage.Enums.Clear();
        bigPackage.Enums.AddRange(enums);

        List<EngineConstant> constants = bigPackage.Constants.DistinctBy(s => s.Name).ToList();
        bigPackage.Constants.Clear();
        bigPackage.Constants.AddRange(constants);

        List<EngineDefine> defines = bigPackage.Defines.DistinctBy(s => s.Name).ToList();
        bigPackage.Defines.Clear();
        bigPackage.Defines.AddRange(defines);

        List<EngineFunction> functions = bigPackage.Functions.DistinctBy(s => s.Name).ToList();
        bigPackage.Functions.Clear();
        bigPackage.Functions.AddRange(functions);

        List<string> conditions = bigPackage.Conditions.DistinctBy(s => s).ToList();
        bigPackage.Conditions.Clear();
        bigPackage.Conditions.AddRange(conditions);

        List<string> forwards = bigPackage.Forwards.DistinctBy(s => s).ToList();
        bigPackage.Forwards.Clear();
        bigPackage.Forwards.AddRange(forwards);

        List<string> typeDefs = bigPackage.TypeDefs.DistinctBy(s => s).ToList();
        bigPackage.TypeDefs.Clear();
        bigPackage.TypeDefs.AddRange(typeDefs);

        return bigPackage;
    }

    /// <summary>
    ///     Generate enginePackage files
    /// </summary>
    /// <param name="enginePackage">Package to generate files for</param>
    /// <returns>File name and its content</returns>
    private async ValueTask<Dictionary<string, string>> GeneratePackageFilesAsync(IEnginePackage enginePackage)
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);

#if DEBUG
        //if (enginePackage.Name != "BasicTypes")
        //    return new Dictionary<string, string>();
#endif

        var ret = new Dictionary<string, string>();

        // Make CppPackageModel
        List<CppStruct> structs = GetStructs(enginePackage).ToList();
        structs.AddRange(GetClasses(enginePackage));

        // Make CppPackage
        var cppPackage = new CppPackage
        {
            Name = enginePackage.Name,
            //BeforeNameSpace = $"#ifdef _MSC_VER{Environment.NewLine}\t#pragma pack(push, 0x{SdkFile.GlobalMemberAlignment:X2}){Environment.NewLine}#endif",
            //AfterNameSpace = $"#ifdef _MSC_VER{Environment.NewLine}\t#pragma pack(pop){Environment.NewLine}#endif",
            HeadingComment = [$"Name: {SdkFile.GameName}", $"Version: {SdkFile.GameVersion}"],
            NameSpace = SdkFile.Namespace,
            Pragmas = ["once"],
            Forwards = enginePackage.Forwards,
            TypeDefs = enginePackage.TypeDefs,
            Defines = GetDefines(enginePackage).ToList(),
            Fields = GetFields(enginePackage).ToList(),
            Functions = GetFunctions(enginePackage).ToList(),
            Constants = GetConstants(enginePackage).ToList(),
            Enums = GetEnums(enginePackage).ToList(),
            Structs = structs,
            Conditions = enginePackage.Conditions,
        };

        // Make static fields be pointer to type
        // will point Klass->{StaticField}
        foreach (CppField cppField in cppPackage.Structs.SelectMany(s => s.Fields))
        {
            if (!cppField.Static)
            {
                continue;
            }

            cppField.Type = $"{cppField.Type}*";
        }

        cppPackage.CppIncludes.Add(Options[CppOptions.PrecompileSyntax].Value == "true" ? "\"pch.h\"" : "\"../SDK.h\"");
        PreparePackageModel(cppPackage, enginePackage);

        //// Parameters Files
        //if (!enginePackage.IsPredefined && Options[CppOptions.GenerateParametersFile].Value == "true" && Options[CppOptions.OffsetsOnly].Value != "true")
        //{
        //    string fileName = $"{enginePackage.Name}_Params.h";
        //    IEnumerable<CppStruct> paramStructs = GetFuncParametersStructs(enginePackage);
        //    string paramsFile = MakeFuncParametersFile(cppPackage, paramStructs);
        //
        //    cppPackage.PackageHeaderIncludes.Add($"\"{fileName}\"");
        //    ret.Add(Path.Combine("SDK", fileName), paramsFile);
        //}

        // Generate files
        Dictionary<string, string> cppFiles = _cppProcessor.GenerateFiles(cppPackage)
            .ToDictionary(kv => Path.Combine("SDK", kv.Key), kv => kv.Value);

        foreach ((string fName, string fContent) in cppFiles)
        {
            ret.Add(fName, fContent);
        }

        // Useful for unit tests
        SavedStructs.AddRange(
            structs.Where(cs => !cs.IsClass)
                .SelectMany(cs => enginePackage.Structs.Where(ec => ec.NameCpp == cs.Name))
        );
        SavedClasses.AddRange(
            structs.Where(cs => cs.IsClass)
                .SelectMany(cs => enginePackage.Classes.Where(ec => ec.NameCpp == cs.Name))
        );

        return ret;
    }

    /// <summary>
    ///     Process local files that needed to be included
    /// </summary>
    /// <param name="processPurpose">Process props</param>
    private async ValueTask<Dictionary<string, string>> GenerateIncludesAsync(OutputPurpose processPurpose)
    {
        var ret = new Dictionary<string, string>();
        return ret;

        // Init
        var unitTestCpp = new UnitTest(this);

        if (processPurpose == OutputPurpose.External)
        {
            var mmHeader = new MemManagerHeader(this);
            var mmCpp = new MemManagerCpp(this);

            ValueTask<string> taskMmHeader = mmHeader.ProcessAsync(processPurpose);
            ValueTask<string> taskMmCpp = mmCpp.ProcessAsync(processPurpose);

            ret.Add(mmHeader.FileName, await taskMmHeader.ConfigureAwait(false));
            ret.Add(mmCpp.FileName, await taskMmCpp.ConfigureAwait(false));
        }

        // Process
        ValueTask<string> taskUnitTestCpp = unitTestCpp.ProcessAsync(processPurpose);

        // Wait tasks
        ret.Add(unitTestCpp.FileName, await taskUnitTestCpp.ConfigureAwait(false));

        // PchHeader
        if (Options[CppOptions.PrecompileSyntax].Value == "true")
        {
            var pchHeader = new PchHeader(this);
            ret.Add(pchHeader.FileName, await pchHeader.ProcessAsync(processPurpose).ConfigureAwait(false));
        }

        return ret;
    }

    protected override ValueTask OnInitAsync()
    {
        ArgumentNullException.ThrowIfNull(SdkFile);

        _cppProcessor = new CppProcessor();
        var cppOpts = new CppLangOptions
        {
            NewLine = NewLineType.CRLF,
            PrintSectionName = true,
            InlineCommentPadSize = 56,
            VariableMemberTypePadSize = 60,
            GeneratePackageSyntax = true,
            AddPackageHeaderToCppFile = false,
        };
        _cppProcessor.Init(cppOpts);

        SavedClasses.Clear();
        SavedStructs.Clear();

#if DEBUG
        Options[CppOptions.PrecompileSyntax].SetValue("true");
#endif

        // Make big package
        // Copy all packages to one package
        // That's because il2cpp can't be presented in packages
        // As generic types have heavy dependency cycle
        List<UnityPackage> packsToMerge = SdkFile.Packages.Where(p => !p.IsPredefined)
            .ToList();
        UnityPackage bigPackage = MergePackages(packsToMerge, SdkFile.GameName);

        SdkFile.Packages.RemoveAll(p => !p.IsPredefined);
        SdkFile.Packages.Add(bigPackage);

        // Sort structs in packages
        PackageSorter.SortStructsClassesInPackages(SdkFile.Packages);

        // Add predefined methods
        foreach (IEnginePackage pack in SdkFile.Packages.Where(p => !p.IsPredefined))
        {
            foreach (EngineStruct @struct in pack.Structs)
            {
                AddPredefinedMethodsToStruct(@struct);
            }

            foreach (EngineClass @class in pack.Classes)
            {
                AddPredefinedMethodsToClass(@class);
            }
        }

        return ValueTask.CompletedTask;
    }

    public override async ValueTask SaveAsync(string saveDirPath, OutputPurpose processPurpose)
    {
        var builder = new MyStringBuilder();
        builder.AppendLine($"#pragma once{Environment.NewLine}");
        builder.AppendLine("// --------------------------------------- \\\\");
        builder.AppendLine("//      Sdk Generated By ( CheatGear )     \\\\");
        builder.AppendLine("// --------------------------------------- \\\\");
        builder.AppendLine($"// Name: {SdkFile.GameName.Trim()}, Version: {SdkFile.GameVersion}{Environment.NewLine}");

        builder.AppendLine("#include <set>");
        builder.AppendLine("#include <string>");
        builder.AppendLine("#include <vector>");
        builder.AppendLine("#include <locale>");
        builder.AppendLine("#include <unordered_set>");
        builder.AppendLine("#include <unordered_map>");
        builder.AppendLine("#include <iostream>");
        builder.AppendLine("#include <sstream>");
        builder.AppendLine("#include <cstdint>");
        builder.AppendLine("#include <Windows.h>");

        // Packages generator [ Should be first task ]
        int packCount = 0;
        foreach (UnityPackage pack in SdkFile.Packages)
        {
            foreach ((string fName, string fContent) in await GeneratePackageFilesAsync(pack).ConfigureAwait(false))
            {
                await FileManager.WriteAsync(saveDirPath, fName, fContent).ConfigureAwait(false);
            }

            if (Status?.ProgressbarStatus is not null)
            {
                await Status.ProgressbarStatus.Invoke(
                        "",
                        packCount,
                        SdkFile.Packages.Count - packCount
                    )
                    .ConfigureAwait(false);
            }

            packCount++;
        }

        // Includes
        foreach ((string fName, string fContent) in await GenerateIncludesAsync(processPurpose).ConfigureAwait(false))
        {
            await FileManager.WriteAsync(saveDirPath, fName, fContent).ConfigureAwait(false);

            if (!fName.EndsWith(".cpp") && fName.ToLower() != "pch.h")
            {
                builder.AppendLine($"#include \"{fName.Replace("\\", "/")}\"");
            }
        }

        builder.Append(Environment.NewLine);

        // Package sorter
        if (Status?.TextStatus is not null)
        {
            await Status.TextStatus.Invoke("Sort packages depend on dependencies").ConfigureAwait(false);
        }

        PackageSorterResult<IEnginePackage> sortResult =
            PackageSorter.Sort(SdkFile.Packages.Cast<IEnginePackage>().ToList());
        if (sortResult.CycleList.Count > 0)
        {
            builder.AppendLine("// # Dependency cycle headers");
            builder.AppendLine($"// # (Sorted: {sortResult.SortedList.Count}, Cycle: {sortResult.CycleList.Count})\n");

            foreach ((IEnginePackage package, IEnginePackage dependPackage) in sortResult.CycleList)
            {
                builder.AppendLine($"// {package.Name} <-> {dependPackage.Name}");
                builder.AppendLine($"#include \"SDK/{package.Name}_Package.h\"");
            }

            builder.AppendLine();
            builder.AppendLine();
        }

        foreach (IEnginePackage package in sortResult.SortedList.Where(p => p.IsPredefined))
        {
            builder.AppendLine($"#include \"SDK/{package.Name}_Package.h\"");
        }

        foreach (IEnginePackage package in sortResult.SortedList.Where(p => !p.IsPredefined))
        {
            builder.AppendLine($"#include \"SDK/{package.Name}_Package.h\"");
        }

        await FileManager.WriteAsync(saveDirPath, "SDK.h", builder.ToString()).ConfigureAwait(false);
    }
}
