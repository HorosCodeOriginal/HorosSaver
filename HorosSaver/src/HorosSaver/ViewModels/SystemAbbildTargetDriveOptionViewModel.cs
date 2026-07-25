namespace HorosSaver.ViewModels;

public sealed class SystemAbbildTargetDriveOptionViewModel
{
    public SystemAbbildTargetDriveOptionViewModel(string targetPath, string label, bool isNtfs)
    {
        TargetPath = targetPath;
        Label = label;
        IsNtfs = isNtfs;
    }

    public string TargetPath { get; }
    public string Label { get; }
    public bool IsNtfs { get; }
}
