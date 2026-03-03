using PhotoMapperAI.Services.AI;

var testCases = new[]
{
    (Name1: "Fernández", Name2: "Fernandes", ExpectedMatch: true, Description: "z/s variation"),
    (Name1: "Gonzalez", Name2: "Gonsales", ExpectedMatch: true, Description: "z/s variation"),
    (Name1: "López", Name2: "Lopez", ExpectedMatch: true, Description: "accent removal"),
    (Name1: "Martín", Name2: "Martin", ExpectedMatch: true, Description: "accent removal"),
    (Name1: "Rodríguez Sánchez", Name2: "Rodriguez Sanchez", ExpectedMatch: true, Description: "multiple accents"),
    (Name1: "João Félix", Name2: "Joao Felix", ExpectedMatch: true, Description: "Portuguese accents"),
    (Name1: "Luis García", Name2: "Luis Martínez", ExpectedMatch: false, Description: "different surnames"),
    (Name1: "Messi Lionel", Name2: "Cristiano Ronaldo", ExpectedMatch: false, Description: "completely different"),
    (Name1: "José Antonio", Name2: "Antonio José", ExpectedMatch: true, Description: "name order swap"),
    (Name1: "Müller", Name2: "Mueller", ExpectedMatch: true, Description: "umlaut removal"),
    (Name1: "Østergård", Name2: "Ostergard", ExpectedMatch: true, Description: "Scandinavian chars"),
    (Name1: "Åström", Name2: "Astrom", ExpectedMatch: true, Description: "Scandinavian chars"),
    (Name1: "Alexis Mac Allister", Name2: "Alexis MacAllister", ExpectedMatch: true, Description: "space in compound surname"),
    (Name1: "Cristiano Ronaldo", Name2: "Cristiano Ronado", ExpectedMatch: false, Description: "one letter different"),
    (Name1: "Pedri González", Name2: "Pedro Gonzalez", ExpectedMatch: false, Description: "similar but different names"),
    (Name1: "Ramos Sergio", Name2: "Sergio Ramos", ExpectedMatch: true, Description: "name order swap"),
    (Name1: "Iniesta Andrés", Name2: "Andres Iniesta", ExpectedMatch: true, Description: "name order swap + accent"),
    (Name1: "García Luis", Name2: "Luis Garcia", ExpectedMatch: true, Description: "name order swap + accent"),
    (Name1: "María del Carmen", Name2: "Carmen María", ExpectedMatch: true, Description: "complex name reorder"),
};

Console.WriteLine("Testing Name Matching Improvements");
Console.WriteLine("===================================");
Console.WriteLine();

var preCheckMatches = 0;
var needsAiEvaluation = 0;

// Test prompt building and tokenization
foreach (var test in testCases)
{
    var prompt = NameComparisonPromptBuilder.Build(test.Name1, test.Name2);

    // Extract tokens from prompt
    var tokens1Match = System.Text.RegularExpressions.Regex.Match(prompt, "\"name1_core_tokens\":\\s*\\[(.*?)\\]");
    var tokens2Match = System.Text.RegularExpressions.Regex.Match(prompt, "\"name2_core_tokens\":\\s*\\[(.*?)\\]");

    var tokens1 = new List<string>();
    var tokens2 = new List<string>();

    if (tokens1Match.Success)
    {
        foreach (var token in tokens1Match.Groups[1].Value.Split('"', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = token.Trim();
            if (trimmed != "," && !string.IsNullOrWhiteSpace(trimmed))
                tokens1.Add(trimmed);
        }
    }

    if (tokens2Match.Success)
    {
        foreach (var token in tokens2Match.Groups[1].Value.Split('"', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = token.Trim();
            if (trimmed != "," && !string.IsNullOrWhiteSpace(trimmed))
                tokens2.Add(trimmed);
        }
    }

    // Check if tokens are identical (pre-check should catch this)
    var tokensIdentical = tokens1.Count == tokens2.Count &&
                         tokens1.OrderBy(t => t).SequenceEqual(tokens2.OrderBy(t => t));

    var result = tokensIdentical ? "PRE-CHECK MATCH" : "NEEDS AI EVALUATION";

    if (tokensIdentical)
        preCheckMatches++;
    else
        needsAiEvaluation++;

    var expected = test.ExpectedMatch ? "✓ MATCH" : "✗ NO MATCH";

    Console.WriteLine($"{test.Description}: {expected}");
    Console.WriteLine($"  {test.Name1} vs {test.Name2}");
    Console.WriteLine($"  Tokens 1: [{string.Join(", ", tokens1)}]");
    Console.WriteLine($"  Tokens 2: [{string.Join(", ", tokens2)}]");
    Console.WriteLine($"  Result: {result}");
    Console.WriteLine();
}

Console.WriteLine("===================================");
Console.WriteLine($"Test complete!");
Console.WriteLine($"Pre-check matches: {preCheckMatches}/{testCases.Length}");
Console.WriteLine($"Needs AI evaluation: {needsAiEvaluation}/{testCases.Length}");
