using ManageEmployee.Context;
using ManageEmployee.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
using System.Text.RegularExpressions;

namespace ManageEmployee;

internal class Program
{
    private static readonly Regex SsnRegex = new(@"^\d{3}-\d{2}-\d{4}$", RegexOptions.Compiled);
    private static readonly Regex PhoneRegex = new(@"^\(\d{3}\) \d{3}-\d{4}$", RegexOptions.Compiled);
    private static readonly Regex StateRegex = new(@"^[A-Z]{2}$", RegexOptions.Compiled);
    private static readonly Regex ZipRegex = new(@"^\d{5}$", RegexOptions.Compiled);

    private static async Task Main(string[] args)
    {
        try
        {
            ServiceCollection services = new();
            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlite("Data Source=employee.db"));

            ServiceProvider serviceProvider = services.BuildServiceProvider();

            using IServiceScope scope = serviceProvider.CreateScope();
            AppDbContext context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            await context.Database.EnsureCreatedAsync();
            await InitializeData(context);

            if (args?.Length == 0)
            {
                Console.WriteLine("Usage: ManageEmployee.exe [-list | -titles | -list \"search_term\" | -add]");
                return;
            }

            string command = args?[0]?.ToLower()?.Trim() ?? "";

            switch (command)
            {
                case "-list":
                    if (args.Length > 1 && !string.IsNullOrWhiteSpace(args[1]))
                    {
                        string searchTerm = args[1].Trim();
                        if (searchTerm.Length > 50) // Prevent excessively long searches
                        {
                            Console.WriteLine("Search term too long");
                            return;
                        }

                        if (IsTitleSearch(searchTerm))
                            await ListEmployeesByTitle(context, searchTerm);
                        else
                            await ListEmployeesByName(context, searchTerm);
                    }
                    else
                        await ListAllEmployees(context);
                    break;

                case "-titles":
                    await ListTitles(context);
                    break;

                case "-add":
                    await AddEmployee(context);
                    break;

                default:
                    Console.WriteLine("Invalid command. Use -list, -titles, or -add");
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Application error: {ex.GetType().Name}");
            Environment.Exit(1);
        }
    }

    private static async Task InitializeData(AppDbContext context)
    {
        try
        {
            SQLitePCL.Batteries.Init();

            if (!context.Employees.Any())
            {
                Random random = new();
                string[] firstNames = [
                    "John", "Jane", "Michael", "Sarah", "David", "Emma",
                    "Daniel", "Olivia", "James", "Ava", "Robert", "Emily",
                    "William", "Isabella", "Joseph", "Mia", "Charles", "Amelia",
                    "Thomas", "Harper", "Matthew", "Evelyn", "Anthony", "Abigail",
                    "Christopher", "Ella", "Joshua", "Elizabeth", "Andrew", "Sofia",
                    "Ryan", "Madison", "Benjamin", "Scarlett", "Samuel", "Victoria",
                    "Jacob", "Aria", "Nathan", "Grace", "Logan", "Chloe"
                ];

                string[] lastNames = [
                    "Smith", "Johnson", "Williams", "Brown", "Jones",
                    "Garcia", "Miller", "Davis", "Rodriguez", "Martinez",
                    "Hernandez", "Lopez", "Gonzalez", "Wilson", "Anderson",
                    "Thomas", "Taylor", "Moore", "Jackson", "Martin",
                    "Lee", "Perez", "Thompson", "White", "Harris",
                    "Sanchez", "Clark", "Ramirez", "Lewis", "Robinson",
                    "Walker", "Young", "Allen", "King", "Wright",
                    "Scott", "Torres", "Nguyen", "Hill", "Flores",
                    "Green", "Adams"
                ];

                string[] titles = ["Developer", "Senior Developer", "Manager", "Analyst", "Engineer"];
                string[] states = ["CA", "NY", "TX", "FL", "IL"];
                string[] cities = ["Los Angeles", "New York", "Houston", "Miami", "Chicago"];

                for (int i = 0; i < 100; i++)
                {
                    Employee employee = new()
                    {
                        Name = $"{firstNames[random.Next(firstNames.Length)]} {lastNames[random.Next(lastNames.Length)]}",
                        SSN = GenerateSSN(random),
                        DOB = DateTime.Today.AddYears(-random.Next(22, 65)),
                        Address = $"{random.Next(100, 9999)} Main St",
                        City = cities[random.Next(cities.Length)],
                        State = states[random.Next(states.Length)],
                        Zip = $"{random.Next(10000, 99999)}",
                        Phone = $"({random.Next(200, 999)}) {random.Next(100, 999)}-{random.Next(1000, 9999)}",
                        JoinDate = DateTime.Today.AddYears(-random.Next(1, 20)),
                        ExitDate = random.Next(0, 10) == 0 ? DateTime.Today.AddDays(-random.Next(1, 365)) : null
                    };

                    context.Employees.Add(employee);
                    await context.SaveChangesAsync();

                    context.EmployeeSalaries.Add(new EmployeeSalary
                    {
                        EmployeeId = employee.Id,
                        FromDate = employee.JoinDate,
                        ToDate = employee.ExitDate,
                        Title = titles[random.Next(titles.Length)],
                        Salary = random.Next(50000, 150000) / 100m * 100
                    });
                }
                await context.SaveChangesAsync();
            }
        }
        catch (Exception)
        {
            throw new InvalidOperationException("Failed to initialize data");
        }
    }

