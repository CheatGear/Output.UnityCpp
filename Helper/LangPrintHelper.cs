using System.Collections.Generic;
using System.Linq;
using System.Text;
using CG.SDK.Dotnet.Engine.Models;
using LangPrint;
using LangPrint.Cpp;

namespace CG.Output.UnityCpp.Helper;

public static class LangPrintHelper
{
    /// <summary>
    ///     Convert <see cref="EngineDefine" /> to <see cref="CppDefine" />
    /// </summary>
    /// <param name="define">Define to convert</param>
    /// <returns>Converted <see cref="CppConstant" /></returns>
    internal static CppDefine ToCpp(this EngineDefine define)
    {
        return new CppDefine
        {
            Name = define.Name,
            Value = define.Value,
            Conditions = define.Conditions,
            Comments = define.Comments,
            BeforePrint = define.BeforePrint,
            AfterPrint = define.AfterPrint,
        };
    }

    /// <summary>
    ///     Convert <see cref="EngineEnum" /> to <see cref="CppEnum" />
    /// </summary>
    /// <param name="eEnum">Enum to convert</param>
    /// <returns>Converted <see cref="CppStruct" /></returns>
    internal static CppEnum ToCpp(this EngineEnum eEnum)
    {
        return new CppEnum
        {
            Name = eEnum.NameCpp,
            Type = eEnum.Type,
            IsClass = true,
            Values = eEnum.Values.Select(
                    kv => new PackageNameValue { Name = kv.Key, Value = kv.Value }
                )
                .ToList(),
            HexValues = eEnum.HexValues,
            Conditions = eEnum.Conditions,
            Comments = eEnum.Comments,
            BeforePrint = eEnum.BeforePrint,
            AfterPrint = eEnum.AfterPrint,
        }.WithComment([eEnum.FullName]);
    }

    /// <summary>
    ///     Convert <see cref="EngineConstant" /> to <see cref="CppConstant" />
    /// </summary>
    /// <param name="constant">Constant to convert</param>
    /// <returns>Converted <see cref="CppConstant" /></returns>
    internal static CppConstant ToCpp(this EngineConstant constant)
    {
        return new CppConstant
        {
            Name = constant.Name,
            Type = constant.Type,
            Value = constant.Value,
            Conditions = constant.Conditions,
            Comments = constant.Comments,
            BeforePrint = constant.BeforePrint,
            AfterPrint = constant.AfterPrint,
        };
    }

    /// <summary>
    ///     Convert <see cref="EngineField" /> to <see cref="CppField" />
    /// </summary>
    /// <param name="field">Variable to convert</param>
    /// <returns>Converted <see cref="CppField" /></returns>
    internal static CppField ToCpp(this EngineField field)
    {
        var inlineComment = new StringBuilder();
        inlineComment.Append($"0x{field.Offset:X4}(0x{field.Size:X4})");

        if (!string.IsNullOrEmpty(field.Comment))
        {
            inlineComment.Append($" {field.Comment}");
        }

        if (!string.IsNullOrEmpty(field.FlagsString))
        {
            inlineComment.Append($" {field.FlagsString}");
        }

        return new CppField
        {
            Name = field.Name,
            Type = field.Type,
            Value = field.Value,
            ArrayDim = field.ArrayDim,
            Bitfield = field.Bitfield,
            Private = false /*field.Private*/,
            Static = field.IsStatic,
            Const = field.IsConst,
            Constexpr = field.IsConstexpr,
            Friend = field.IsFriend,
            Extern = field.IsExtern,
            Union = field.IsUnion,
            ForceUnion = field.ForceUnion,
            InlineComment = field.Comment,
            Conditions = field.Conditions,
            Comments = field.Comments,
            BeforePrint = field.BeforePrint,
            AfterPrint = field.AfterPrint,
        }.WithInlineComment(inlineComment.ToString());
    }

    /// <summary>
    ///     Convert <see cref="EngineParameter" /> to <see cref="CppParameter" />
    /// </summary>
    /// <param name="param">Parameter to convert</param>
    /// <returns>Converted <see cref="CppParameter" /></returns>
    internal static CppParameter ToCpp(this EngineParameter param)
    {
        return new CppParameter
        {
            Name = param.Name,
            Type =
                (param.IsReference ? "const " : "") + param.Type + (param.IsReference ? "&" : param.IsOut ? "*" : ""),
            Conditions = param.Conditions,
            Comments = param.Comments,
            BeforePrint = param.BeforePrint,
            AfterPrint = param.AfterPrint,
        };
    }

