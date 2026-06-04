using System.Collections.ObjectModel;
using AIM.Core.Memory;
using AIM.Core.Personalities;
using AIM.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AIM.Desktop.Wpf.ViewModels;

public sealed class PersonalityEditorViewModel : ObservableObject
{
    private readonly IPersonalityService _personalityService;
    private readonly IMemoryService _memoryService;
    private readonly IProviderAccountService _providerAccountService;
    private Guid? _personalityId;
    private string _displayName = string.Empty;
    private string _status = string.Empty;
    private string _avatarText = string.Empty;
    private string _avatarImagePath = string.Empty;
    private string _category = "My Contacts";
    private string _systemPrompt = string.Empty;
    private string _modelId = string.Empty;
    private string _newMemoryText = string.Empty;
    private ProviderOptionViewModel? _selectedProvider;
    private MemoryRecordViewModel? _selectedMemory;
    private PersonalityTemplateViewModel? _selectedTemplate;

    public PersonalityEditorViewModel(
        IPersonalityService personalityService,
        IMemoryService memoryService,
        IProviderAccountService providerAccountService)
    {
        _personalityService = personalityService;
        _memoryService = memoryService;
        _providerAccountService = providerAccountService;
        SaveCommand = new AsyncRelayCommand(SaveAsync, CanSave);
        DeleteCommand = new AsyncRelayCommand(DeleteAsync, () => _personalityId is not null);
        AddMemoryCommand = new AsyncRelayCommand(AddMemoryAsync, () => _personalityId is not null && !string.IsNullOrWhiteSpace(NewMemoryText));
        DeleteMemoryCommand = new AsyncRelayCommand(DeleteMemoryAsync, () => SelectedMemory is not null);
        ApplyTemplateCommand = new RelayCommand(ApplySelectedTemplate, () => SelectedTemplate is not null);

        foreach (var template in PersonalityTemplateCatalog.Archetypes)
        {
            Templates.Add(new PersonalityTemplateViewModel(template));
        }
    }

    public ObservableCollection<ProviderOptionViewModel> Providers { get; } = [];

    public ObservableCollection<MemoryRecordViewModel> Memories { get; } = [];

    public ObservableCollection<PersonalityTemplateViewModel> Templates { get; } = [];

    public IAsyncRelayCommand SaveCommand { get; }

    public IAsyncRelayCommand DeleteCommand { get; }

    public IAsyncRelayCommand AddMemoryCommand { get; }

    public IAsyncRelayCommand DeleteMemoryCommand { get; }

    public IRelayCommand ApplyTemplateCommand { get; }

    public bool WasSavedOrDeleted { get; private set; }

