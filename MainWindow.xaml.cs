using JollyCSharpFormatter.Formatting;
using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
namespace JollyCSharpFormatter
{
	public partial class MainWindow : Window
	{
		private string? _currentFilePath;
		private bool _updatingOriginal;
		private ScrollViewer? _originalScrollViewer;
		private ScrollViewer? _formattedScrollViewer;
		private bool _synchronizingScroll;

		public MainWindow()
		{
			InitializeComponent();
		}

        private void Window_StateChanged(object? sender, EventArgs e)
        {
            if (this.WindowState == WindowState.Maximized)
            {
                RestoreButton.Visibility = Visibility.Visible;
                MaximizeButton.Visibility = Visibility.Collapsed;
            }
            else
            {
                MaximizeButton.Visibility = Visibility.Visible;
                RestoreButton.Visibility = Visibility.Collapsed;
            }
        }

        private void CommandBinding_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        private void CommandBinding_Executed_Minimize(object sender, ExecutedRoutedEventArgs e)
        {
            SystemCommands.MinimizeWindow(this);
        }

        private void CommandBinding_Executed_Maximize(object sender, ExecutedRoutedEventArgs e)
        {
            SystemCommands.MaximizeWindow(this);
        }

        private void CommandBinding_Executed_Restore(object sender, ExecutedRoutedEventArgs e)
        {
            SystemCommands.RestoreWindow(this);
        }

        private void CommandBinding_Executed_Close(object sender, ExecutedRoutedEventArgs e)
        {
            SystemCommands.CloseWindow(this);
        }
        
		private void Open_Click(object sender, RoutedEventArgs e)
		{
			var dialog = new OpenFileDialog
			{
				Filter = "C# files (*.cs)|*.cs|All files (*.*)|*.*",
				Title = "Open C# source file"
			};

			if (dialog.ShowDialog() == true)
				LoadFile(dialog.FileName);
		}

        private void Format_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var options = new FormatterOptions
                {
                    SingleLineMethodDeclarations = SingleLineMethodDeclarationsOption.IsChecked == true,
                    SingleLineConstructorDeclarations = SingleLineConstructorDeclarationsOption.IsChecked == true,
                    SingleLineMethodCalls = SingleLineMethodCallsOption.IsChecked == true,
                    SingleLineConstructorCalls = SingleLineConstructorCallsOption.IsChecked == true,
                    SingleLineConditions = SingleLineConditionsOption.IsChecked == true,
                    SingleLineRecordDeclarations = SingleLineRecordDeclarationsOption.IsChecked == true,
                    ExpandBlocks = ExpandBlocksOption.IsChecked == true,
                    UseTabs = TabsOption.IsChecked == true,
                    BlankLineAfterBlocks = BlankLineAfterBlocksOption.IsChecked == true,
                    BlankLineAfterControlStatements = BlankLineAfterControlStatementsOption.IsChecked == true,
                    BlankLineBetweenMembers = BlankLineBetweenMembersOption.IsChecked == true
                };

