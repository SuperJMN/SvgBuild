using CSharpFunctionalExtensions;
using Microsoft.Build.Framework;

namespace SvgBuild.Tasks;

public class ConvertSvg : Microsoft.Build.Utilities.Task
{
    [Required]
    public string InputPath { get; set; } = string.Empty;

    [Required]
    public string OutputFormat { get; set; } = string.Empty;

    [Required]
    public string OutputPath { get; set; } = string.Empty;

    public override bool Execute()
    {
        var result = Result.Success(new ConversionRequest(InputPath, OutputFormat, OutputPath))
            .Map(static request => request.Normalize())
            .Bind(static request => request.Validate())
            .Bind(SvgConverter.Convert);

        if (result.IsFailure)
        {
            LogConversionError(result.Error);
        }
        else
        {
            Log.LogMessage(MessageImportance.High, "Converted '{0}' to '{1}'", InputPath, OutputPath);
        }

        return result.IsSuccess;
    }

    void LogConversionError(string error) => Log.LogError("{0}", error);

    internal readonly record struct ConversionRequest(string Input, string Format, string Output)
    {
        public ConversionRequest Normalize() => new(
            Path.GetFullPath(Input),
            Format.Trim().ToUpperInvariant(),
            Path.GetFullPath(Output));

        public Result<ConversionRequest> Validate()
        {
            if (!File.Exists(Input))
            {
                return Result.Failure<ConversionRequest>($"Input file '{Input}' does not exist.");
            }

            if (Format is not "PNG" and not "ICO")
            {
                return Result.Failure<ConversionRequest>($"Output format '{Format}' is not supported. Use 'png' or 'ico'.");
            }

            var directory = Path.GetDirectoryName(Output);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            return Result.Success(this);
        }
    }
}
