using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Reclaim.Ui;

public static class TextBoxBehavior
{
    public static readonly DependencyProperty CommitOnEnterProperty = DependencyProperty.RegisterAttached(
        "CommitOnEnter", typeof(bool), typeof(TextBoxBehavior), new PropertyMetadata(false, OnCommitOnEnterChanged));

    public static bool GetCommitOnEnter(DependencyObject element) => (bool)element.GetValue(CommitOnEnterProperty);

    public static void SetCommitOnEnter(DependencyObject element, bool value) => element.SetValue(CommitOnEnterProperty, value);

    static void OnCommitOnEnterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox box || e.NewValue is not true)
            return;

        box.KeyDown += OnKeyDown;
        box.LostKeyboardFocus += (_, _) => Commit(box);
        box.GotKeyboardFocus += (_, _) => box.SelectAll();
        box.PreviewMouseLeftButtonDown += OnPreviewMouseDown;
    }

    static void OnKeyDown(object sender, KeyEventArgs e)
    {
        var box = (TextBox)sender;
        if (e.Key == Key.Enter)
        {
            Commit(box);
            box.SelectAll();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            box.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
            Keyboard.ClearFocus();
            e.Handled = true;
        }
    }

    static void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        var box = (TextBox)sender;
        if (box.IsKeyboardFocusWithin)
            return;
        box.Focus();
        e.Handled = true;
    }

    static void Commit(TextBox box)
    {
        var binding = box.GetBindingExpression(TextBox.TextProperty);
        if (binding is null)
            return;
        binding.UpdateSource();
        binding.UpdateTarget();
    }
}
