using Avalonia.Controls;
using Avalonia.Interactivity;
using System;

namespace EvaluacionesApp.Views.Maintenance;

public partial class CriteriaView : UserControl
{
    public CriteriaView()
    {
        InitializeComponent();
        
        // Setup auto-save on text changes
        this.AttachedToVisualTree += OnAttachedToVisualTree;
    }
    
    private void OnAttachedToVisualTree(object? sender, Avalonia.VisualTreeAttachmentEventArgs e)
    {
        // Add event handlers for auto-save
        if (NameTextBox != null)
        {
            NameTextBox.LostFocus += OnTextBoxLostFocus;
        }
        
        if (WeightTextBox != null)
        {
            WeightTextBox.LostFocus += OnTextBoxLostFocus;
        }
    }
    
    private void OnTextBoxLostFocus(object? sender, RoutedEventArgs e)
    {
        if (DataContext is CriteriaViewModel vm)
        {
            // Trigger save when any textbox loses focus
            vm.SaveCommand();
            Console.WriteLine("[DEBUG] Criteria saved after text change");
        }
    }
}
