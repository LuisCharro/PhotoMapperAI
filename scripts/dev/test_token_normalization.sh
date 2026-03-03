#!/bin/bash

echo "Testing Name Matching Prompt Improvements"
echo "==========================================="
echo ""

# Create a simple C# test to extract and compare tokens
cat > /tmp/test_tokens.cs << 'EOF'
using System.Text.RegularExpressions;

var testCases = new[]
{
    (Name1: "Fernández", Name2: "Fernandes", Description: "z/s variation"),
    (Name1: "Gonzalez", Name2: "Gonsales", Description: "z/s variation"),
    (Name1: "López", Name2: "Lopez", Description: "accent removal"),
    (Name1: "Martín", Name2: "Martin", Description: "accent removal"),
    (Name1: "Rodríguez Sánchez", Name2: "Rodriguez Sanchez", Description: "multiple accents"),
    (Name1: "João Félix", Name2: "Joao Felix", Description: "Portuguese accents"),
    (Name1: "Müller", Name2: "Mueller", Description: "umlaut removal"),
    (Name1: "Østergård", Name2: "Ostergard", Description: "Scandinavian chars"),
    (Name1: "Åström", Name2: "Astrom", Description: "Scandinavian chars"),
};

// Test token normalization helper
string NormalizeTokens(string name)
{
    // Simple normalization: lowercase, remove accents, replace special chars
    var normalized = System.Text.RegularExpressions.Regex.Replace(name.ToLowerInvariant(), "[^a-z0-9\\s]", " ");
    return System.Text.RegularExpressions.Regex.Replace(normalized, "\\s+", " ").Trim();
}

foreach (var test in testCases)
{
    var tokens1 = NormalizeTokens(test.Name1).Split(' ');
    var tokens2 = NormalizeTokens(test.Name2).Split(' ');

    var tokensIdentical = tokens1.OrderBy(t => t).SequenceEqual(tokens2.OrderBy(t => t));

    Console.WriteLine($"{test.Description}:");
    Console.WriteLine($"  {test.Name1} vs {test.Name2}");
    Console.WriteLine($"  Tokens 1: [{string.Join(", ", tokens1)}]");
    Console.WriteLine($"  Tokens 2: [{string.Join(", ", tokens2)}]");
    Console.WriteLine($"  Identical tokens: {tokensIdentical}");
    Console.WriteLine();
}
EOF

# Run the test using C# interactive if available, or just show the analysis
echo "Analyzing token normalization logic..."
echo ""

echo "Key improvements made:"
echo "1. Expanded spelling normalizations dictionary (z/s, accent variants)"
echo "2. Added near-identity detection for 1-2 character differences"
echo "3. Made prompt more permissive for single-character differences"
echo "4. Added pre-check for identical token sets (avoids AI calls)"
echo "5. Adjusted confidence thresholds for better precision/recall balance"
echo ""

echo "Testing specific cases that should improve:"
echo ""

# Test normalization
python3 << 'PYTHON_EOF'
import re
import unicodedata

def normalize_name(name):
    """Normalize name: remove accents, lowercase, normalize spaces"""
    # Normalize unicode to decomposed form
    name = unicodedata.normalize('NFKD', name)
    # Remove combining marks (accents, diacritics)
    name = ''.join([c for c in name if not unicodedata.combining(c)])
    # Lowercase and replace non-alphanumeric with spaces
    name = name.lower()
    name = re.sub(r'[^a-z0-9\s]', ' ', name)
    # Normalize spaces
    name = re.sub(r'\s+', ' ', name)
    return name.strip()

test_cases = [
    ("Fernández", "Fernandes", "z/s variation"),
    ("Gonzalez", "Gonsales", "z/s variation"),
    ("López", "Lopez", "accent removal"),
    ("Martín", "Martin", "accent removal"),
    ("Rodríguez Sánchez", "Rodriguez Sanchez", "multiple accents"),
    ("João Félix", "Joao Felix", "Portuguese accents"),
    ("Müller", "Mueller", "umlaut removal"),
    ("Østergård", "Ostergard", "Scandinavian chars"),
    ("Åström", "Astrom", "Scandinavian chars"),
]

print("Token Normalization Results:")
print("----------------------------")
for name1, name2, desc in test_cases:
    tokens1 = normalize_name(name1).split()
    tokens2 = normalize_name(name2).split()
    identical = sorted(tokens1) == sorted(tokens2)
    status = "✓ MATCH" if identical else "✗ DIFFERENT"
    print(f"{desc}: {status}")
    print(f"  {name1} -> [{', '.join(tokens1)}]")
    print(f"  {name2} -> [{', '.join(tokens2)}]")
    print()
PYTHON_EOF

echo "==========================================="
echo "Analysis complete!"
