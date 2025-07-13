// See https://aka.ms/new-console-template for more information
Console.WriteLine("Hello, World!");
var a = System.Reflection.Assembly.GetAssembly(typeof(Microsoft.Build.Evaluation.Project));
Console.WriteLine($"Assembly: {a?.FullName} {a?.GetName().Version} {a?.Location}");