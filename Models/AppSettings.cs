namespace Claudel.Models;

public class AppSettings
{
    public MySqlSettings MySql { get; set; } = new();

    public S3Settings S3 { get; set; } = new();
}

public class MySqlSettings
{
    public string Host { get; set; } = "";

    public int Port { get; set; } = 3306;

    public string Database { get; set; } = "";

    public string User { get; set; } = "";

    public string Password { get; set; } = "";
}

public class S3Settings
{
    public string AccessKeyId { get; set; } = "";

    public string SecretAccessKey { get; set; } = "";

    public string Region { get; set; } = "ap-northeast-1";

    public string Bucket { get; set; } = "";
}