    private static string GenerateSSN(Random random) =>
        $"{random.Next(100, 999)}-{random.Next(10, 99)}-{random.Next(1000, 9999)}";

    private static bool IsTitleSearch(string term)
    {
        if (string.IsNullOrWhiteSpace(term)) return false;
        string[] titles = ["developer", "manager", "analyst", "engineer"];
        return titles.Any(t => t.Contains(term.ToLower(), StringComparison.OrdinalIgnoreCase));
    }

    private static async Task ListAllEmployees(AppDbContext context)
    {
        try
        {
            var employees = await context.Employees
                .Join(context.EmployeeSalaries,
                    e => e.Id,
                    es => es.EmployeeId,
                    (e, es) => new { e, es })
                .Where(x => x.es.ToDate == null || x.es.ToDate > DateTime.Today)
                .Take(1000) // Limit results to prevent memory issues
                .Select(x => new
                {
                    x.e.Name,
                    x.e.SSN,
                    x.e.DOB,
                    x.e.Address,
                    x.e.City,
                    x.e.State,
                    x.e.Zip,
                    x.e.Phone,
                    x.e.JoinDate,
                    x.e.ExitDate,
                    x.es.Title,
                    x.es.Salary
                })
                .ToListAsync();

            CultureInfo ci = new("en-US"); // Use consistent culture

            Console.WriteLine(new string('-', 160));
            Console.WriteLine(
                $"{"Name",-20} {"SSN",-12} {"DOB",-12} {"Address",-15} {"City",-15} {"State",-6} {"Zip",-6} {"Phone",-15} {"Join",-12} {"Exit",-12} {"Title",-15} {"Salary",12}");
            Console.WriteLine(new string('-', 160));

            foreach (var emp in employees)
            {
                Console.WriteLine(
                    $"{TruncateString(emp.Name, 20),-20} {emp.SSN,-12} {emp.DOB:MM/dd/yyyy}  {TruncateString(emp.Address, 15),-15} {TruncateString(emp.City, 15),-15} {emp.State,-6} {emp.Zip,-6} {emp.Phone,-15} {emp.JoinDate:MM/dd/yyyy} " +
                    $"{emp.ExitDate?.ToString("MM/dd/yyyy") ?? "Active",-12} {TruncateString(emp.Title, 15),-15} {emp.Salary.ToString("C", ci),12}");
            }
            Console.WriteLine(new string('-', 160));
        }
        catch (Exception)
        {
            Console.WriteLine("Error retrieving employee data");
        }
    }

