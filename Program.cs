using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace GradeJournal;

sealed class GradeEntry
{
    public int Id { get; set; }
    public string Subject { get; set; } = "";
    public int Score { get; set; }
    public string? Comment { get; set; }
    public string Date { get; set; } = "";
}

static class GradeStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string DataPath =>
        Path.Combine(Directory.GetCurrentDirectory(), "grades.json");

    public static List<GradeEntry> Load()
    {
        if (!File.Exists(DataPath))
        {
            return [];
        }

        try
        {
            var json = File.ReadAllText(DataPath);
            return JsonSerializer.Deserialize<List<GradeEntry>>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static void Save(List<GradeEntry> grades)
    {
        File.WriteAllText(DataPath, JsonSerializer.Serialize(grades, JsonOptions) + "\n");
    }

    public static int NextId(List<GradeEntry> grades) =>
        grades.Count == 0 ? 1 : grades.Max(g => g.Id) + 1;
}

public static class Program
{
    public static int Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.InputEncoding = System.Text.Encoding.UTF8;

        if (args.Length == 0)
        {
            return RunInteractive();
        }

        return args[0].ToLowerInvariant() switch
        {
            "add" => CmdAdd(args.Skip(1).ToArray()),
            "list" => CmdList(),
            "avg" or "average" => CmdAverage(args.Skip(1).ToArray()),
            "remove" or "rm" => CmdRemove(args.Skip(1).ToArray()),
            "help" or "-h" or "--help" => PrintHelpAndOk(),
            _ => Unknown(args[0]),
        };
    }

    private static int RunInteractive()
    {
        Console.WriteLine("GradeJournal — журнал оценок студента");
        Console.WriteLine($"Файл данных: {GradeStore.DataPath}");
        Console.WriteLine();

        while (true)
        {
            Console.WriteLine("Меню:");
            Console.WriteLine("  1 — Добавить оценку");
            Console.WriteLine("  2 — Список оценок");
            Console.WriteLine("  3 — Средний балл");
            Console.WriteLine("  4 — Удалить оценку");
            Console.WriteLine("  0 — Выход");
            Console.Write("Выбор: ");

            var choice = (Console.ReadLine() ?? "").Trim();
            Console.WriteLine();

            switch (choice)
            {
                case "1":
                    InteractiveAdd();
                    break;
                case "2":
                    CmdList();
                    break;
                case "3":
                    InteractiveAverage();
                    break;
                case "4":
                    InteractiveRemove();
                    break;
                case "0":
                    Console.WriteLine("До свидания.");
                    return 0;
                default:
                    Console.WriteLine("Неизвестный пункт меню. Введите число от 0 до 4.");
                    break;
            }

            Console.WriteLine();
        }
    }

    private static void InteractiveAdd()
    {
        Console.Write("Предмет: ");
        var subject = (Console.ReadLine() ?? "").Trim();
        if (string.IsNullOrWhiteSpace(subject))
        {
            Console.WriteLine("Ошибка: название предмета не может быть пустым.");
            return;
        }

        Console.Write("Оценка (2–5): ");
        var scoreRaw = (Console.ReadLine() ?? "").Trim();
        if (!TryParseScore(scoreRaw, out var score))
        {
            Console.WriteLine("Ошибка: оценка должна быть целым числом от 2 до 5.");
            return;
        }

        Console.Write("Комментарий (необязательно): ");
        var comment = (Console.ReadLine() ?? "").Trim();
        if (comment.Length == 0)
        {
            comment = null!;
        }

        var grades = GradeStore.Load();
        var entry = new GradeEntry
        {
            Id = GradeStore.NextId(grades),
            Subject = subject,
            Score = score,
            Comment = comment,
            Date = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        };
        grades.Add(entry);
        GradeStore.Save(grades);
        Console.WriteLine($"Добавлено: #{entry.Id} {entry.Subject} — {entry.Score} ({entry.Date}).");
    }

    private static void InteractiveAverage()
    {
        Console.Write("Предмет (пусто = все предметы): ");
        var subject = (Console.ReadLine() ?? "").Trim();
        CmdAverage(string.IsNullOrEmpty(subject) ? [] : [subject]);
    }

    private static void InteractiveRemove()
    {
        Console.Write("Номер оценки для удаления: ");
        var raw = (Console.ReadLine() ?? "").Trim();
        if (!int.TryParse(raw, out var id))
        {
            Console.WriteLine("Ошибка: введите целочисленный номер оценки.");
            return;
        }

        CmdRemove([id.ToString(CultureInfo.InvariantCulture)]);
    }

    private static int CmdAdd(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Использование: GradeJournal add <предмет> <оценка 2-5> [комментарий]");
            return 1;
        }

        var subject = args[0].Trim();
        if (string.IsNullOrWhiteSpace(subject))
        {
            Console.WriteLine("Ошибка: название предмета не может быть пустым.");
            return 1;
        }

        if (!TryParseScore(args[1], out var score))
        {
            Console.WriteLine("Ошибка: оценка должна быть целым числом от 2 до 5.");
            return 1;
        }

        var comment = args.Length > 2 ? string.Join(' ', args.Skip(2)).Trim() : null;
        if (string.IsNullOrEmpty(comment))
        {
            comment = null;
        }

        var grades = GradeStore.Load();
        var entry = new GradeEntry
        {
            Id = GradeStore.NextId(grades),
            Subject = subject,
            Score = score,
            Comment = comment,
            Date = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        };
        grades.Add(entry);
        GradeStore.Save(grades);
        Console.WriteLine($"Добавлено: #{entry.Id} {entry.Subject} — {entry.Score} ({entry.Date}).");
        return 0;
    }

    private static int CmdList()
    {
        var grades = GradeStore.Load();
        if (grades.Count == 0)
        {
            Console.WriteLine("Журнал пуст. Добавьте первую оценку через пункт меню «1».");
            return 0;
        }

        Console.WriteLine($"{"№",-4} {"Предмет",-22} {"Оц.",-4} {"Дата",-12} Комментарий");
        Console.WriteLine(new string('-', 70));
        foreach (var g in grades.OrderBy(x => x.Id))
        {
            var comment = string.IsNullOrWhiteSpace(g.Comment) ? "—" : g.Comment!;
            Console.WriteLine($"{g.Id,-4} {Truncate(g.Subject, 22),-22} {g.Score,-4} {g.Date,-12} {comment}");
        }

        return 0;
    }

    private static int CmdAverage(string[] args)
    {
        var grades = GradeStore.Load();
        if (grades.Count == 0)
        {
            Console.WriteLine("Журнал пуст — средний балл посчитать нельзя.");
            return 0;
        }

        if (args.Length == 0)
        {
            PrintAverage("все предметы", grades);
            Console.WriteLine();
            foreach (var group in grades.GroupBy(g => g.Subject, StringComparer.OrdinalIgnoreCase)
                         .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
            {
                PrintAverage(group.Key, group.ToList());
            }

            return 0;
        }

        var subject = string.Join(' ', args).Trim();
        var filtered = grades
            .Where(g => string.Equals(g.Subject, subject, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (filtered.Count == 0)
        {
            Console.WriteLine($"Оценок по предмету «{subject}» не найдено.");
            return 1;
        }

        PrintAverage(subject, filtered);
        return 0;
    }

    private static int CmdRemove(string[] args)
    {
        if (args.Length != 1 || !int.TryParse(args[0], out var id))
        {
            Console.WriteLine("Использование: GradeJournal remove <номер>");
            return 1;
        }

        var grades = GradeStore.Load();
        var entry = grades.FirstOrDefault(g => g.Id == id);
        if (entry is null)
        {
            Console.WriteLine($"Ошибка: оценка с номером {id} не найдена.");
            return 1;
        }

        grades.Remove(entry);
        GradeStore.Save(grades);
        Console.WriteLine($"Удалено: #{entry.Id} {entry.Subject} — {entry.Score}.");
        return 0;
    }

    private static void PrintAverage(string label, List<GradeEntry> grades)
    {
        var avg = grades.Average(g => g.Score);
        Console.WriteLine($"{label}: средний балл {avg:F2} (оценок: {grades.Count})");
    }

    private static bool TryParseScore(string raw, out int score)
    {
        score = 0;
        if (!int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out score))
        {
            return false;
        }

        return score is >= 2 and <= 5;
    }

    private static string Truncate(string value, int max)
    {
        if (value.Length <= max)
        {
            return value;
        }

        return value[..(max - 1)] + "…";
    }

    private static int PrintHelpAndOk()
    {
        Console.WriteLine("""
            GradeJournal — журнал оценок студента (C# / .NET 8)

            Без аргументов — интерактивное меню (удобно для Rider: Run / F5).

            Команды:
              add <предмет> <оценка 2-5> [комментарий]
              list
              avg [предмет]
              remove <номер>
              help
            """);
        return 0;
    }

    private static int Unknown(string cmd)
    {
        Console.WriteLine($"Неизвестная команда: {cmd}. Используйте help.");
        return 1;
    }
}
