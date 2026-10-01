namespace DirectoryManagement.Services;

public static class EntryTypes
{
    public const string Folder = "folder";
    public const string File = "file";
    public const string Link = "link";

    public static string Normalize(string type) => type switch
    {
        Folder or File or Link => type,
        "Klasör" => Folder,
        "Dosya" => File,
        "Bağlantı" => Link,
        _ => type
    };
}
