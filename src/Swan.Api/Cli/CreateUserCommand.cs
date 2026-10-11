using Microsoft.EntityFrameworkCore;
using Swan.Domain.Identity;
using Swan.Infrastructure.Auth;
using Swan.Infrastructure.Persistence;

namespace Swan.Api.Cli;

/// <summary>
/// Creates a local account from the command line. The password is read with echo off and never
/// appears in arguments, shell history, or logs.
/// </summary>
public static class CreateUserCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        string? email = null, name = null;
        var roles = new List<string>();
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--email" when i + 1 < args.Length: email = args[++i]; break;
                case "--name" when i + 1 < args.Length: name = args[++i]; break;
                case "--role" when i + 1 < args.Length: roles.Add(args[++i]); break;
            }
        }

        if (email is null || name is null || roles.Count == 0)
        {
            Console.Error.WriteLine("Usage: create-user --email <addr> --name <display name> --role <role> [--role <role>...]");
            Console.Error.WriteLine($"Roles: {string.Join(", ", new[] { RoleNames.Writer, RoleNames.Reviewer, RoleNames.Publisher, RoleNames.ClientManager, RoleNames.Administrator })}");
            return 2;
        }

        var password = ReadHidden("Password: ");
        var confirm = ReadHidden("Repeat password: ");
        if (password != confirm)
        {
            Console.Error.WriteLine("Passwords do not match.");
            return 2;
        }

        var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<SwanDbContext>()
            .UseSqlite(ApiDatabase.ConnectionString())
            .Options;
        await using var db = new SwanDbContext(options);
        await db.Database.MigrateAsync();

        try
        {
            var user = await new UserAdmin(db).CreateUserAsync(email, name, password, roles);
            Console.WriteLine($"Created {user.Email} with role(s): {string.Join(", ", roles)}");
            return 0;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static string ReadHidden(string prompt)
    {
        Console.Write(prompt);
        var chars = new List<char>();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter) break;
            if (key.Key == ConsoleKey.Backspace && chars.Count > 0) { chars.RemoveAt(chars.Count - 1); continue; }
            if (!char.IsControl(key.KeyChar)) chars.Add(key.KeyChar);
        }
        Console.WriteLine();
        return new string(chars.ToArray());
    }
}

/// <summary>Database location shared by the API and the admin command.</summary>
public static class ApiDatabase
{
    public static string ConnectionString()
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".swan-data", "swan.db");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return $"Data Source={path}";
    }
}
