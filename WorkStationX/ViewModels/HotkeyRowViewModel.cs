using CommunityToolkit.Mvvm.ComponentModel;
using WorkStationX.Services;

namespace WorkStationX.ViewModels;

public partial class HotkeyRowViewModel : ObservableObject
{
    [ObservableProperty]
    private HotkeyBinding _binding;

    [ObservableProperty]
    private bool _isCapturing;

    [ObservableProperty]
    private bool _hasConflict;

    public HotkeyRowViewModel(HotkeyBinding binding)
    {
        _binding = binding;
        Description = HotkeyDefaults.Describe(binding.Action);
    }

    public HotkeyAction Action => Binding.Action;

    public string Description { get; }

    public string Display => IsCapturing ? "Press keys…" : Binding.Display;

    partial void OnBindingChanged(HotkeyBinding value) => OnPropertyChanged(nameof(Display));

    partial void OnIsCapturingChanged(bool value) => OnPropertyChanged(nameof(Display));
}
