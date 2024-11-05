using System;
using System.Reflection;

public static class Logger
{
    public static void LogPublicFields(object obj)
    {
        if (obj == null)
        {
            Console.WriteLine("Object is null.");
            return;
        }

        Console.WriteLine($"Logging public fields of {obj.GetType().Name}:");
        foreach (var field in obj.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public))
        {
            var value = field.GetValue(obj);
            Console.WriteLine($"{field.Name} = {value}");
        }

        //Console.WriteLine($"Public methods of {obj.GetType().Name}:");
        //foreach (var method in obj.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public))
        //{
        //    Console.WriteLine($"METHOD: {method.Name}");
        //}
    }
}