    /// <summary>
    ///     Convert <see cref="EngineFunction" /> to <see cref="CppFunction" />
    /// </summary>
    /// <param name="func">Function to convert</param>
    /// <returns>Converted <see cref="CppStruct" /></returns>
    internal static CppFunction ToCpp(this EngineFunction func)
    {
        List<EngineParameter> @params = func.Parameters
            .Where(p => !p.IsReturn && !(p.Name.StartsWith("UnknownData_") && p.Type == "unsigned char"))
            .ToList();

        List<string> comments;
        if (!func.Comments.IsEmpty())
        {
            comments = func.Comments;
        }
        else
        {
            comments =
            [
                "Function:",
                $"\t\tRVA    -> 0x{func.Rva:X8}",
                $"\t\tName   -> {func.FullName}",
                $"\t\tFlags  -> ({func.FlagsString})",
            ];

            if (@params.Count > 0)
            {
                comments.Add("Parameters:");
            }

            foreach (EngineParameter param in @params)
            {
                string comment = string.IsNullOrWhiteSpace(param.FlagsString)
                    ? $"\t\t{param.Type,-50} {param.Name}"
                    : $"\t\t{param.Type,-50} {param.Name,-58} ({param.FlagsString})";

                comments.Add(comment);
            }
        }

        bool isStatic = func.IsStatic;
        if (func.IsStatic && !func.IsPredefined && func.Name.StartsWith("STATIC_"))
        {
            isStatic = false;
        }

        return new CppFunction
        {
            Name = func.Name,
            Type = func.Parameters.First(p => p.IsReturn).Type,
            TemplateParams = func.TemplateParams,
            Params = @params.Select(ep => ep.ToCpp()).ToList(),
            Body = func.Body,
            Private = false /*func.Private*/,
            Static = isStatic,
            Const = func.IsConst,
            Friend = func.IsFriend,
            Inline = func.IsInline,
            Conditions = func.Conditions,
            Comments = func.Comments,
            BeforePrint = func.BeforePrint,
            AfterPrint = func.AfterPrint,
        }.WithComment(comments);
    }

    /// <summary>
    ///     Convert <see cref="EngineStruct" /> to <see cref="CppStruct" />
    /// </summary>
    /// <param name="struct">Struct to convert</param>
    /// <returns>Converted <see cref="CppStruct" /></returns>
    internal static CppStruct ToCpp(this EngineStruct @struct)
    {
        string sizeInfo = @struct.InheritedSize > 0
            ? $"Size -> 0x{@struct.Size - @struct.InheritedSize:X4} (FullSize[0x{@struct.Size:X4}] - InheritedSize[0x{@struct.InheritedSize:X4}])"
            : $"Size -> 0x{@struct.Size:X4}";

        var comments = new List<string> { @struct.FullName, sizeInfo };
        comments.AddRange(@struct.Comments);

        return new CppStruct
        {
            Name = @struct.NameCpp,
            Alignas = @struct.Alignas,
            IsClass = false,
            Supers = @struct.Supers.Select(kv => kv.Value).ToList(),
            Fields = @struct.Fields.Select(ev => ev.ToCpp()).ToList(),
            Methods = @struct.Methods.Select(em => em.ToCpp()).ToList(),
            IsUnion = @struct.IsUnion,
            TemplateParams = @struct.TemplateParams,
            Friends = @struct.Friends,
            Conditions = @struct.Conditions,
            Comments = comments,
            BeforePrint = @struct.BeforePrint,
            AfterPrint = @struct.AfterPrint,
        };
    }

    /// <summary>
    ///     Convert <see cref="EngineClass" /> to <see cref="CppStruct" />
    /// </summary>
    /// <param name="class">Class to convert</param>
    /// <returns>Converted <see cref="CppStruct" /></returns>
    internal static CppStruct ToCpp(this EngineClass @class)
    {
        CppStruct ret = ((EngineStruct)@class).ToCpp();
        ret.IsClass = true;

        return ret;
    }

    internal static T WithComment<T>(this T cppItem, List<string> comments) where T : PackageItemBase
    {
        cppItem.Comments = comments;
        return cppItem;
    }

    internal static T WithInlineComment<T>(this T cppItem, string inlineComment) where T : PackageItemBase
    {
        cppItem.InlineComment = inlineComment;
        return cppItem;
    }

    internal static T WithCondition<T>(this T cppItem, List<string> conditions) where T : PackageItemBase
    {
        cppItem.Conditions = conditions;
        return cppItem;
    }
}
