namespace ThePlot.Database;

public class DatabaseOptions
{
    public required string ConnectionString { get; set; }
    public required int CommandTimeout { get; set; } = 30;
    
    public DatabaseOptions() { } 
}