namespace WarcraftArchive.Api.Configuration;

public class SeedSettings
{
    public const string SectionName = "SeedSettings";
    public bool AdminEnabled { get; set; } = false;
    public string AdminEmail { get; set; } = "admin@local";
    public string AdminUsername { get; set; } = "admin";
    public string AdminPassword { get; set; } = "Admin123!123!";

    /// <summary>
    /// When true the app will try to import demo data from CSV files found in CsvDataPath on startup.
    /// Controlled via env: DEMO_IMPORT_ENABLED=true
    /// </summary>
    public bool DemoImportEnabled { get; set; } = false;

    /// <summary>
    /// Directory containing the Notion-exported CSV files.
    /// Expected files: Personajes*.csv, Raids*.csv (or "Raids, dungeons*"), Content Progress*.csv
    /// Controlled via env: CSV_DATA_PATH
    /// </summary>
    public string CsvDataPath { get; set; } = "/data/csv";
}