                FormattedEditor.Text = CSharpFormatter.Format(OriginalEditor.Text, options);
                StatusText.Text = "Formatted";
            }
            catch (Exception ex)
            {
                StatusText.Text = "Formatting failed";
                MessageBox.Show(this, ex.Message, "Jolly C# Formatter", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
		{
			if (string.IsNullOrEmpty(FormattedEditor.Text))
				return;
			Clipboard.SetText(FormattedEditor.Text);
			StatusText.Text = "Formatted source copied to clipboard.";
		}

		private void Save_Click(object sender, RoutedEventArgs e)
		{
			if (string.IsNullOrWhiteSpace(_currentFilePath))
			{
				SaveAs_Click(sender, e);
				return;
			}
			try
			{
				File.WriteAllText(_currentFilePath, FormattedEditor.Text);
				_updatingOriginal = true;
				OriginalEditor.Text = FormattedEditor.Text;
				_updatingOriginal = false;
				StatusText.Text = $"Saved: {Path.GetFileName(_currentFilePath)}";
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, ex.Message, "Save failed", MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}

		private void SaveAs_Click(object sender, RoutedEventArgs e)
		{
			var dialog = new SaveFileDialog
			{
				Filter = "C# files (*.cs)|*.cs|All files (*.*)|*.*",
				FileName = string.IsNullOrWhiteSpace(_currentFilePath) ? "Formatted.cs" : Path.GetFileName(_currentFilePath),
				Title = "Save formatted C# source"
			};

			if (dialog.ShowDialog() != true)
				return;
			try
			{
				File.WriteAllText(dialog.FileName, FormattedEditor.Text);
				_currentFilePath = dialog.FileName;
				_updatingOriginal = true;
				OriginalEditor.Text = FormattedEditor.Text;
				_updatingOriginal = false;
				FileNameText.Text = _currentFilePath;
				StatusText.Text = $"Saved: {Path.GetFileName(_currentFilePath)}";
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, ex.Message, "Save failed", MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}

		private void OriginalEditor_TextChanged(object sender, TextChangedEventArgs e)
		{
			if (!_updatingOriginal)
				StatusText.Text = "Source modified.";
		}

        private void Window_PreviewDragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }

            e.Handled = true;
        }

        private void Window_PreviewDrop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Handled = true;
                return;
            }

            if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
            {
                var file = files.FirstOrDefault(x =>
                    string.Equals(Path.GetExtension(x), ".cs", StringComparison.OrdinalIgnoreCase));

                if (file is not null)
                    LoadFile(file);
            }

            e.Handled = true;
        }

        private void LoadFile(string path)
		{
			try
			{
				_currentFilePath = path;
				_updatingOriginal = true;
				OriginalEditor.Text = File.ReadAllText(path);
				_updatingOriginal = false;
				FileNameText.Text = path;
				FormatSource();
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, ex.Message, "Open failed", MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}

		private void FormatSource()
		{
			try
			{
				FormattedEditor.Text = CSharpFormatter.Format(OriginalEditor.Text);
				StatusText.Text = "Formatting complete.";
			}
			catch (Exception ex)
			{
				FormattedEditor.Text = OriginalEditor.Text;
				StatusText.Text = "Formatting failed.";
				MessageBox.Show(this, ex.Message, "Formatting failed", MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}

		private void Editor_Loaded(object sender, RoutedEventArgs e)
		{
			if (sender == OriginalEditor)
			{
				_originalScrollViewer = FindVisualChild<ScrollViewer>(OriginalEditor);
				if (_originalScrollViewer is not null)
					_originalScrollViewer.ScrollChanged += OriginalScrollViewer_ScrollChanged;
			}

			if (sender == FormattedEditor)
			{
				_formattedScrollViewer = FindVisualChild<ScrollViewer>(FormattedEditor);
				if (_formattedScrollViewer is not null)
					_formattedScrollViewer.ScrollChanged += FormattedScrollViewer_ScrollChanged;
			}
		}

		private void OriginalScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
		{
			if (_synchronizingScroll || _formattedScrollViewer is null)
				return;

			_synchronizingScroll = true;
			_formattedScrollViewer.ScrollToVerticalOffset(e.VerticalOffset);
			_synchronizingScroll = false;
		}

		private void FormattedScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
		{
			if (_synchronizingScroll || _originalScrollViewer is null)
				return;

			_synchronizingScroll = true;
			_originalScrollViewer.ScrollToVerticalOffset(e.VerticalOffset);
			_synchronizingScroll = false;
		}

		private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
		{
			for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
			{
				var child = VisualTreeHelper.GetChild(parent, i);

				if (child is T result)
					return result;

				var descendant = FindVisualChild<T>(child);
				if (descendant is not null)
					return descendant;
			}

			return null;
		}
    }
}


