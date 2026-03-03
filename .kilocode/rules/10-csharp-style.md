# C# Style Guide

## Naming Conventions

### Files
- **Classes/Interfaces:** PascalCase (`MapCommand.cs`, `IImageProcessor.cs`)
- **Enums:** PascalCase with singular names (`FaceDetectionModel`)
- **Records:** PascalCase

### Types
- **Classes:** PascalCase (`PlayerMappingService`)
- **Interfaces:** PascalCase with `I` prefix (`IImageProcessor`)
- **Records:** PascalCase
- **Structs:** PascalCase

### Members
- **Methods:** PascalCase (`GetPlayerMappings`)
- **Properties:** PascalCase (`PlayerName`)
- **Fields (private):** `_camelCase` or `camelCase` with `_` prefix
- **Constants:** PascalCase or UPPER_SNAKE_CASE
- **Parameters:** camelCase
- **Local variables:** camelCase

## Code Style

### Language Usage
- Use language keywords (`int`, `string`) not BCL types (`Int32`, `String`)
- Use `var` when type is explicit from right side
- Use `nameof()` instead of magic strings
- Use null-conditional operators (`?.`) and null-coalescing (`??`)
- Prefer string interpolation (`$"Hello {name}"`) over concatenation
- Use pattern matching where appropriate

### Classes
- Use primary constructors when appropriate (C# 12+)
- Make classes `sealed` unless derivation required
- Use `readonly` fields where possible
- Order members: fields → constructors → properties → methods

### Braces
- Use Allman style (each brace on new line)
```csharp
public class PlayerService
{
    public void GetPlayer()
    {
        // code
    }
}
```

### Properties vs Fields
- Use properties for public/protected access
- Use backing fields for lazy loading or when logic needed

### LINQ
- Use LINQ for collection operations
- Prefer method syntax for readability
- Use `AsNoTracking()` for read-only queries

### Async/Await
- Use `async`/`await` for I/O operations
- Use `Task` return types
- Avoid `.Result` or `.Wait()`

## Code Organization

### File Structure
```
Services/
├── IPlayerService.cs      # Interface first
├── PlayerService.cs       # Implementation
Models/
├── Player.cs
Commands/
├── MapCommand.cs
Utils/
├── FileHelper.cs
```

### Using Directives
- Group: System → External → Internal
- Sort alphabetically within groups
- Remove unused imports

## Attributes

### Attribute Ordering
```csharp
[Command(Name = "map", Description = "Map photos to players")]
[Option(Description = "Input CSV path")]
public string InputCsvPath { get; set; }
```

## Error Handling

### Exceptions
- Throw specific exceptions
- Include meaningful messages
- Use `ArgumentNullException` for null params

### Validation
- Validate inputs at method boundaries
- Use guard clauses
```csharp
public void ProcessPlayer(Player player)
{
    ArgumentNullException.ThrowIfNull(player);
    // proceed
}
```

## Performance

### String Operations
- Use `StringBuilder` for multiple concatenations
- Use `string.IsNullOrEmpty()` or `string.IsNullOrWhiteSpace()`

### Collections
- Specify collection types when possible
- Use `IReadOnlyList<T>` for return types when not modifying
- Consider `Span<T>` for performance-critical code