    private static async Task ListEmployeesByName(AppDbContext context, string name)
    {
        try
        {
            var employees = await context.Employees
                .Join(context.EmployeeSalaries,
                    e => e.Id,
                    es => es.EmployeeId,
                    (e, es) => new { e, es })
                .Where(x => EF.Functions.Like(x.e.Name.ToLower(), $"%{name.ToLower()}%") &&
                           (x.es.ToDate == null || x.es.ToDate > DateTime.Today))
                .Take(100) // Limit results
                .Select(x => new
                {
                    x.e.Name,
                    x.es.Title,
                    x.es.Salary
                })
                .ToListAsync();

            if (employees.Count == 0)
            {
                Console.WriteLine("No employees found");
                return;
            }

            foreach (var emp in employees)
            {
                Console.WriteLine($"Name: {emp.Name}, Title: {emp.Title}, Salary: {emp.Salary:C}");
            }
        }
        catch (Exception)
        {
            Console.WriteLine("Error searching employees");
        }
    }

    private static async Task ListEmployeesByTitle(AppDbContext context, string title)
    {
        try
        {
            var employees = await context.Employees
                .Join(context.EmployeeSalaries,
                    e => e.Id,
                    es => es.EmployeeId,
                    (e, es) => new { e, es })
                .Where(x => x.es.Title.ToLower() == title.ToLower() &&
                           (x.es.ToDate == null || x.es.ToDate > DateTime.Today))
                .Take(100) // Limit results
                .Select(x => new
                {
                    x.e.Name,
                    x.es.Title,
                    x.es.Salary
                })
                .ToListAsync();

            if (employees.Count == 0)
            {
                Console.WriteLine("No employees found with that title");
                return;
            }

            foreach (var emp in employees)
            {
                Console.WriteLine($"Name: {emp.Name}, Title: {emp.Title}, Salary: {emp.Salary:C}");
            }
        }
        catch (Exception)
        {
            Console.WriteLine("Error searching by title");
        }
    }

    private static async Task ListTitles(AppDbContext context)
    {
        try
        {
            var titles = await context.EmployeeSalaries
                .Where(es => es.ToDate == null || es.ToDate > DateTime.Today)
                .GroupBy(es => es.Title)
                .Select(g => new
                {
                    Title = g.Key,
                    MinSalary = g.Min(es => (double)es.Salary),
                    MaxSalary = g.Max(es => (double)es.Salary)
                })
                .ToListAsync();

            if (titles.Count == 0)
            {
                Console.WriteLine("No active titles found");
                return;
            }

            foreach (var title in titles)
            {
                Console.WriteLine($"Title: {title.Title}, Min Salary: {title.MinSalary:C}, Max Salary: {title.MaxSalary:C}");
            }
        }
        catch (Exception)
        {
            Console.WriteLine("Error retrieving title data");
        }
    }