    public string DisplayName
    {
        get => _displayName;
        set
        {
            if (SetProperty(ref _displayName, value))
            {
                SaveCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public string AvatarText
    {
        get => _avatarText;
        set => SetProperty(ref _avatarText, value);
    }

    public string AvatarImagePath
    {
        get => _avatarImagePath;
        set => SetProperty(ref _avatarImagePath, value);
    }

    public string Category
    {
        get => _category;
        set => SetProperty(ref _category, string.IsNullOrWhiteSpace(value) ? "My Contacts" : value);
    }

    public string SystemPrompt
    {
        get => _systemPrompt;
        set
        {
            if (SetProperty(ref _systemPrompt, value))
            {
                SaveCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string ModelId
    {
        get => _modelId;
        set
        {
            if (SetProperty(ref _modelId, value))
            {
                SaveCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string NewMemoryText
    {
        get => _newMemoryText;
        set
        {
            if (SetProperty(ref _newMemoryText, value))
            {
                AddMemoryCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public ProviderOptionViewModel? SelectedProvider
    {
        get => _selectedProvider;
        set
        {
            if (SetProperty(ref _selectedProvider, value))
            {
                if (string.IsNullOrWhiteSpace(ModelId))
                {
                    ModelId = value?.DefaultModelId ?? string.Empty;
                }

                SaveCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public MemoryRecordViewModel? SelectedMemory
    {
        get => _selectedMemory;
        set
        {
            if (SetProperty(ref _selectedMemory, value))
            {
                DeleteMemoryCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public PersonalityTemplateViewModel? SelectedTemplate
    {
        get => _selectedTemplate;
        set
        {
            if (SetProperty(ref _selectedTemplate, value))
            {
                ApplyTemplateCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public async Task LoadAsync(Personality? personality, CancellationToken cancellationToken = default)
    {
        var providers = await _providerAccountService.ListAsync(cancellationToken);
        Providers.Clear();

        foreach (var provider in providers)
        {
            Providers.Add(new ProviderOptionViewModel(provider));
        }

        _personalityId = personality?.Id;
        DisplayName = personality?.DisplayName ?? string.Empty;
        Status = personality?.Status ?? string.Empty;
        AvatarText = personality?.AvatarText ?? string.Empty;
        AvatarImagePath = personality?.AvatarImagePath ?? string.Empty;
        Category = personality?.Category ?? "My Contacts";
        SystemPrompt = personality?.SystemPrompt ?? "You are a helpful AI contact.";
        ModelId = personality?.ModelId ?? string.Empty;
        SelectedProvider = Providers.FirstOrDefault(provider => provider.Key == personality?.ProviderKey) ?? Providers.FirstOrDefault();
        SelectedTemplate = Templates.FirstOrDefault();

        await LoadMemoriesAsync(cancellationToken);
        DeleteCommand.NotifyCanExecuteChanged();
        AddMemoryCommand.NotifyCanExecuteChanged();
    }

    private bool CanSave()
    {
        return SelectedProvider is not null &&
            !string.IsNullOrWhiteSpace(DisplayName) &&
            !string.IsNullOrWhiteSpace(SystemPrompt) &&
            !string.IsNullOrWhiteSpace(ModelId);
    }

    private async Task SaveAsync()
    {
        if (SelectedProvider is null)
        {
            return;
        }

        var saved = await _personalityService.SaveAsync(new PersonalityDraft(
            _personalityId,
            DisplayName,
            Status,
            AvatarText,
            SystemPrompt,
            SelectedProvider.Key,
            ModelId,
            AvatarImagePath,
            Category));

        _personalityId = saved.Id;
        WasSavedOrDeleted = true;
        await LoadMemoriesAsync();
        DeleteCommand.NotifyCanExecuteChanged();
        AddMemoryCommand.NotifyCanExecuteChanged();
    }

    private void ApplySelectedTemplate()
    {
        if (SelectedTemplate is null)
        {
            return;
        }

        var template = SelectedTemplate.Template;
        DisplayName = template.DisplayName;
        Status = template.Status;
        AvatarText = template.AvatarText;
        AvatarImagePath = template.AvatarImagePath;
        Category = template.Category;
        SystemPrompt = template.SystemPrompt;
        SelectedProvider = Providers.FirstOrDefault(provider => provider.Key == template.ProviderKey) ?? SelectedProvider;
        ModelId = string.IsNullOrWhiteSpace(template.ModelId)
            ? SelectedProvider?.DefaultModelId ?? ModelId
            : template.ModelId;
    }

    private async Task DeleteAsync()
    {
        if (_personalityId is null)
        {
            return;
        }

        await _personalityService.DeleteAsync(_personalityId.Value);
        WasSavedOrDeleted = true;
        _personalityId = null;
        Memories.Clear();
    }

    private async Task AddMemoryAsync()
    {
        if (_personalityId is null || string.IsNullOrWhiteSpace(NewMemoryText))
        {
            return;
        }

        await _memoryService.RememberAsync(_personalityId.Value, NewMemoryText);
        NewMemoryText = string.Empty;
        await LoadMemoriesAsync();
    }

    private async Task DeleteMemoryAsync()
    {
        if (SelectedMemory is null)
        {
            return;
        }

        await _memoryService.DeleteAsync(SelectedMemory.Id);
        await LoadMemoriesAsync();
    }

    private async Task LoadMemoriesAsync(CancellationToken cancellationToken = default)
    {
        Memories.Clear();

        if (_personalityId is null)
        {
            return;
        }

        var memories = await _memoryService.GetMemoriesAsync(_personalityId.Value, cancellationToken);

        foreach (var memory in memories)
        {
            Memories.Add(new MemoryRecordViewModel(memory));
        }
    }
}
