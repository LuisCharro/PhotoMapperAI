using System;
using System.Reflection;
using PhotoMapperAI.Services.AI;

public class TestPromptBuild
{
    public static void Main()
    {
        var buildMethod = typeof(NameComparisonPromptBuilder).GetMethod("Build",
            BindingFlags.Public | BindingFlags.Static);

        if (buildMethod == null)
        {
            Console.WriteLine("Build method not found!");
            return;
        }

        var prompt = (string)buildMethod.Invoke(null, new object[] { "Fernández", "Fernandes" });
        Console.WriteLine(prompt);
    }
}
