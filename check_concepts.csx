using Npgsql;

var connStr = "Host=localhost;Port=5432;Database=ivf_db;Username=ivf_user;Password=ivf_password123";
using var conn = new NpgsqlConnection(connStr);
conn.Open();

// Check if concepts exist
using var checkCmd = new NpgsqlCommand("SELECT COUNT(*) FROM \"Concepts\"", conn);
var count = (long)checkCmd.ExecuteScalar()!;
Console.WriteLine($"Current concepts count: {count}");

if (count == 0) {
    Console.WriteLine("No concepts found. Database needs seeding.");
} else {
    Console.WriteLine("Concepts already exist.");
}
