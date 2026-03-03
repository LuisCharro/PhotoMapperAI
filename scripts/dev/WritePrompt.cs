using System;
using System.IO;
using System.Reflection;
using PhotoMapperAI.Services.AI;

public class WritePrompt
{
    public static void Main()
    {
        var buildMethod = typeof(NameComparisonPromptBuilder).GetMethod("Build",
            BindingFlags.Public | BindingFlags.Static);

        var prompt = (string)buildMethod?.Invoke(null, new object[] { "Fernández", "Fernandes" });
        File.WriteAllText("/tmp/prompt_debug.txt", prompt ?? "No prompt generated");
        Console.WriteLine("Prompt written to /tmp/prompt_debug.txt");
    }
}
