using System.Threading.Tasks;
using MySqlConnector;

namespace Claudel.Data;

public class Database
{
    private readonly string _connectionString;

    public Database(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<MySqlConnection> OpenConnectionAsync()
    {
        var connection = new MySqlConnection(_connectionString);

        await connection.OpenAsync();

        return connection;
    }
}