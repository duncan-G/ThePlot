namespace ThePlot.Database.Abstractions;

public interface IDateStamped
{
    DateTime DateCreated { get; set; }
    DateTime DateLastModified { get; set; }
}
