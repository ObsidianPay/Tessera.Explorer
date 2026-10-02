using System.Windows;
using System.Windows.Controls;

namespace Tessera.Explorer.Controls;

/// <summary>One operation of the library: title, what it does, its inputs, and the buttons that run it.</summary>
public class OpCard : ContentControl
{
    static OpCard() => DefaultStyleKeyProperty.OverrideMetadata(typeof(OpCard), new FrameworkPropertyMetadata(typeof(OpCard)));

    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string), typeof(OpCard));
    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(nameof(Description), typeof(string), typeof(OpCard));
    public static readonly DependencyProperty SignatureProperty = DependencyProperty.Register(nameof(Signature), typeof(string), typeof(OpCard));
    public static readonly DependencyProperty OneWayProperty = DependencyProperty.Register(nameof(OneWay), typeof(bool), typeof(OpCard));
    public static readonly DependencyProperty ActionsProperty = DependencyProperty.Register(nameof(Actions), typeof(object), typeof(OpCard));

    public string? Title { get => (string?)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string? Description { get => (string?)GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    /// <summary>The managed call this card makes, shown in monospace so the API is learnable from the UI.</summary>
    public string? Signature { get => (string?)GetValue(SignatureProperty); set => SetValue(SignatureProperty, value); }
    public bool OneWay { get => (bool)GetValue(OneWayProperty); set => SetValue(OneWayProperty, value); }
    public object? Actions { get => GetValue(ActionsProperty); set => SetValue(ActionsProperty, value); }
}

/// <summary>A label (and optional hint) above one input.</summary>
public class Field : ContentControl
{
    static Field() => DefaultStyleKeyProperty.OverrideMetadata(typeof(Field), new FrameworkPropertyMetadata(typeof(Field)));

    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(Field));
    public static readonly DependencyProperty HintProperty = DependencyProperty.Register(nameof(Hint), typeof(string), typeof(Field));

    public string? Label { get => (string?)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string? Hint { get => (string?)GetValue(HintProperty); set => SetValue(HintProperty, value); }
}

/// <summary>A ComboBox over an enum. <see cref="Selected{T}"/> reads it back.</summary>
public class EnumBox : ComboBox
{
    private Type? _enumType;

    public Type? EnumType
    {
        get => _enumType;
        set
        {
            _enumType = value;
            if (value is null) return;
            ItemsSource = Enum.GetValues(value);
            SelectedIndex = 0;
        }
    }

    public T Selected<T>() where T : struct, Enum => SelectedItem is T t ? t : default;

    public void Select<T>(T value) where T : struct, Enum => SelectedItem = value;
}
