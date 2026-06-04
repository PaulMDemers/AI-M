using AIM.Core.Memory;

namespace AIM.Desktop.Wpf.ViewModels;

public sealed class MemoryRecordViewModel
{
    public MemoryRecordViewModel(MemoryRecord memory)
    {
        Memory = memory;
    }

    public MemoryRecord Memory { get; }

    public Guid Id => Memory.Id;

    public string Content => Memory.Content;

    public string CreatedAt => Memory.CreatedAt.ToString("g");
}
