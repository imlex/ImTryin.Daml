using System.ComponentModel.DataAnnotations;

namespace ImTryin.Daml.Dump;

public class DumpOptions
{
    [Required]
    public string OutputFile { get; set; } = null!;

    public bool Force { get; set; }
}