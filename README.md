# Jolly C# Formatter

A lightweight Windows WPF application for formatting C# source code with practical, configurable formatting rules.

Jolly C# Formatter is designed to produce clean, readable C# without imposing artificial line-length limits or aggressively restructuring code.

## Features

* Format C# source using the Roslyn C# syntax API.
* Side-by-side **Original** and **Reformatted** editors.
* Open `.cs` files from a file dialog.
* Drag and drop `.cs` files directly onto the application.
* Save formatted source back to the current file.
* Save formatted source as a new file.
* Copy formatted source to the clipboard.
* Synchronized vertical scrolling between the original and formatted editors.
* Configurable formatting options.
* Dark-themed WPF interface.
* Native Windows application targeting modern .NET.

## Formatting Options

The formatter currently supports options for:

* Method declarations on one line.
* Constructor declarations on one line.
* Method calls on one line.
* Constructor calls on one line.
* Conditions on one line.
* Record declaration parameters on one line.
* Expanding single-line blocks.
* Spaces or tabs for indentation.
* Blank lines after blocks.
* Blank lines after unbraced control statements.
* Blank lines between type members.

### Statement Formatting

Ordinary simple statements are kept on a single physical line regardless of their length.

For example:

```csharp
throw new NotSupportedException($"Global array initializer for '{global.Name}' at index {i} must currently be a constant integer or floating-point value.");
```

The formatter does not impose an arbitrary 80, 100, 120, or 160 character line limit.

Structured constructs such as `if`, `for`, `while`, `switch`, and blocks retain their multi-line structure.

### String Concatenation

Adjacent constant string and interpolated-string fragments can be merged where it is safe to do so.

For example:

```csharp
throw new NotSupportedException($"Global array initializer for '{global.Name}' " +
                                $"at index {i} must currently be a constant integer " +
                                "or floating-point value.");
```

becomes:

```csharp
throw new NotSupportedException($"Global array initializer for '{global.Name}' at index {i} must currently be a constant integer or floating-point value.");
```

Arithmetic expressions are not treated as string concatenations, so expressions such as:

```csharp
int result = a + b;
```

remain unchanged.

## Example

### Before

```csharp
public void ProcessSomething(
    string name,
    int value,
    bool enabled)
{
    if (enabled &&
        value > 0 &&
        name.Length > 0)
    {
        Console.WriteLine(
            $"Processing {name} with value {value}");
    }
}
```

### After

```csharp
public void ProcessSomething(string name, int value, bool enabled)
{
    if (enabled && value > 0 && name.Length > 0)
    {
        Console.WriteLine($"Processing {name} with value {value}");
    }
}
```

## User Interface

The application provides:

* **Open** — open a C# source file.
* **Format** — format the contents of the original editor.
* **Copy** — copy the formatted source.
* **Save** — save formatted source to the current file.
* **Save As** — save formatted source to a new file.
* **Formatting Options** — configure formatter behaviour.
* Drag-and-drop file loading.

The original and reformatted source are displayed side by side for easy comparison.

## Technology

* **C#**
* **.NET 10**
* **WPF**
* **Microsoft.CodeAnalysis.CSharp**
* **Microsoft.CodeAnalysis.CSharp.Workspaces**
* **Roslyn**

## Requirements

* Windows
* .NET 10 compatible runtime/environment
* Visual Studio 2022 or another IDE capable of building .NET 10 WPF applications

## Building

Clone the repository:

```powershell
git clone <repository-url>
cd JollyCSharpFormatter
```

Build the project:

```powershell
dotnet build
```

Run the application:

```powershell
dotnet run
```

## Project Structure

```text
JollyCSharpFormatter
│
├── App.xaml
├── App.xaml.cs
├── MainWindow.xaml
├── MainWindow.xaml.cs
│
├── Formatting
│   ├── CSharpFormatter.cs
│   └── FormatterOptions.cs
│
└── Controls
    ├── CodeEditor.xaml
    └── CodeEditor.xaml.cs
```

## Design Goals

Jolly C# Formatter is intentionally focused on **readability and predictability**.

The formatter aims to:

* Keep simple statements together.
* Avoid arbitrary line-length wrapping.
* Keep method and constructor signatures readable.
* Keep method and constructor calls readable.
* Keep conditions readable on one line when enabled.
* Preserve structured C# constructs.
* Avoid unnecessary blank lines.
* Keep related declarations together.
* Separate methods clearly.
* Allow the user to choose spaces or tabs.
* Make formatting behaviour configurable rather than enforcing one rigid style.

## Status

Jolly C# Formatter is an actively developed project.

The formatter is currently focused on practical source-code formatting using the Roslyn syntax tree rather than attempting to implement a complete C# parser or compiler.

Additional formatting rules and improvements may be added as development continues.

## License

Add your preferred license here.

For example:

```text
MIT License
```

See the repository's `LICENSE` file for the complete license terms.