    private static async Task AddEmployee(AppDbContext context)
    {
        try
        {
            string name = GetValidatedInput("Enter Name: ", ValidateName, 100);
            string ssn = GetValidatedInput("Enter SSN (###-##-####): ", ValidateSSN, 11);

            // Check for duplicate SSN
            if (await context.Employees.AnyAsync(e => e.SSN == ssn))
            {
                Console.WriteLine("Error: SSN already exists");
                return;
            }

            DateTime dob = GetValidatedDate("Enter DOB (MM/dd/yyyy): ");
            if (!ValidateAge(dob))
            {
                Console.WriteLine("Error: Employee must be between 22 and 64 years old");
                return;
            }

            string address = GetValidatedInput("Enter Address: ", ValidateAddress, 100);
            string city = GetValidatedInput("Enter City: ", ValidateCity, 50);
            string state = GetValidatedInput("Enter State (2 letters): ", ValidateState, 2);
            string zip = GetValidatedInput("Enter Zip: ", ValidateZip, 5);
            string phone = GetValidatedInput("Enter Phone (###) ###-####: ", ValidatePhone, 14);

            DateTime joinDate = GetValidatedDate("Enter Join Date (MM/dd/yyyy): ");
            if (joinDate > DateTime.Today)
            {
                Console.WriteLine("Error: Join date cannot be in the future");
                return;
            }

            string title = GetValidatedInput("Enter Title: ", ValidateTitle, 50);
            decimal salary = GetValidatedSalary("Enter Salary: ");

            Employee employee = new()
            {
                Name = name,
                SSN = ssn,
                DOB = dob,
                Address = address,
                City = city,
                State = state.ToUpper(),
                Zip = zip,
                Phone = phone,
                JoinDate = joinDate
            };

            context.Employees.Add(employee);
            await context.SaveChangesAsync();

            context.EmployeeSalaries.Add(new EmployeeSalary
            {
                EmployeeId = employee.Id,
                FromDate = joinDate,
                Title = title,
                Salary = salary
            });

            await context.SaveChangesAsync();
            Console.WriteLine("Employee added successfully!");
        }
        catch (Exception)
        {
            Console.WriteLine("Error: Failed to add employee");
        }
    }

    private static string GetValidatedInput(string prompt, Func<string, bool> validator, int maxLength)
    {
        int attempts = 0;
        while (attempts < 3)
        {
            Console.Write(prompt);
            string input = Console.ReadLine()?.Trim();

            if (!string.IsNullOrWhiteSpace(input) && input.Length <= maxLength && validator(input))
                return input;

            Console.WriteLine("Invalid input. Please try again.");
            attempts++;
        }
        throw new ArgumentException("Too many invalid attempts");
    }

    private static DateTime GetValidatedDate(string prompt)
    {
        int attempts = 0;
        while (attempts < 3)
        {
            Console.Write(prompt);
            if (DateTime.TryParse(Console.ReadLine(), out DateTime date))
                return date;

            Console.WriteLine("Invalid date format. Please try again.");
            attempts++;
        }
        throw new ArgumentException("Invalid date format");
    }

    private static decimal GetValidatedSalary(string prompt)
    {
        int attempts = 0;
        while (attempts < 3)
        {
            Console.Write(prompt);
            if (decimal.TryParse(Console.ReadLine(), out decimal salary) && salary > 0 && salary <= 1000000)
                return salary;

            Console.WriteLine("Invalid salary. Must be positive and reasonable.");
            attempts++;
        }
        throw new ArgumentException("Invalid salary");
    }

    private static bool ValidateName(string name) =>
        !string.IsNullOrWhiteSpace(name) && name.All(c => char.IsLetter(c) || char.IsWhiteSpace(c) || c == '\'');

    private static bool ValidateSSN(string ssn) => SsnRegex.IsMatch(ssn);

    private static bool ValidateAge(DateTime dob) =>
        dob <= DateTime.Today.AddYears(-22) && dob >= DateTime.Today.AddYears(-64);

    private static bool ValidateAddress(string address) =>
        !string.IsNullOrWhiteSpace(address) && address.Any(char.IsDigit);

    private static bool ValidateCity(string city) =>
        !string.IsNullOrWhiteSpace(city) && city.All(c => char.IsLetter(c) || char.IsWhiteSpace(c));

    private static bool ValidateState(string state) => StateRegex.IsMatch(state?.ToUpper() ?? "");

    private static bool ValidateZip(string zip) => ZipRegex.IsMatch(zip);

    private static bool ValidatePhone(string phone) => PhoneRegex.IsMatch(phone);

    private static bool ValidateTitle(string title) =>
        !string.IsNullOrWhiteSpace(title) && title.All(c => char.IsLetter(c) || char.IsWhiteSpace(c));

    private static string TruncateString(string input, int maxLength) =>
        input?.Length > maxLength ? input[..(maxLength - 3)] + "..." : input ?? "";
}