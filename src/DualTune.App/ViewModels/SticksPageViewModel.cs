namespace DualTune.App.ViewModels;

public enum StickSide { Left, Right }

/// <summary>Onglet Sticks : un seul stick affiché à la fois, choisi par GAUCHE / DROIT.</summary>
public sealed class SticksPageViewModel : ViewModelBase
{
    private readonly StickViewModel _left;
    private readonly StickViewModel _right;
    private StickSide _side = StickSide.Left;

    public SticksPageViewModel(StickViewModel left, StickViewModel right)
    {
        _left = left;
        _right = right;
    }

    public StickSide SelectedSide
    {
        get => _side;
        set
        {
            if (!Set(ref _side, value)) return;
            Raise(nameof(Current));
            Raise(nameof(IsLeft));
            Raise(nameof(IsRight));
        }
    }

    public bool IsLeft { get => _side == StickSide.Left; set { if (value) SelectedSide = StickSide.Left; } }
    public bool IsRight { get => _side == StickSide.Right; set { if (value) SelectedSide = StickSide.Right; } }

    public StickViewModel Current => _side == StickSide.Left ? _left : _right;
}
